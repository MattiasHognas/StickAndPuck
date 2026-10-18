using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Checks.Dev
{
    /// <summary>Test setup, SAP_SCENE=checks: an away carrier skates loops around centre ice with the puck
    /// while you, on a home skater, hunt them down. Arrows skate, shift sprints, Q (or gamepad B)
    /// checks, C (or gamepad Y) switches to the teammate nearest the puck. Knock the carrier off the
    /// puck and pick it up; control follows pickups and goes to the nearest skater on defence.</summary>
    public sealed class ChecksScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "checks";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            ChecksDemo.Setup(world, script: false);
            return world;
        }
    }
}
