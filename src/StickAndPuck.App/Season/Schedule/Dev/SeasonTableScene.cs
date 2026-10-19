using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule.Dev
{
    /// <summary>SAP_SCENE=seasontable: the saved season's table (or a new season's) over the rink, nothing played.</summary>
    public sealed class SeasonTableScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "seasontable";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            clubs = Leagues.ClubsFromEnvironment(clubs); // SAP_LEAGUE=nhl, or an NHL id in SAP_CLUB or SAP_HOME, plays the NHL
            _ = SeasonScenes.Open(app, clubs);
            return SeasonScenes.TableWorld(clubs, settings);
        }
    }
}
