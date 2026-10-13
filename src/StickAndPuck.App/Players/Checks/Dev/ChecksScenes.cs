using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Dev;

/// <summary>
/// W4c test setup, SAP_SCENE=checks: an away carrier skates loops around centre ice with the puck
/// while you, on a home skater, hunt them down. Arrows skate, shift sprints, Q (or gamepad B)
/// checks, C (or gamepad Y) switches to the teammate nearest the puck. Knock the carrier off the
/// puck and pick it up; control follows pickups and goes to the nearest skater on defence.
/// </summary>
public sealed class ChecksScene : IScene
{
    /// <inheritdoc/>
    public string Name => "checks";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        ChecksDemo.Setup(world, script: false);
        return world;
    }
}

/// <summary>SAP_SCENE=checksdemo: a home skater runs down the away carrier and checks them off the puck, over and over.</summary>
public sealed class ChecksDemoScene : IScene
{
    /// <inheritdoc/>
    public string Name => "checksdemo";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        ChecksDemo.Setup(world, script: true);
        return world;
    }
}

/// <summary>The script behind <see cref="ChecksScene"/> and <see cref="ChecksDemoScene"/>, driven from <see cref="SwitchingSystem"/> so its commands land before skating.</summary>
internal sealed class ChecksDemo(ChecksCast cast, bool scripted)
{
    float start, lostAt = float.PositiveInfinity;

    /// <summary>Adds the players.</summary>
    /// <param name="world">The world to fill.</param>
    /// <param name="script">True to play the check by itself; false to give the hunter to the human.</param>
    public static void Setup(World world, bool script)
    {
        var home = Skaters(world.Setup.Home);
        var away = Skaters(world.Setup.Away);
        // the hunter is the club's best checker, the carrier the away club's weakest, so the hit shows
        var hunter = world.AddPlayer(Side.Home, home.MaxBy(p => p.Traits.Checking)!, Vector3.Zero);
        var mate1 = world.AddPlayer(Side.Home, home[0] == hunter.Data ? home[1 % home.Count] : home[0], Vector3.Zero);
        var mate2 = world.AddPlayer(Side.Home, home[2 % home.Count], Vector3.Zero);
        var carrier = world.AddPlayer(Side.Away, away.MinBy(p => p.Traits.Checking)!, Vector3.Zero);
        var c = new ChecksCast(carrier, hunter, mate1, mate2);
        var demo = new ChecksDemo(c, script);
        world.Dev.Checks.Set(demo);
        if (!script) hunter.Slot = 0;
        demo.Reset(world, c);
    }

    static List<PlayerData> Skaters(ClubData club)
    {
        var list = club.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
    }

    void Reset(World world, ChecksCast c)
    {
        Place(c.Carrier, new Vector3(4, 0, 1), -MathF.PI / 2);
        Place(c.Hunter, new Vector3(7.5f, 0, 1.5f), -MathF.PI / 2);
        Place(c.Mate1, new Vector3(-6, 0, -5), MathF.PI / 2);
        Place(c.Mate2, new Vector3(-8, 0, 5), MathF.PI / 2);
        c.Carrier.Velocity = new Vector3(-3, 0, 0);
        c.Hunter.Velocity = new Vector3(-5, 0, 0);
        var pk = world.Puck;
        pk.Carrier = c.Carrier.Id;
        pk.Position = PuckHandling.Blade(c.Carrier);
        pk.Velocity = Vector3.Zero;
        pk.Live = true;
        start = world.Time;
        lostAt = float.PositiveInfinity;
    }

    static void Place(Player p, Vector3 at, float heading)
    {
        p.Position = at;
        p.Velocity = Vector3.Zero;
        p.Heading = heading;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    /// <summary>Writes this step's commands for the scripted players.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (world.Dev.Checks.TryGet(out var demo)) demo.Run(world);
    }

    void Run(World world)
    {
        var c = cast;
        var (carrier, hunter) = (c.Carrier, c.Hunter);
        float t = world.Time - start;

        // the carrier circles centre ice while they have the puck
        if (carrier.Slot < 0 && world.Puck.Carrier == carrier.Id)
        {
            float a = t * 0.6f;
            carrier.Command = new PlayerCommand { Move = new Vector2(-SimMath.Cos(a), -SimMath.Sin(a)) * 0.6f };
        }
        else if (carrier.Slot < 0)
        {
            carrier.Command = default;
        }

        if (scripted)
        {
            var to = carrier.Position - hunter.Position;
            bool press = hunter.Action == PlayerAction.None && world.Puck.Carrier == carrier.Id && to.Length() < 1.4f;
            hunter.Command = world.Puck.Carrier == carrier.Id
                ? new PlayerCommand { Move = Vector2.Normalize(new Vector2(to.X, to.Z)), Sprint = true, Check = press }
                : new PlayerCommand { Move = ToPuck(world, hunter) };
            if (t > 4.5f) Reset(world, c);
        }
        else
        {
            // a few seconds after the carrier loses the puck, start a new chase
            if (world.Puck.Carrier == carrier.Id) lostAt = float.PositiveInfinity;
            else if (float.IsPositiveInfinity(lostAt)) lostAt = world.Time;
            if (world.Time - lostAt > 4f) Reset(world, c);
        }
    }

    static Vector2 ToPuck(World world, Player p)
    {
        var d = world.Puck.Position - p.Position;
        var v = new Vector2(d.X, d.Z);
        return v.Length() > 0.3f ? Vector2.Normalize(v) * 0.6f : Vector2.Zero;
    }
}

/// <summary>The four skaters a checks scene adds.</summary>
/// <param name="Carrier">The away carrier.</param>
/// <param name="Hunter">The home checker.</param>
/// <param name="Mate1">A home teammate.</param>
/// <param name="Mate2">Another home teammate.</param>
internal sealed record ChecksCast(Player Carrier, Player Hunter, Player Mate1, Player Mate2);
