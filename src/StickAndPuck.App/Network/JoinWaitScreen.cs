using System.Runtime.Versioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The joiner connects to the lobby's address and port, says hello and waits for the host's match;
    /// when it comes, both start it. A failed join says why; enter tries again, back leaves.</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class JoinWaitScreen : Screen
    {
        private const int PanelW = 300;

        private readonly string target;
        private NetJoin join;
        private Maybe<string> Problem { get; set; }

        /// <summary>Initializes a new instance of the <see cref="JoinWaitScreen"/> class and starts connecting.</summary>
        public JoinWaitScreen(AppState app)
            : base(app)
        {
            NetLobby lobby = app.Network.Lobby;
            target = $"{lobby.Address}:{lobby.PortText}";
            join = Connect();
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            if (press.Contains(MenuButton.Back))
            {
                join.Dispose();
                Flow.Pop();
                return;
            }

            if (Failure.HasValue)
            {
                if (press.Contains(MenuButton.Accept))
                {
                    join.Dispose();
                    Problem = Maybe.None<string>();
                    join = Connect();
                }

                return;
            }

            if (!join.Poll().TryGet(out NetMessage.Welcome welcome))
            {
                return;
            }

            if (
                !NetMatch
                    .Read(welcome.Header, Flow.Clubs, out string why)
                    .TryGet(out ReplayHeader header)
            )
            {
                Problem = why;
                join.Dispose();
                return;
            }

            TcpLink link = join.TakeLink();
            World match = NetMatch.Start(App, header);
            App.Network.Begin(match, new LockstepSession(link, Side.Away, welcome.Delay));
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            (int x, int top) = art.Panel(
                Text.Format(K.NetJoining, target),
                PanelW,
                16 + (2 * ScreenArt.Row),
                90
            );
            int cx = x + (PanelW / 2);
            if (Failure.TryGet(out string key))
            {
                art.Centred(Text.Get(key), cx, top, art.Gold);
                art.Hint(Text.Get(K.HintNetRetry));
                return;
            }

            art.Centred(
                Text.Get(join.Connected ? K.NetWaitingForHost : K.NetConnecting),
                cx,
                top,
                art.Text
            );
            art.Hint(Text.Get(K.HintNetWait));
        }

        /// <summary>Gets why joining failed, as a string key, or None while it may still work.</summary>
        private Maybe<string> Failure => Problem.HasValue ? Problem : join.Failed;

        private NetJoin Connect()
        {
            NetLobby lobby = App.Network.Lobby;
            return NetJoin.Connect(
                lobby.Address,
                lobby.Port,
                new NetMessage.Hello(
                    NetMessage.Protocol,
                    NetPlay.Version,
                    !Env.Is("SAP_HUMAN", "0")
                )
            );
        }
    }
}
