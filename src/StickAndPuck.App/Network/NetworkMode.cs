using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>Online versus, the mode a network match plays in: one player on each machine, the host's club
    /// at home. It is not on the new game list as a mode of its own (the lobby starts it, see
    /// <see cref="LobbyScreen"/>), but its name in a replay rebuilds the match as it was played, and the
    /// results screen offers no next game.</summary>
    public sealed class NetworkMode : IGameMode
    {
        /// <inheritdoc/>
        public string Title => Text.Get(K.OnlineVersus);

        /// <inheritdoc/>
        public int Order => 11;

        /// <inheritdoc/>
        public bool Available(AppState app)
        {
            return false;
        }

        /// <inheritdoc/>
        public Maybe<MatchSetup> Setup(GameModeStart start)
        {
            return Maybe.None<MatchSetup>(); // the lobby builds the match with the other machine
        }
    }
}
