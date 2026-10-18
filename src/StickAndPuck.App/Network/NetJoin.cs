using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The joining side of the lobby: connects to a host, says hello and waits for the match. Polled
    /// once a step by the joiner's waiting screen; never blocks (a host name is looked up once).</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class NetJoin : IDisposable
    {
        /// <summary>Seconds to wait for the host to answer before giving up.</summary>
        private const double Timeout = 10;

        private readonly Socket socket;
        private readonly Task connecting;
        private readonly NetMessage.Hello hello;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private Maybe<TcpLink> link;
        private bool linkTaken;

        private NetJoin(Socket socket, Task connecting, NetMessage.Hello hello)
        {
            this.socket = socket;
            this.connecting = connecting;
            this.hello = hello;
        }

        /// <summary>Gets a value indicating whether the connection is made and the hello sent.</summary>
        public bool Connected => link.HasValue;

        /// <summary>Gets why joining failed (a string key), or None while it may still work.</summary>
        public Maybe<string> Failed { get; private set; }

        /// <summary>Starts connecting to a host.</summary>
        public static NetJoin Connect(string address, int port, NetMessage.Hello hello)
        {
            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Task connect;
            try
            {
                connect = client.ConnectAsync(new IPEndPoint(Resolve(address), port));
            }
            catch (Exception e) when (e is SocketException or ArgumentException)
            {
                connect = Task.FromException(e);
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: network: joining {address}:{port}"
                )
            );
            return new NetJoin(client, connect, hello);
        }

        /// <summary>Gets the host's answer once it came, or None while waiting (or after <see cref="Failed"/>).</summary>
        public Maybe<NetMessage.Welcome> Poll()
        {
            if (Failed.HasValue || linkTaken)
            {
                return Maybe.None<NetMessage.Welcome>();
            }

            if (!link.TryGet(out TcpLink connected))
            {
                if (!connecting.IsCompleted)
                {
                    TimeOut();
                    return Maybe.None<NetMessage.Welcome>();
                }

                if (connecting.IsFaulted || connecting.IsCanceled)
                {
                    Console.WriteLine(
                        $"SAP: network: could not connect ({connecting.Exception?.GetBaseException().Message})"
                    );
                    Failed = K.NetCouldNotConnect;
                    return Maybe.None<NetMessage.Welcome>();
                }

                connected = new TcpLink(socket);
                link = connected;
                connected.Send(hello.Write());
                Console.WriteLine("SAP: network: connected, waiting for the host");
            }

            while (connected.Receive().TryGet(out byte[] bytes))
            {
                if (!NetMessage.Read(bytes).TryGet(out NetMessage message))
                {
                    continue;
                }

                if (message is NetMessage.Welcome welcome)
                {
                    return welcome;
                }

                if (message is NetMessage.Refuse refuse)
                {
                    Console.WriteLine($"SAP: network: the host refused ({refuse.Reason})");
                    Failed = refuse.Reason;
                    return Maybe.None<NetMessage.Welcome>();
                }
            }

            if (!connected.Open)
            {
                Failed = K.NetConnectionLost;
                return Maybe.None<NetMessage.Welcome>();
            }

            TimeOut();
            return Maybe.None<NetMessage.Welcome>();
        }

        /// <summary>Hands the connection to the match; this lobby no longer closes it.</summary>
        public TcpLink TakeLink()
        {
            linkTaken = true;
            return link.TryGet(out TcpLink l)
                ? l
                : throw new InvalidOperationException("the join has no connection yet");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (linkTaken)
            {
                return;
            }

            if (link.TryGet(out TcpLink l))
            {
                l.Dispose();
            }
            else
            {
                socket.Dispose();
            }

            if (connecting.IsCompleted)
            {
                connecting.Dispose();
            }
        }

        private void TimeOut()
        {
            if (clock.Elapsed.TotalSeconds > Timeout)
            {
                Console.WriteLine("SAP: network: the host did not answer");
                Failed = link.HasValue ? K.NetNoAnswer : K.NetCouldNotConnect;
            }
        }

        private static IPAddress Resolve(string address)
        {
            return IPAddress.TryParse(address, out IPAddress ip) ? ip
                : string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
                    ? IPAddress.Loopback
                : Dns.GetHostAddresses(address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                    ?? throw new ArgumentException(
                        $"no IPv4 address for {address}",
                        nameof(address)
                    );
        }
    }
}
