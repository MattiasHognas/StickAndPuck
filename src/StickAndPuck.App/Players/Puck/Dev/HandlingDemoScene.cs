using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>SAP_SCENE=handlingdemo: plays a carry, pass and shot on goal by itself, over and over, with the
    /// camera following the puck along the rink. For recording what puck handling does.</summary>
    public sealed class HandlingDemoScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "handlingdemo";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            HandlingDemo.Setup(world);
            return world;
        }
    }
}
