using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>SAP_SCENE=goalieswap: the match demo (shots at each net in turn) with two-minute periods and
    /// the energy panel, so a goalie soon lets in three in a short spell and the backup comes in at
    /// the next faceoff.</summary>
    public sealed class GoalieSwapScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "goalieswap";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, LinesScene.Period(120));
            world.Dev.Match.Set(new MatchDemo());
            LinesScenes.Demo(world);
            return world;
        }
    }
}
