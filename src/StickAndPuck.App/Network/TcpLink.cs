using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.Versioning;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Network
{
    /// <summary>A TCP connection carrying length-prefixed messages, without ever blocking: sends that the
    /// socket cannot take yet wait in a queue, and receives take what has arrived. Nagle is off, so a
    /// step's small input message leaves at once. Desktop only; the browser has no sockets.</summary>
    [UnsupportedOSPlatform("browser")]
    internal sealed class TcpLink : INetLink
    {
        /// <summary>The largest message accepted; a longer length means the stream is not ours.</summary>
        private const int MaxMessage = 1 << 20;

        private readonly Socket socket;
        private readonly List<byte> outbox = [];
        private readonly List<byte> inbox = [];
        private readonly byte[] buffer = new byte[16 * 1024];
        private bool broken,
            disposed;

        /// <summary>Initializes a new instance of the <see cref="TcpLink"/> class on a connected socket.</summary>
        public TcpLink(Socket socket)
        {
            this.socket = socket;
            socket.Blocking = false;
            socket.NoDelay = true;
        }

        /// <inheritdoc/>
        public bool Open => !disposed && !broken && !ClosedByPeer;

        /// <inheritdoc/>
        public bool ClosedByPeer { get; private set; }

        /// <inheritdoc/>
        public void Send(byte[] message)
        {
            if (!Open)
            {
                return;
            }

            outbox.AddRange(NetMessage.Frame(message));
            Flush();
        }

        /// <inheritdoc/>
        public Maybe<byte[]> Receive()
        {
            if (inbox.Count < 4 && Open)
            {
                Flush();
                Fill();
            }

            if (inbox.Count < 4)
            {
                return Maybe.None<byte[]>();
            }

            Span<byte> head = stackalloc byte[4];
            for (int i = 0; i < 4; i++)
            {
                head[i] = inbox[i];
            }

            int length = BinaryPrimitives.ReadInt32LittleEndian(head);
            if (length is < 0 or > MaxMessage)
            {
                Console.WriteLine("SAP: network: the stream is not a game's, closing it");
                broken = true;
                inbox.Clear();
                return Maybe.None<byte[]>();
            }

            if (inbox.Count < 4 + length && Open)
            {
                Fill();
            }

            if (inbox.Count < 4 + length)
            {
                return Maybe.None<byte[]>();
            }

            byte[] message = [.. inbox.GetRange(4, length)];
            inbox.RemoveRange(0, 4 + length);
            return message;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (!broken && !ClosedByPeer)
            {
                Flush(); // the goodbye goes out before the socket closes
            }

            disposed = true;
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                // already gone
            }

            socket.Dispose();
        }

        private void Flush()
        {
            while (outbox.Count > 0 && !broken)
            {
                try
                {
                    int sent = socket.Send([.. outbox], SocketFlags.None, out SocketError error);
                    if (error == SocketError.WouldBlock)
                    {
                        return;
                    }

                    if (error != SocketError.Success)
                    {
                        Break(error.ToString());
                        return;
                    }

                    outbox.RemoveRange(0, sent);
                }
                catch (ObjectDisposedException)
                {
                    broken = true;
                }
            }
        }

        private void Fill()
        {
            while (!broken && !ClosedByPeer)
            {
                int read;
                try
                {
                    read = socket.Receive(
                        buffer,
                        0,
                        buffer.Length,
                        SocketFlags.None,
                        out SocketError error
                    );
                    if (error == SocketError.WouldBlock)
                    {
                        return;
                    }

                    if (error != SocketError.Success)
                    {
                        Break(error.ToString());
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    broken = true;
                    return;
                }

                if (read == 0)
                {
                    ClosedByPeer = true; // an orderly close: the other side quit
                    Console.WriteLine("SAP: network: the other side closed the connection");
                    return;
                }

                inbox.AddRange(buffer.AsSpan(0, read));
            }
        }

        private void Break(string why)
        {
            broken = true;
            Console.WriteLine($"SAP: network: connection lost ({why})");
        }
    }
}
