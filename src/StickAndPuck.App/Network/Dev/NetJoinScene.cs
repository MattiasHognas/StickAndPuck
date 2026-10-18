using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Network.Dev
{
    /// <summary>SAP_SCENE=netjoin: the lobby's join flow, connecting to SAP_NET_ADDRESS (default 127.0.0.1) on
    /// SAP_NET_PORT (default 7777); the match starts when the host answers.</summary>
    public sealed class NetJoinScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "netjoin";

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
            app.Screens.Push(new JoinWaitScreen(app));
            return world;
        }
    }
}
