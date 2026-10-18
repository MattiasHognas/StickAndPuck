using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Arcade.Dev
{
    /// <summary>SAP_SCENE=arcadesummary: the saved arcade run's panel (or a new run's) over the rink, nothing played.</summary>
    public sealed class ArcadeSummaryScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "arcadesummary";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            _ = ArcadeScenes.Open(app, clubs);
            return ArcadeScenes.SummaryWorld(clubs, settings);
        }
    }
}
