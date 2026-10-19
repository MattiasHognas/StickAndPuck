using StickAndPuck.App.Core;

namespace StickAndPuck.App.Pregame.Dev
{
    /// <summary>SAP_SCENE=introcard: the warm-up scene (arena flyover and both squads skating) with the W139 stats
    /// card (<see cref="TeamCard"/>) held on screen through the whole warm-up instead of sliding out with the
    /// flyover, for looking at the card. An exhibition, so the season rows show a dash.</summary>
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
