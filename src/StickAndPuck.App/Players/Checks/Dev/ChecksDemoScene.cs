using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Checks.Dev
{
    /// <summary>SAP_SCENE=checksdemo: a home skater runs down the away carrier and checks them off the puck, over and over.</summary>
    public sealed class ChecksDemoScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "checksdemo";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            ChecksDemo.Setup(world, script: true);
            return world;
        }
    }
}
