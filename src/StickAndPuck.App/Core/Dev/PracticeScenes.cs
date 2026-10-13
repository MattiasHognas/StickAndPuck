using System.Numerics;

namespace StickAndPuck.App.Core.Dev;

/// <summary>The default scene: one home skater with the puck on an empty rink, on keyboard and mouse.</summary>
public sealed class PracticeScene : IScene
{
    /// <inheritdoc/>
    public string Name => "practice";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        var skater = world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G) ?? new PlayerData { Position = PlayingPosition.C };
        var p = world.AddPlayer(Side.Home, skater, new Vector3(0, 0, 3));
        p.Slot = 0;
        world.Puck.Carrier = p.Id;
        return world;
    }
}

/// <summary>Both clubs' starters at the opening faceoff, the human on the home centre. Puck on the centre dot.</summary>
public sealed class LineupScene : IScene
{
    /// <inheritdoc/>
    public string Name => "lineup";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        if (world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre) centre.Slot = 0;
        world.Puck.Position = RinkSpec.CentreDot;
        return world;
    }
}
