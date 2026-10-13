using System.Numerics;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Dev;

/// <summary>
/// SAP_SCENE=handlingdemo: plays a carry, pass and shot on goal by itself, over and over, with the
/// camera following the puck along the rink. For recording what puck handling does.
/// </summary>
public sealed class HandlingDemoScene : IScene
{
    /// <inheritdoc/>
    public string Name => "handlingdemo";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        HandlingDemo.Setup(world);
        return world;
    }
}

/// <summary>The script behind <see cref="HandlingDemoScene"/>, driven from <see cref="PuckHandlingSystem"/>.</summary>
internal sealed class HandlingDemo
{
    Maybe<Player> carrier, receiver, defender, goalie;
    float start;
    int got = -1, shotAt = -1, round;

    /// <summary>Adds the players for the demo.</summary>
    /// <param name="world">The world to fill.</param>
    public static void Setup(World world)
    {
        var demo = new HandlingDemo();
        world.Dev.Handling.Set(demo);
        demo.Fill(world);
    }

    void Fill(World world)
    {
        var home = world.Setup.Home.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        var away = world.Setup.Away.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        PlayerData Pick(List<PlayerData> list, int i) => list.Count > 0 ? list[i % list.Count] : new PlayerData { Position = PlayingPosition.C };
        carrier = world.AddPlayer(Side.Home, Pick(home, 0), Vector3.Zero);
        receiver = world.AddPlayer(Side.Home, Pick(home, 1), Vector3.Zero);
        defender = world.AddPlayer(Side.Away, Pick(away, 3), Vector3.Zero);
        if (world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is { } g)
            goalie = world.AddPlayer(Side.Away, g, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0));
        Reset(world);
    }

    void Reset(World world)
    {
        if (!carrier.TryGet(out var c) || !receiver.TryGet(out var r) || !defender.TryGet(out var d)) return;   // never: Setup adds them first
        Place(c, new Vector3(-8, 0, 2 + (round % 2) * 3), MathF.PI / 2);
        Place(r, new Vector3(11, 0, round % 2 == 0 ? -4.5f : 4.5f), MathF.PI / 2);
        Place(d, new Vector3(17, 0, round % 2 == 0 ? 3 : -3), -MathF.PI / 2);
        if (goalie.TryGet(out var g)) Place(g, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0), -MathF.PI / 2);
        c.Command = new PlayerCommand { Move = Vector2.UnitX };
        var pk = world.Puck;
        pk.Carrier = c.Id;
        pk.Position = PuckHandling.Blade(c);
        pk.Velocity = Vector3.Zero;
        pk.Live = true;
        start = world.Time;
        got = shotAt = -1;
        round++;
    }

    static void Place(Player p, Vector3 at, float heading)
    {
        p.Position = at;
        p.Velocity = Vector3.Zero;
        p.Heading = heading;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    /// <summary>Writes this step's commands and moves the camera with the puck.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (world.Dev.Handling.TryGet(out var demo)) demo.Run(world);
    }

    void Run(World world)
    {
        if (!carrier.TryGet(out var c) || !receiver.TryGet(out var r)) return;
        var pk = world.Puck;
        int step = (int)MathF.Round((world.Time - start) / Game.Step);
        if (step == 80)
        {
            var toMate = PuckHandling.Blade(r) - pk.Position;
            c.Command = new PlayerCommand { Move = Vector2.Normalize(new Vector2(toMate.X, toMate.Z)), Pass = true };
        }
        else if (step > 80)
        {
            c.Command = default;
        }

        if (got < 0 && pk.Carrier == r.Id) got = step;
        if (got >= 0 && shotAt < 0)
        {
            bool slap = round % 2 == 0;
            int release = got + (slap ? 70 : 35);
            r.Command = new PlayerCommand { Move = new Vector2(1, r.Position.Z > 0 ? -0.3f : 0.3f), Shoot = step >= got + 20 && step < release };
            if (step >= release) { shotAt = step; r.Command = default; }
        }
        if (step > 420 || (shotAt >= 0 && step > shotAt + 150)) Reset(world);
        Follow(world);
    }

    static void Follow(World world)
    {
        var cam = BroadcastCamera.Straight();
        float want = Math.Clamp(world.Puck.Position.X, -10f, 17f);
        float x = MathF.Round(Lerp(world.Camera.Target.X - cam.Target.X, want, 0.08f) * 32f) / 32f;
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }

    static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
