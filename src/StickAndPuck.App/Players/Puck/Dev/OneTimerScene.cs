using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>SAP_SCENE=onetimer: a home passer feeds a teammate in the slot who presses shoot right after
    /// the pass, holds the wind-up while the puck travels and one-times it on arrival. Every second round an
    /// away skater stands in the lane and intercepts, so the wind-up drops. The camera sits close on the
    /// play, and the script loops with a pause.</summary>
    public sealed class OneTimerScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "onetimer";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings with { OneTimers = true });
            OneTimerDemo.Attach(world);
            return world;
        }
    }
}
