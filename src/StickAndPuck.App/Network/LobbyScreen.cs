using System.Runtime.Versioning;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The online versus lobby, from the new game list: host a match (pick both clubs, then wait for a
    /// player, <see cref="HostWaitScreen"/>) or join one (<see cref="JoinWaitScreen"/>) at the address and
    /// port typed here, and the input delay the host plays with. On the address and port rows the digit
    /// keys and the dot type and backspace erases.</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class LobbyScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 250;
        private const int AddressRow = 2,
            PortRow = 3;

        private NetLobby Lobby => App.Network.Lobby;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(Text.Get(K.NetHostGame), Host),
                new MenuItem(Text.Get(K.NetJoinGame), () => Flow.Push(new JoinWaitScreen(App))),
                new MenuItem(Text.Get(K.NetAddress), () => { }, Lobby.Address),
                new MenuItem(Text.Get(K.NetPort), () => { }, Lobby.PortText),
                new MenuItem(
                    Text.Get(K.NetDelay),
                    Text.Format(K.NetDelaySteps, Lobby.Delay),
                    d => Lobby.Delay = Math.Clamp(Lobby.Delay + d, 1, NetLobby.MaxDelay)
                ),
            ];
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            if (Selected is AddressRow or PortRow && Type(Selected == AddressRow))
            {
                return; // a typed character or an erase, not a menu press
            }

            base.Update(world, press);
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            (int x, int top) = art.Panel(
                Text.Get(K.OnlineVersus),
                PanelW,
                (items.Count * ScreenArt.Row) + 30,
                70
            );
            art.Menu(items, Selected, x, PanelW, top);
            art.Hint(Text.Get(K.HintLobby));
        }

        /// <summary>Opens the host's flow: their club, the opponent's club, then waiting for a player.</summary>
        private void Host()
        {
            Flow.Push(
                new TeamSelectScreen(
                    App,
                    Text.Get(K.NetPickHome),
                    Maybe.None<ClubData>(),
                    home =>
                        Flow.Push(
                            new TeamSelectScreen(
                                App,
                                Text.Get(K.NetPickAway),
                                home,
                                away => Flow.Push(new HostWaitScreen(App, home, away))
                            )
                        )
                )
            );
        }

        /// <summary>Types this step's digit keys into the row, or erases on backspace.</summary>
        /// <returns>True when a key was taken.</returns>
        private bool Type(bool address)
        {
            bool taken = false;
            foreach (int key in DeviceInput.Current.Pressed)
            {
                if (key == (int)KeyboardKey.Backspace)
                {
                    if (address)
                    {
                        Lobby.EraseAddress();
                    }
                    else
                    {
                        Lobby.ErasePort();
                    }

                    taken = true;
                }
                else if (Digit(key) is >= 0 and var digit)
                {
                    if (address)
                    {
                        Lobby.TypeAddress((char)('0' + digit));
                    }
                    else
                    {
                        Lobby.TypePort(digit);
                    }

                    taken = true;
                }
                else if (address && key is (int)KeyboardKey.Period or (int)KeyboardKey.KpDecimal)
                {
                    Lobby.TypeAddress('.');
                    taken = true;
                }
            }

            return taken;
        }

        private static int Digit(int key)
        {
            return key switch
            {
                >= (int)KeyboardKey.Zero and <= (int)KeyboardKey.Nine => key
                    - (int)KeyboardKey.Zero,
                >= (int)KeyboardKey.Kp0 and <= (int)KeyboardKey.Kp9 => key - (int)KeyboardKey.Kp0,
                _ => -1,
            };
        }
    }
}
