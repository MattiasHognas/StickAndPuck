using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Network.Dev
{
    /// <summary>SAP_SCENE=netlobby: the start screen flow opened on the online versus lobby.</summary>
    public sealed class NetLobbyScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "netlobby";

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
            Console.WriteLine("SAP: network: lobby");
            return world;
        }
    }
}
