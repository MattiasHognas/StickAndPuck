using StickAndPuck.App.Core;

namespace StickAndPuck.App.Network
{
    /// <summary>A connection to the other machine that carries whole messages in order (<see cref="TcpLink"/>
    /// over the network). Every call returns at once: the game loop polls it, it never waits.</summary>
    internal interface INetLink : IDisposable
    {
        /// <summary>Gets a value indicating whether the connection is still up.</summary>
        bool Open { get; }

        /// <summary>Gets a value indicating whether the other side closed the connection itself, rather than it breaking.</summary>
        bool ClosedByPeer { get; }

        /// <summary>Queues a message and sends what the connection takes now.</summary>
        void Send(byte[] message);

        /// <summary>Takes the next whole message that has arrived, or None.</summary>
        Maybe<byte[]> Receive();
    }
}
