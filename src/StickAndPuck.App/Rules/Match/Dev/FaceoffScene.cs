using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=faceoff: faceoffs at centre with the human on the home left wing, moved onto the centre for each
    /// draw; the scene presses shoot 0.15 s after each drop (a timed win), then whistles the next faceoff.</summary>
    public sealed class FaceoffScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "faceoff";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 20 * 60);
            if (
                world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.LW) is
                { } wing
            )
            {
                wing.Slot = 0;
            }

            world.Dev.Match.Set(new MatchDemo { PressAfter = 0.15f });
            return world;
        }
    }
}
