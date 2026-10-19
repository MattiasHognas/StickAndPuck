using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Playoffs.Dev
{
    /// <summary>SAP_SCENE=playoffs: a playoff test setup. It uses its own save, playoffs-test.json next to the
    /// season save (SAP_SAVE overrides), so the real season is never touched: it starts a season there
    /// if there is none (SAP_SEASON=new, SAP_CLUB, SAP_SEED as in the season scene), settles the rest
    /// of the regular season on paper, human's games included, and plays the human's next playoff game.
    /// Start it again for the next game; when the human's club is out the bracket plays to a champion.
    /// SAP_PERIOD, SAP_OT and SAP_HUMAN=0 work as in the season scene.</summary>
    public sealed class PlayoffScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "playoffs";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            clubs = Leagues.ClubsFromEnvironment(clubs); // SAP_LEAGUE=nhl, or an NHL id in SAP_CLUB or SAP_HOME, plays the NHL
            PlayoffScenes.OpenTestSeason(app, clubs);
            return PlayoffScenes.Play(app, clubs, settings, PlayoffScenes.Human);
        }
    }
}
