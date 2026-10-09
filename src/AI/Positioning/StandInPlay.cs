using System.Numerics;
using Raylib_cs;
using StickAndPuck.Camera;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.AI;

/// <summary>
/// Only for the positioning scenes (SAP_SCENE=positioning and positioningtest): a crude stand-in
/// for the parts of a match that other tasks own and are not in yet, so 5 on 5 can run a whole
/// period. AI carriers shoot from the slot and pass when pressed or after a while (W6b replaces
/// this), a dead puck is whistled for a faceoff, and the camera follows the puck (W9). When W7a's
/// match flow runs it keeps the clock, goals and faceoffs; without it the stand-in restarts after
/// goals and runs its own clock. It never runs in a normal match.
/// </summary>
internal static class StandInPlay
{
    static readonly Dictionary<int, int> Shooting = [];
    static Random dice = new(3);
    static int carrier = -1;
    static float gotAt, passAfter, goalAt = float.NaN, stillSince, faceoffAt = float.NegativeInfinity;
    static Vector3 stillAt;
    static int dropIn;

    /// <summary>Gets a value indicating whether the stand-in runs.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>Gets or sets a value indicating whether the stand-in moves the camera with the puck.</summary>
    public static bool FollowCamera { get; set; }

    /// <summary>Gets or sets a value indicating whether each AI skater's target is drawn as a marker.</summary>
    public static bool ShowTargets { get; set; }

    /// <summary>Gets the stand-in clock: game seconds left in the period.</summary>
    public static float Clock { get; private set; }

    /// <summary>Gets the goals per side, indexed by <see cref="Side"/>.</summary>
    public static int[] Goals { get; } = new int[2];

    /// <summary>Gets the shots taken per side, indexed by <see cref="Side"/>.</summary>
    public static int[] Shots { get; } = new int[2];

    /// <summary>Gets how many times a puck lay dead for long enough to need a faceoff.</summary>
    public static int DeadPucks { get; private set; }

    /// <summary>Turns the stand-in on for a world and drops the puck at centre ice.</summary>
    /// <param name="world">The match.</param>
    /// <param name="seed">Seed for the stand-in's choices, so tests repeat.</param>
    public static void Start(World world, int seed = 3)
    {
        Enabled = true;
#pragma warning disable S2245 // gameplay variation, not security
        dice = new Random(seed);
#pragma warning restore S2245
        Shooting.Clear();
        carrier = -1;
        goalAt = float.NaN;
        Clock = world.Setup.PeriodSeconds;
        Array.Clear(Goals);
        Array.Clear(Shots);
        DeadPucks = 0;
        Faceoff(world, RinkSpec.CentreDot);
    }

    /// <summary>Turns the stand-in off.</summary>
    public static void Stop()
    {
        Enabled = false;
        FollowCamera = false;
        ShowTargets = false;
    }

    /// <summary>Runs the clock, restarts after goals and dead pucks, and moves the camera. Called first in positioning's update.</summary>
    /// <param name="world">The match.</param>
    /// <param name="dt">The step.</param>
    public static void Before(World world, float dt)
    {
        if (!Enabled) return;
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
            pk.Velocity = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (1.5f + dice.NextSingle() * 2f);
            pk.Live = true;
        }
    }

    /// <summary>A loose puck nobody gets to, or one a goalie keeps, is faced off again at the nearest dot.</summary>
    static void DeadPuck(World world, Action restart)
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

    /// <summary>Adds a carrier's shot or pass to the move positioning chose.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">An AI skater.</param>
    /// <param name="command">The command so far.</param>
    public static void Act(World world, Player p, ref PlayerCommand command)
    {
        if (!Enabled) return;
        int end = PuckHandling.AttackEnd(world, p.Side);
        var net = new Vector2(end * RinkSpec.GoalLineX, 0);
        var at = new Vector2(p.Position.X, p.Position.Z);

        // a shot under way: keep shoot held, then let go
        if (Shooting.TryGetValue(p.Id, out int left))
        {
            if (left > 0) { Shooting[p.Id] = left - 1; command = command with { Move = Vector2.Normalize(net - at), Shoot = true }; }
            else Shooting.Remove(p.Id);
            return;
        }

        var pk = world.Puck;
        if (pk.Carrier != p.Id || p.Action != PlayerAction.None) return;
        if (carrier != p.Id) { carrier = p.Id; gotAt = world.Time; passAfter = 1.5f + dice.NextSingle() * 3f; }
        float held = world.Time - gotAt, pa = end * p.Position.X;
        bool pressed = world.Players.Any(o => o.OnIce && o.Side != p.Side && !o.IsGoalie && Vector3.Distance(o.Position, p.Position) < 2.5f);

        bool shootingRange = pa > RinkSpec.GoalLineX - 15f && pa < RinkSpec.GoalLineX - 1f && MathF.Abs(p.Position.Z) < 9f;
        if (held > 0.3f && shootingRange && (pa > RinkSpec.GoalLineX - 10f || dice.NextSingle() < 0.02f))
        {
            int hold = pa < RinkSpec.GoalLineX - 12f ? 40 : 1;   // a slap shot from the point, a wrist shot in close
            Shooting[p.Id] = hold;
            Shots[(int)p.Side]++;
            command = command with { Move = Vector2.Normalize(net - at), Shoot = true };
            return;
        }

        if (held > passAfter || (pressed && held > 0.5f))
        {
            var mate = OpenMate(world, p, end);
            if (mate == null) { passAfter = held + 1f; return; }
            var to = PuckHandling.Blade(mate) - PuckHandling.Blade(p);
            command = command with { Move = Vector2.Normalize(new Vector2(to.X, to.Z)), Pass = true };
        }
    }

    /// <summary>The best teammate to pass to: open, not too close or far, preferably further up ice.</summary>
    static Player? OpenMate(World world, Player p, int end)
    {
        var from = PuckHandling.Blade(p);
        Player? best = null;
        float bestScore = float.MinValue;
        foreach (var mate in world.OnIce(p.Side))
        {
            if (mate == p || mate.IsGoalie) continue;
            var to = PuckHandling.Blade(mate);
            float d = Vector3.Distance(from, to);
            if (d is < 4f or > 25f) continue;
            float open = world.Players.Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie)
                .Select(o => LaneDistance(from, to, o.Position)).DefaultIfEmpty(99f).Min();
            if (open < 1.5f) continue;
            float score = end * (to.X - from.X) * 0.15f + Math.Min(open, 5f) - d * 0.05f + dice.NextSingle();
            if (score > bestScore) { bestScore = score; best = mate; }
        }
        return best;
    }

    static float LaneDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        var ab = new Vector2(b.X - a.X, b.Z - a.Z);
        var ap = new Vector2(p.X - a.X, p.Z - a.Z);
        float t = Math.Clamp(Vector2.Dot(ap, ab) / Math.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return (ap - ab * t).Length();
    }

    /// <summary>Puts the skaters in their faceoff spots around a dot and drops the puck after a moment.</summary>
    /// <param name="world">The match.</param>
    /// <param name="dot">The faceoff dot.</param>
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
            p.Command = default;
            p.Begin(PlayerAction.None);
        }
        Shooting.Clear();
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.LastTouch = -1;
        pk.Position = dot with { Y = 0 };
        pk.Velocity = Vector3.Zero;
        pk.Live = false;
        dropIn = 30;
        goalAt = float.NaN;
        stillAt = pk.Position;
        stillSince = faceoffAt = world.Time;
    }

    static void Follow(World world)
    {
        var cam = BroadcastCamera.Straight();
        float want = Math.Clamp(world.Puck.Position.X, -17f, 17f);
        float x = world.Time - faceoffAt < 0.05f ? want : world.Camera.Target.X - cam.Target.X + (want - (world.Camera.Target.X - cam.Target.X)) * 0.06f;
        x = MathF.Round(x * 32f) / 32f;
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }

    /// <summary>Draws a small marker where each AI skater is heading (SAP_AI_TARGETS=1).</summary>
    /// <param name="world">The match.</param>
    public static void DrawTargets(World world)
    {
        if (!Enabled || !ShowTargets) return;
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || p.Slot >= 0) continue;
            var t = Positioning.TargetOf(world, p);
            var colour = p.Side == Side.Home ? new Color(0xef, 0xe0, 0xc2, 255) : new Color(0xc8, 0x57, 0x4a, 255);
            DrawCube(new Vector3(t.X, 0.05f, t.Y), 0.35f, 0.1f, 0.35f, colour);
        }
    }

    /// <summary>Draws the stand-in clock and tally.</summary>
    /// <param name="world">The match.</param>
    public static void DrawHud(World world)
    {
        if (!Enabled) return;
        bool rules = MatchFlow.IsRunning(world);
        var goals = rules ? world.Match.Score : Goals;
        string clock = MatchFlow.ClockText(rules ? world.Match.Clock : Clock);
        string text = $"{world.Setup.Home.ShortName} {goals[0]} - {goals[1]} {world.Setup.Away.ShortName}   {clock}   SHOTS {Shots[0]}-{Shots[1]}";
        DrawText(text, 6, 6, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }
}
