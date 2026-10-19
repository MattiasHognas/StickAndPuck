using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Network.Dev
{
    /// <summary>SAP_SCENE=nethost: the lobby's host flow with SAP_HOME and SAP_AWAY picked, listening on
    /// SAP_NET_PORT (default 7777) for a player to join; the match starts when one does.</summary>
    public sealed class NetHostScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "nethost";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("network play needs the desktop game");
            }

            World world = app.Screens.Begin(clubs, new StartScreen(app));
            app.Screens.Push(new ModeScreen(app));
            app.Screens.Push(new LobbyScreen(app));
            MatchSetup picked = Scenes.DefaultSetup(clubs);
            app.Screens.Push(new HostWaitScreen(app, picked.Home, picked.Away));
            return world;
        }
    }
}
