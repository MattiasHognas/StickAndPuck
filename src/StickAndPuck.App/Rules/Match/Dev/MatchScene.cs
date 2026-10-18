using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=match: a full exhibition between SAP_HOME and SAP_AWAY with both starting lineups,
    /// the human on the home centre, the intro, three periods and the clock. SAP_PERIOD=seconds
    /// shortens the periods; SAP_MATCH_DEBUG=1 shows a plain score and clock line.</summary>
    public sealed class MatchScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "match";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 20 * 60);
            if (
                world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is
                { } centre
            )
            {
                centre.Slot = 0;
            }

            return world;
        }
    }
}
