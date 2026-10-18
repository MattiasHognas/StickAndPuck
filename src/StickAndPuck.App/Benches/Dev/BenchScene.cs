using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Benches.Dev
{
    /// <summary>SAP_SCENE=benches: an all-AI exhibition with the camera close on the home bench, where a
    /// script steps through the reactions every few seconds in place of the match's own events: a goal
    /// for, a goal against, a penalty against and one for, a fight, a hard check, a shot off the post, an
    /// offside, a stoppage (chats, the trainer and the coach) and a long shift (the line is run down, comes
    /// off and a helmet comes off). Each step and the benches' draw time are printed as SAP lines.</summary>
    public sealed class BenchScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "benches";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings with { Benches = true }, 1200);
            BenchDemo.Attach(world);
            return world;
        }
    }
}
