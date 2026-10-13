using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI.Dev;

/// <summary>Only for the positioning scenes (SAP_SCENE=positioning and positioningtest): a crude stand-in
/// for the parts of a match that other tasks own and are not in yet, so 5 on 5 can run a whole
/// period. AI carriers shoot from the slot and pass when pressed or after a while, a dead puck is whistled for a faceoff, and the camera follows the puck. When the
/// match flow runs it keeps the clock, goals and faceoffs; without it the stand-in restarts after
/// goals and runs its own clock. It never runs in a normal match.</summary>
internal sealed class StandInPlay
{
    readonly Random dice;
    float goalAt = float.NaN, stillSince, faceoffAt = float.NegativeInfinity;
    Vector3 stillAt;
    int dropIn;

    StandInPlay(int seed, float clock)
    {
#pragma warning disable S2245 // gameplay variation, not security
        dice = new Random(seed);
#pragma warning restore S2245
        Clock = clock;
    }

    /// <summary>Gets or sets a value indicating whether the stand-in moves the camera with the puck.</summary>
    public bool FollowCamera { get; set; }

    /// <summary>Gets or sets a value indicating whether each AI skater's target is drawn as a marker.</summary>
    public bool ShowTargets { get; set; }

    /// <summary>Gets the stand-in clock: game seconds left in the period.</summary>
    public float Clock { get; private set; }

    /// <summary>Gets the goals per side, indexed by <see cref="Side"/>.</summary>
    public int[] Goals { get; } = new int[2];

    /// <summary>Gets how many times a puck lay dead for long enough to need a faceoff.</summary>
    public int DeadPucks { get; private set; }

    /// <summary>Gets a value indicating whether the stand-in runs on a world.</summary>
    public static bool Running(World world) => world.Dev.StandIn.HasValue;

    /// <summary>Turns the stand-in on for a world and drops the puck at centre ice.</summary>
    public static StandInPlay Start(World world, int seed = 3)
    {
        var standIn = new StandInPlay(seed, world.Setup.PeriodSeconds);
        world.Dev.StandIn.Set(standIn);
        Faceoff(world, RinkSpec.CentreDot);
        return standIn;
    }

    /// <summary>Runs the clock, restarts after goals and dead pucks, and moves the camera. Called first in positioning's update.</summary>
    public static void Before(World world, float dt)
    {
        if (world.Dev.StandIn.TryGet(out var standIn)) standIn.Run(world, dt);
    }

    void Run(World world, float dt)
    {
        if (FollowCamera) Follow(world);
        if (MatchFlow.IsRunning(world)) { DeadPuck(world, () => MatchFlow.Whistle(world, MatchFlow.NearestDot(world.Puck.Position))); return; }
        var pk = world.Puck;
        if (world.Match.Phase == MatchPhase.Play) Clock = Math.Max(0, Clock - dt);
        if (Clock <= 0 && !FollowCamera) return;
        if (Clock <= 0) { Clock = world.Setup.PeriodSeconds; Faceoff(world, RinkSpec.CentreDot); }

        // a puck in the net: count the goal, then face off at centre after the celebration
        int end = PuckMotion.FullyOverGoalLine(pk.Position, 1) ? 1 : PuckMotion.FullyOverGoalLine(pk.Position, -1) ? -1 : 0;
        if (end != 0 && float.IsNaN(goalAt))
        {
            goalAt = world.Time;
            var defender = end < 0 ? world.Match.DefendsLeft : Other(world.Match.DefendsLeft);
            Goals[(int)Other(defender)]++;
        }
        if (!float.IsNaN(goalAt) && world.Time - goalAt > 1.5f) Faceoff(world, RinkSpec.CentreDot);

        DeadPuck(world, () => Faceoff(world, MatchFlow.NearestDot(world.Puck.Position)));

        if (dropIn > 0 && --dropIn == 0)
        {
            // the drop: the puck squirts a little to one side
            float angle = dice.NextSingle() * MathF.Tau;
            pk.Velocity = new Vector3(SimMath.Cos(angle), 0, SimMath.Sin(angle)) * (1.5f + dice.NextSingle() * 2f);
            pk.Live = true;
        }
    }

    /// <summary>A loose puck nobody gets to, or one a goalie keeps, is faced off again at the nearest dot.</summary>
    void DeadPuck(World world, Action restart)
    {
        var pk = world.Puck;
        bool goalieHolds = pk.Carrier >= 0 && world.Players[pk.Carrier].IsGoalie;
        if (!pk.Live || (pk.Carrier >= 0 && !goalieHolds) || Vector3.Distance(pk.Position, stillAt) > 0.5f)
        {
            stillAt = pk.Position;
            stillSince = world.Time;
        }
        if (world.Time - stillSince <= (goalieHolds ? 6f : 8f)) return;
        DeadPucks++;
        stillSince = world.Time;
        restart();
    }

    /// <summary>Puts the skaters in their faceoff spots around a dot and drops the puck after a moment.</summary>
    public static void Faceoff(World world, Vector3 dot)
    {
        foreach (var p in world.Players)
        {
            if (!p.OnIce) continue;
            int end = PuckHandling.AttackEnd(world, p.Side);
            var spot = p.IsGoalie
                ? new Vector2(-end * (RinkSpec.GoalLineX - 1f), 0)
                : TeamShape.Faceoff(Positioning.RoleOf(world, p), end, new Vector2(dot.X, dot.Z));
            p.Position = new Vector3(spot.X, 0, spot.Y);
            p.Velocity = Vector3.Zero;
            p.Heading = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            if (p.Slot < 0) p.Command = default;   // a human's command is the input system's, never the AI's
            p.Begin(PlayerAction.None);
        }
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.LastTouch = -1;
        pk.Position = dot with { Y = 0 };
        pk.Velocity = Vector3.Zero;
        pk.Live = false;
        if (!world.Dev.StandIn.TryGet(out var standIn)) return;
        standIn.dropIn = 30;
        standIn.goalAt = float.NaN;
        standIn.stillAt = pk.Position;
        standIn.stillSince = standIn.faceoffAt = world.Time;
    }

    void Follow(World world)
    {
        var cam = BroadcastCamera.Straight();
        float want = Math.Clamp(world.Puck.Position.X, -17f, 17f);
        float x = world.Time - faceoffAt < 0.05f ? want : world.Camera.Target.X - cam.Target.X + (want - (world.Camera.Target.X - cam.Target.X)) * 0.06f;
        x = MathF.Round(x * 32f) / 32f;
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }

    /// <summary>Draws a small marker where each AI skater is heading (SAP_AI_TARGETS=1).</summary>
    public static void DrawTargets(World world)
    {
        if (!world.Dev.StandIn.TryGet(out var standIn) || !standIn.ShowTargets) return;
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || p.Slot >= 0) continue;
            var t = Positioning.TargetOf(world, p);
            var colour = p.Side == Side.Home ? new Color(0xef, 0xe0, 0xc2, 255) : new Color(0xc8, 0x57, 0x4a, 255);
            DrawCube(new Vector3(t.X, 0.05f, t.Y), 0.35f, 0.1f, 0.35f, colour);
        }
    }

    /// <summary>Draws the stand-in clock and tally.</summary>
    public static void DrawHud(World world)
    {
        if (!world.Dev.StandIn.TryGet(out var standIn)) return;
        bool rules = MatchFlow.IsRunning(world);
        int[] goals = rules ? world.Match.Score : standIn.Goals;
        string clock = MatchFlow.ClockText(rules ? world.Match.Clock : standIn.Clock);
        string text = $"{world.Setup.Home.ShortName} {goals[0]} - {goals[1]} {world.Setup.Away.ShortName}   {clock}   SHOTS {Decisions.Tally(world, Side.Home).Shots}-{Decisions.Tally(world, Side.Away).Shots}   PASSES {Pct(world, Side.Home)}-{Pct(world, Side.Away)}";
        DrawText(text, 6, 6, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }

    static string Pct(World world, Side side)
    {
        var t = Decisions.Tally(world, side);
        return $"{t.Completed}/{t.Passes}";
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }
}
