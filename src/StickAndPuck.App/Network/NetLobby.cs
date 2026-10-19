using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Network
{
    /// <summary>What the lobby is set to for this run: the host's address to join, the port and the input
    /// delay. SAP_NET_ADDRESS, SAP_NET_PORT and SAP_NET_DELAY set where they start (default 127.0.0.1,
    /// 7777 and 4 steps); the lobby screen changes them.</summary>
    internal sealed class NetLobby
    {
        /// <summary>The port used unless SAP_NET_PORT or the lobby picks another.</summary>
        public const int DefaultPort = 7777;

        /// <summary>The input delay used unless SAP_NET_DELAY or the lobby picks another: 4 steps, 67 ms.</summary>
        public const int DefaultDelay = 4;

        /// <summary>The longest input delay offered.</summary>
        public const int MaxDelay = 15;

        /// <summary>The longest address the lobby takes.</summary>
        public const int MaxAddress = 39;

        /// <summary>Gets or sets the host's address to join.</summary>
        public string Address { get; set; } =
            Env.Get("SAP_NET_ADDRESS") is { Length: > 0 } a ? a : "127.0.0.1";

        /// <summary>Gets or sets the TCP port the host listens on and the joiner connects to.</summary>
        public int Port { get; set; } = Number("SAP_NET_PORT", DefaultPort, 1, 65535);

        /// <summary>Gets or sets the input delay in steps the host plays with.</summary>
        public int Delay { get; set; } = Number("SAP_NET_DELAY", DefaultDelay, 1, MaxDelay);

        /// <summary>Opens the lobby over the current screen; the browser has no sockets, so nothing opens there.</summary>
        public static void Open(AppState app)
        {
            if (OperatingSystem.IsBrowser())
            {
                return;
            }

            app.Screens.Push(new LobbyScreen(app));
        }

        /// <summary>Gets the port as text.</summary>
        public string PortText => Port.ToString(CultureInfo.InvariantCulture);

        /// <summary>Types a character into the address: digits, dots and letters (a host name), at most <see cref="MaxAddress"/>.</summary>
        public void TypeAddress(char c)
        {
            if (
                Address.Length < MaxAddress
                && (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or ':')
            )
            {
                Address += char.ToLowerInvariant(c);
            }
        }

        /// <summary>Removes the address's last character.</summary>
        public void EraseAddress()
        {
            Address = Address.Length > 0 ? Address[..^1] : Address;
        }

        /// <summary>Types a digit into the port, keeping it a valid port.</summary>
        public void TypePort(int digit)
        {
            int next = (Port * 10) + digit;
            Port = next <= 65535 ? next : digit;
            Port = Math.Max(1, Port);
        }

        /// <summary>Removes the port's last digit.</summary>
        public void ErasePort()
        {
            Port = Math.Max(1, Port / 10);
        }

        private static int Number(string variable, int fallback, int min, int max)
        {
            return
                int.TryParse(
                    Env.Get(variable),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int n
                )
                && n >= min
                && n <= max
                ? n
                : fallback;
        }
    }
}
