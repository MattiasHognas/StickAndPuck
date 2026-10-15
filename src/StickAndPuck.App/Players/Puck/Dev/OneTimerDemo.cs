using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Dev;

/// <summary>SAP_SCENE=onetimer: a home passer feeds a teammate in the slot who presses shoot right after
/// the pass, holds the wind-up while the puck travels and one-times it on arrival. Every second round an
/// away skater stands in the lane and intercepts, so the wind-up drops. The camera sits close on the
/// play, and the script loops with a pause.</summary>
public sealed class OneTimerScene : IScene
{
    /// <inheritdoc/>
    public string Name => "onetimer";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings with { OneTimers = true });
        OneTimerDemo.Attach(world);
        return world;
    }
}

/// <summary>The script and camera behind <see cref="OneTimerScene"/>, run from the skater renderer.</summary>
internal sealed class OneTimerDemo
{
    /// <summary>The step the pass leaves on.</summary>
    const int PassStep = 60;

    /// <summary>Steps after the pass that the receiver presses shoot.</summary>
    const int PressAfter = 4;

    /// <summary>Steps each round runs, the pause after the shot included.</summary>
    const int RoundSteps = 300;

    static readonly ConditionalWeakTable<World, OneTimerDemo> Demos = [];

    Maybe<Player> passer, receiver, cutter;
    float start;
    int round = -1;

    /// <summary>Gets a value indicating whether this round's pass is cut off by the away skater.</summary>
    bool Intercepted => round % 2 == 1;

    /// <summary>Gets the one-timer scene's script for a world, if it runs one.</summary>
    public static Maybe<OneTimerDemo> Of(World world) => Demos.TryGetValue(world, out var demo) ? demo : Maybe<OneTimerDemo>.None;

    /// <summary>Adds the players and runs the one-timer scene's script in a world.</summary>
    public static void Attach(World world)
    {
        var demo = new OneTimerDemo();
        var home = world.Setup.Home.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        var away = world.Setup.Away.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        PlayerData Pick(List<PlayerData> list, int i) => list.Count > 0 ? list[i % list.Count] : new PlayerData { Position = PlayingPosition.C };
        demo.passer = world.AddPlayer(Side.Home, Pick(home, 0), Vector3.Zero);
        demo.receiver = world.AddPlayer(Side.Home, Pick(home, 1), Vector3.Zero);
        demo.cutter = world.AddPlayer(Side.Away, Pick(away, 3), Vector3.Zero);
        if (world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is { } g)
            world.AddPlayer(Side.Away, g, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0)).Heading = -MathF.PI / 2;
        Demos.Add(world, demo);
        demo.Reset(world);
    }

    /// <summary>Writes the next step's commands, frames the play and starts a new round when this one is over.</summary>
    public void Run(World world)
    {
        if (!passer.TryGet(out var a) || !receiver.TryGet(out var r)) return;
        int step = (int)MathF.Round((world.Time - start) / Game.Step);
        var toMate = PuckHandling.Blade(r) - world.Puck.Position;
        a.Command = step == PassStep ? new PlayerCommand { Move = Vector2.Normalize(new Vector2(toMate.X, toMate.Z)), Pass = true } : default;
        r.Command = new PlayerCommand { Shoot = step >= PassStep + PressAfter && step < PassStep + PressAfter + 30 };
        world.Camera = BroadcastCamera.Straight((a.Position.X + r.Position.X) / 2f, 31.5f, 17f);
        if (step >= RoundSteps) Reset(world);
    }

    void Reset(World world)
    {
        if (!passer.TryGet(out var a) || !receiver.TryGet(out var r) || !cutter.TryGet(out var c)) return;   // never: Attach adds them first
        round++;
        int end = PuckHandling.AttackEnd(world, Side.Home);
        float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        Place(a, new Vector3(end * (RinkSpec.GoalLineX - 20f), 0, -4f), face);
        Place(r, new Vector3(end * (RinkSpec.GoalLineX - 9f), 0, 2.5f), face);
        var mid = (PuckHandling.Blade(a) + PuckHandling.Blade(r)) / 2;
        // in the lane, its blade on the line between the two sticks, or trailing the passer well out of it
        var at = Intercepted ? mid - PuckHandling.Blade(Vector3.Zero, -face) : a.Position + new Vector3(-end * 5f, 0, 3f);
        Place(c, at, -face);
        var pk = world.Puck;
        pk.Carrier = a.Id;
        pk.Position = PuckHandling.Blade(a);
        pk.Velocity = Vector3.Zero;
        pk.Live = true;
        start = world.Time;
    }

    static void Place(Player p, Vector3 at, float heading)
    {
        p.Position = at;
        p.Velocity = Vector3.Zero;
        p.Heading = heading;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }
}
