using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=matchdemo: the match flow on its own with 25-second periods (or SAP_PERIOD) and no
    /// human. Whenever the puck is in play the scene fires a shot at the attacking net of each club in
    /// turn, so goals, saves, frozen pucks, period ends and the final all happen in a minute or two.
    /// The score and clock line is on.</summary>
    public sealed class MatchDemoScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "matchdemo";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 25);
            world.Dev.Match.Set(new MatchDemo());
            world.Switches.MatchLine = true;
            return world;
        }
    }
}
