using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Playoffs.Dev
{
    /// <summary>SAP_SCENE=playoffbracket: the playoff test save's bracket over the rink, nothing played.</summary>
    public sealed class PlayoffBracketScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "playoffbracket";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            PlayoffScenes.OpenTestSeason(app, clubs);
            _ = app.Playoffs.Open();
            if (app.Playoffs.Current.TryGet(out PlayoffState playoffs))
            {
                PlayoffSystem.Log(playoffs);
            }

            return PlayoffScenes.BracketWorld(clubs, settings);
        }
    }
}
