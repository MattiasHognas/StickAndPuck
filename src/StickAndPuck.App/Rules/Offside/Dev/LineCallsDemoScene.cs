using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Rules.Offside.Dev
{
    /// <summary>SAP_SCENE=offsidedemo: the match flow with a scripted play a second after each faceoff, in turn
    /// a pass to a winger standing offside, a clean carry-in, and an icing dump from the home end.
    /// The HUD shows the calls; the hybrid race and tag-ups are covered by SAP_SCENE=offsidetest.</summary>
    public sealed class LineCallsDemoScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "offsidedemo";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 120);
            world.Dev.LineCalls.Set(new LineCallsDemo());
            return world;
        }
    }
}
