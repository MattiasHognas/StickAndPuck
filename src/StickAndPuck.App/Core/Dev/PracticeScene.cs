using System.Numerics;

namespace StickAndPuck.App.Core.Dev
{
    /// <summary>The default scene: one home skater with the puck on an empty rink, on keyboard and mouse.</summary>
    public sealed class PracticeScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "practice";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            PlayerData skater =
                world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G)
                ?? new PlayerData { Position = PlayingPosition.C };
            Player p = world.AddPlayer(Side.Home, skater, new Vector3(0, 0, 3));
            p.Slot = 0;
            world.Puck.Carrier = p.Id;
            return world;
        }
    }
}
