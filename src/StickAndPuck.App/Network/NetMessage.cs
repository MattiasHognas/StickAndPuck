using System.Buffers.Binary;
using System.Text;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Network
{
    /// <summary>One message between the two machines of a network match. The joining side says
    /// <see cref="Hello"/>; the host answers with the match (<see cref="Welcome"/>) or a reason it cannot
    /// play (<see cref="Refuse"/>). During the match both sides send each step's input
    /// (<see cref="StepInput"/>), every few steps their world's checksum (<see cref="Check"/>), and
    /// <see cref="Bye"/> when they leave.</summary>
    internal abstract record NetMessage
    {
        /// <summary>The protocol version; both sides must speak the same one.</summary>
        public const int Protocol = 1;

        private enum Kind : byte
        {
            Hello = 1,
            Welcome = 2,
            Refuse = 3,
            StepInput = 4,
            Check = 5,
            Bye = 6,
        }

        /// <summary>The joining side's greeting.</summary>
        /// <param name="ProtocolVersion">The joiner's <see cref="Protocol"/>.</param>
        /// <param name="Version">The joiner's game version; the host refuses another one, as its match would play differently.</param>
        /// <param name="Human">Whether a human plays on the joining side (SAP_HUMAN=0 hands the visitors to the AI).</param>
        public sealed record Hello(int ProtocolVersion, string Version, bool Human) : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.Hello);
                w.Write(ProtocolVersion);
                w.Write(Version);
                w.Write(Human);
            }
        }

        /// <summary>The host's answer: the match to play.</summary>
        /// <param name="Delay">The input delay in steps: input sampled on a step plays that many steps later.</param>
        /// <param name="Header">The match as a replay header stores it (seed, setup, mode, settings, club hash), as JSON.</param>
        public sealed record Welcome(int Delay, string Header) : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.Welcome);
                w.Write(Delay);
                w.Write(Header);
            }
        }

        /// <summary>The host cannot play with this joiner.</summary>
        /// <param name="Reason">The string key of the reason.</param>
        public sealed record Refuse(string Reason) : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.Refuse);
                w.Write(Reason);
            }
        }

        /// <summary>One side's input for one step.</summary>
        /// <param name="Tick">The step it plays on.</param>
        /// <param name="Pad">The input as one gamepad (<see cref="NetPad"/>).</param>
        public sealed record StepInput(long Tick, PadFrame Pad) : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.StepInput);
                w.Write(Tick);
                w.Write(Pad.Down);
                w.Write(Pad.Pressed);
                w.Write(NetPad.AxisBits(Pad, 0));
                w.Write(NetPad.AxisBits(Pad, 1));
            }
        }

        /// <summary>One side's world checksum after a step.</summary>
        /// <param name="Tick">The step.</param>
        /// <param name="Sum">The <see cref="Replay.WorldChecksum"/> after it.</param>
        public sealed record Check(long Tick, ulong Sum) : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.Check);
                w.Write(Tick);
                w.Write(Sum);
            }
        }

        /// <summary>The side is leaving the match.</summary>
        public sealed record Bye : NetMessage
        {
            /// <inheritdoc/>
            protected override void WriteBody(BinaryWriter w)
            {
                w.Write((byte)Kind.Bye);
            }
        }

        /// <summary>Writes the message as bytes.</summary>
        public byte[] Write()
        {
            using MemoryStream stream = new();
            using BinaryWriter w = new(stream, Encoding.UTF8);
            WriteBody(w);
            w.Flush();
            return stream.ToArray();
        }

        /// <summary>Writes the message's kind and fields.</summary>
        protected abstract void WriteBody(BinaryWriter w);

        /// <summary>Reads a message <see cref="Write"/> wrote, or None for bytes that are not one.</summary>
        public static Maybe<NetMessage> Read(byte[] bytes)
        {
            try
            {
                using MemoryStream stream = new(bytes);
                using BinaryReader r = new(stream, Encoding.UTF8);
                Kind kind = (Kind)r.ReadByte();
                NetMessage message = kind switch
                {
                    Kind.Hello => new Hello(r.ReadInt32(), r.ReadString(), r.ReadBoolean()),
                    Kind.Welcome => new Welcome(r.ReadInt32(), r.ReadString()),
                    Kind.Refuse => new Refuse(r.ReadString()),
                    Kind.StepInput => ReadInput(r),
                    Kind.Check => new Check(r.ReadInt64(), r.ReadUInt64()),
                    Kind.Bye => new Bye(),
                    _ => throw new InvalidDataException($"unknown network message {kind}"),
                };
                return stream.Position == stream.Length
                    ? message
                    : throw new InvalidDataException("bytes after the network message");
            }
            catch (Exception e) when (e is EndOfStreamException or InvalidDataException)
            {
                Console.WriteLine($"SAP: network: dropped a bad message ({e.Message})");
                return Maybe.None<NetMessage>();
            }
        }

        private static StepInput ReadInput(BinaryReader r)
        {
            long tick = r.ReadInt64();
            uint down = r.ReadUInt32(),
                pressed = r.ReadUInt32();
            short x = r.ReadInt16(),
                y = r.ReadInt16();
            return new StepInput(tick, NetPad.Make(down, pressed, x, y));
        }

        /// <summary>Puts the length in front of a message, as the stream carries it.</summary>
        public static byte[] Frame(byte[] message)
        {
            byte[] framed = new byte[message.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(framed, message.Length);
            message.CopyTo(framed, 4);
            return framed;
        }
    }
}
