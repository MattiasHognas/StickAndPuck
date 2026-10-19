using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Network
{
    /// <summary>The hosting side of the lobby: listens on a TCP port for one player to join and hands back their
    /// connection once they said hello. Polled once a step by the host's waiting screen; never blocks.</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class NetHost : IDisposable
    {
        private readonly TcpListener listener;
        private Maybe<TcpLink> pending;

        private NetHost(TcpListener listener)
        {
            this.listener = listener;
        }

        /// <summary>Gets the port listened on (the one asked for, or the one picked for port 0).</summary>
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        /// <summary>Starts listening on every address of this machine.</summary>
        /// <exception cref="SocketException">The port is taken or not allowed.</exception>
        public static NetHost Listen(int port)
        {
            TcpListener listening = new(IPAddress.Any, port);
            listening.Start(1);
            NetHost host = new(listening);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: network: hosting on port {host.Port}"
                )
            );
            return host;
        }

        /// <summary>Takes a player who connected and said hello, or None while nobody has.</summary>
        public Maybe<(TcpLink Link, NetMessage.Hello Hello)> Poll()
        {
            if (!pending.HasValue && listener.Pending())
            {
                Socket socket = listener.AcceptSocket();
                Console.WriteLine($"SAP: network: {socket.RemoteEndPoint} connected");
                pending = new TcpLink(socket);
            }

            if (!pending.TryGet(out TcpLink link))
            {
                return Maybe.None<(TcpLink, NetMessage.Hello)>();
            }

            while (link.Receive().TryGet(out byte[] bytes))
            {
                if (
                    NetMessage.Read(bytes).TryGet(out NetMessage message)
                    && message is NetMessage.Hello hello
                )
                {
                    pending = Maybe.None<TcpLink>();
                    return (link, hello);
                }
            }

            if (!link.Open)
            {
                link.Dispose(); // they left before saying hello: wait for the next one
                pending = Maybe.None<TcpLink>();
            }

            return Maybe.None<(TcpLink, NetMessage.Hello)>();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (pending.TryGet(out TcpLink link))
            {
                link.Dispose();
                pending = Maybe.None<TcpLink>();
            }

            listener.Stop();
            listener.Dispose();
        }
    }
}
