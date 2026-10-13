using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Dev;

/// <summary>Test setup, SAP_SCENE=handling: the human carries the puck at centre ice with two
/// teammates up ice on the wings and one back on defence, against an away defender in front of
/// the away net. Arrows skate, space (or left mouse, or gamepad A) shoots: tap for a wrist shot,
/// hold for a slap shot. E (or right mouse, or gamepad X) passes toward the way the stick points,
/// and control follows the pass.</summary>
public sealed class HandlingScene : IScene
{
    /// <inheritdoc/>
    public string Name => "handling";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        var home = Skaters(world.Setup.Home);
        var away = Skaters(world.Setup.Away);

        var carrier = Add(world, Side.Home, home[0], new Vector3(-4, 0, 1), MathF.PI / 2);
        Add(world, Side.Home, home[1 % home.Count], new Vector3(8, 0, -6), MathF.PI / 2);
        Add(world, Side.Home, home[2 % home.Count], new Vector3(10, 0, 6), MathF.PI / 2);
        Add(world, Side.Home, home[3 % home.Count], new Vector3(-12, 0, 0), MathF.PI / 2);
        Add(world, Side.Away, away[0], new Vector3(18, 0, -1.5f), -MathF.PI / 2);
        if (world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is { } goalie)
            Add(world, Side.Away, goalie, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0), -MathF.PI / 2);

        carrier.Slot = 0;
        world.Puck.Carrier = carrier.Id;
        world.Puck.Position = PuckHandling.Blade(carrier);
        return world;
    }

    static List<PlayerData> Skaters(ClubData club)
    {
        var list = club.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
    }

    static Player Add(World world, Side side, PlayerData data, Vector3 at, float heading)
    {
        var p = world.AddPlayer(side, data, at);
        p.Heading = heading;
        return p;
    }
}
