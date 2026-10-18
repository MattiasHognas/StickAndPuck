using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Benches.Dev
{
    /// <summary>SAP_SCENE=penaltybox: an all-AI exhibition with the camera lowered on the penalty boxes, as
    /// the follow camera frames them with play at the near boards. SAP_BOX picks the script: "in" (the
    /// default) blows the whistle a moment in and gives the home skater farthest from his box a minor,
    /// and he skates there and sits; "two" also sends a second home skater and the away skater farthest
    /// from his box; "out" puts a home skater in the box in play with a few seconds left, and he comes
    /// out of the door. The benches' and boxes' draw time is printed as SAP lines.</summary>
    public sealed class BoxScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "penaltybox";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings with { Benches = true }, 1200);
            BoxDemo.Attach(world, Env.Get("SAP_BOX") is { Length: > 0 } mode ? mode : "in");
            return world;
        }
    }
}
