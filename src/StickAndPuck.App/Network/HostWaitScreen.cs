using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The host waits for a player: listens on the lobby's port and shows this machine's addresses
    /// and the two clubs. When a player of the same game version says hello, the host sends them the
    /// match and both start it. Back stops listening.</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class HostWaitScreen : Screen
    {
        private const int PanelW = 300;

        private readonly ClubData home,
            away;
        private readonly int port;
        private readonly string addresses;
        private Maybe<NetHost> host;
        private readonly Maybe<string> problem;

        /// <summary>Initializes a new instance of the <see cref="HostWaitScreen"/> class and starts listening.</summary>
        public HostWaitScreen(AppState app, ClubData home, ClubData away)
            : base(app)
        {
            this.home = home;
            this.away = away;
            port = app.Network.Lobby.Port;
            addresses = string.Join("  ", LocalAddresses().Take(2));
            try
            {
                host = NetHost.Listen(port);
            }
            catch (SocketException e)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: network: cannot listen on port {port} ({e.Message})"
                    )
                );
                problem = Text.Format(K.NetPortTaken, port);
            }
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            if (press.Contains(MenuButton.Back))
            {
                Close();
                Flow.Pop();
                return;
            }

            if (
                !host.TryGet(out NetHost listening)
                || !listening.Poll().TryGet(out (TcpLink Link, NetMessage.Hello Hello) joined)
            )
            {
                return;
            }

            (TcpLink link, NetMessage.Hello hello) = joined;
            if (
                hello.ProtocolVersion != NetMessage.Protocol
                || !string.Equals(hello.Version, NetPlay.Version, StringComparison.Ordinal)
            )
            {
                Console.WriteLine(
                    $"SAP: network: refused a player on version {hello.Version} (this is {NetPlay.Version})"
                );
                link.Send(new NetMessage.Refuse(K.NetVersionDiffers).Write());
                link.Dispose();
                return;
            }

            int delay = App.Network.Lobby.Delay;
            ReplayHeader header = NetMatch.Host(App, home, away, hello.Human);
            link.Send(new NetMessage.Welcome(delay, NetMatch.Write(header)).Write());
            Close();
            World match = NetMatch.Start(App, header);
            App.Network.Begin(match, new LockstepSession(link, Side.Home, delay));
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            (int x, int top) = art.Panel(
                Text.Format(K.NetHosting, port),
                PanelW,
                30 + (5 * ScreenArt.Row),
                70
            );
            int cx = x + (PanelW / 2);
            if (problem.TryGet(out string text))
            {
                art.Centred(text, cx, top, art.Gold);
            }
            else
            {
                art.Centred(Text.Get(K.NetWaitingForPlayer), cx, top, art.Text);
                art.Centred(
                    Text.Format(K.NetYourAddress, addresses),
                    cx,
                    top + ScreenArt.Row,
                    ScreenArt.Muted
                );
            }

            art.Centred(
                Text.Format(K.NetHomeClub, home.Name),
                cx,
                top + (3 * ScreenArt.Row),
                art.Text
            );
            art.Centred(
                Text.Format(K.NetAwayClub, away.Name),
                cx,
                top + (4 * ScreenArt.Row),
                art.Text
            );
            art.Hint(Text.Get(K.HintNetWait));
        }

        private void Close()
        {
            if (host.TryGet(out NetHost listening))
            {
                listening.Dispose();
                host = Maybe.None<NetHost>();
            }
        }

        /// <summary>Gets this machine's IPv4 addresses on the networks it is up on, the loopback address when there are none.</summary>
        private static List<string> LocalAddresses()
        {
            List<string> found = [];
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (
                        nic.OperationalStatus != OperationalStatus.Up
                        || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    )
                    {
                        continue;
                    }

                    found.AddRange(
                        nic.GetIPProperties()
                            .UnicastAddresses.Where(a =>
                                a.Address.AddressFamily == AddressFamily.InterNetwork
                            )
                            .Select(a => a.Address.ToString())
                    );
                }
            }
            catch (NetworkInformationException e)
            {
                Console.WriteLine($"SAP: network: no address list ({e.Message})");
            }

            return found.Count > 0 ? found : ["127.0.0.1"];
        }
    }
}
