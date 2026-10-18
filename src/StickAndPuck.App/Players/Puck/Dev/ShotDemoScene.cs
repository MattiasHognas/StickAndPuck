using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>SAP_SCENE=shotdemo: a skater in the slot shoots at the away net over and over, a tap, then a
    /// half hold, then a full wind-up, so the shot height and the airborne puck's shadow can be seen
    /// and recorded. The puck is dead, so goals do not count and no replay cuts in.</summary>
    public sealed class ShotDemoScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "shotdemo";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            ShotDemo.Setup(world);
            return world;
        }
    }
}
