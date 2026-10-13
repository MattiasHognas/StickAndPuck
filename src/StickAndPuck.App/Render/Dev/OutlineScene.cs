using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Render.Dev;

/// <summary>SAP_SCENE=outlines: twelve skaters, six per club, packed into two overlapping rows at centre
/// ice, so every outline between touching players shows.</summary>
public sealed class OutlineScene : IScene
{
    /// <inheritdoc/>
    public string Name => "outlines";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var skaters = world.Setup.Club(side).Roster.Where(p => p.Position != PlayingPosition.G).ToList();
            float row = side == Side.Home ? -0.45f : 0.45f;
            for (int i = 0; i < 6; i++)
            {
                var data = skaters.Count > 0 ? skaters[i % skaters.Count] : new PlayerData { Position = PlayingPosition.C };
                var p = world.AddPlayer(side, data, new Vector3((i - 2.5f) * 1.1f + row * 0.9f, 0, row));
                p.Heading = side == Side.Home ? 0.6f : -0.6f + MathF.PI;
            }
        }
        world.Puck.Position = RinkSpec.CentreDot;
        return world;
    }
}
