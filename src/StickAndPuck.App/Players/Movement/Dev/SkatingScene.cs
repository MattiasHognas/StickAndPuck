using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Dev;

/// <summary>Test setup, SAP_SCENE=skating: three skaters race from the left blue line area with the stick
/// held flat out, slow (traits 20), average (50) and fast (80), so trait differences show at a
/// glance. A fourth skater, behind them, is the human's to try every input device with.</summary>
public sealed class SkatingScene : IScene
{
    /// <inheritdoc/>
    public string Name => "skating";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        var roster = world.Setup.Home.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        PlayerData Skater(int i, int trait) => (roster.Count > 0 ? roster[i % roster.Count] : new PlayerData { Position = PlayingPosition.C }) with
        {
            Traits = new Traits { Acceleration = trait, TopSpeed = trait },
        };

        int[] traits = [20, 50, 80];
        for (int i = 0; i < traits.Length; i++)
        {
            var racer = world.AddPlayer(Side.Home, Skater(i, traits[i]), new Vector3(-12f, 0, -6f + i * 4f));
            racer.Heading = MathF.PI / 2;
            racer.Command = new PlayerCommand { Move = Vector2.UnitX };   // nothing else writes their command, so they keep skating
        }

        var human = world.AddPlayer(Side.Home, Skater(3, 50), new Vector3(-12f, 0, 7f));
        human.Heading = MathF.PI / 2;
        human.Slot = 0;
        return world;
    }
}
