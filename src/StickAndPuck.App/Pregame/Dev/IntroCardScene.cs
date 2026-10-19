using StickAndPuck.App.Core;

namespace StickAndPuck.App.Pregame.Dev
{
    /// <summary>SAP_SCENE=introcard: the warm-up scene (arena flyover and both squads skating) with the A22 stats
    /// card art over it, filled by <see cref="IntroCardPreview"/>: the real ratings and tactics of SAP_HOME and
    /// SAP_AWAY with made-up record, place, form and top scorer numbers. Shows the art in game until the real
    /// card (W139) draws it.</summary>
    public sealed class IntroCardScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "introcard";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new WarmupScene().Create(clubs, settings, app);
            world.Switches.IntroCardPreview = true;
            return world;
        }
    }
}
