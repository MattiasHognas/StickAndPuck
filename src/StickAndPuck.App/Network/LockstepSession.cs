using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;

namespace StickAndPuck.App.Network
{
    /// <summary>Lockstep over a link: both machines run the same deterministic match, and a step runs only once
    /// both players' input for it is in. Input sampled on step t plays on step t + <see cref="Delay"/>, so it
    /// has that long to cross the network before anyone waits; the first <see cref="Delay"/> steps play
    /// with nobody pressing anything. Every <see cref="CheckEvery"/> steps both sides send their world's
    /// checksum; the first step where they differ is a desync. Knows nothing of worlds or screens:
    /// <see cref="NetPlay"/> runs it in the game, the tests run two of them over loopback.</summary>
    internal sealed class LockstepSession : IDisposable
    {
        /// <summary>Steps between checksum exchanges.</summary>
        public const int CheckEvery = 30;

        private readonly INetLink link;
        private readonly Dictionary<long, PadFrame>[] inputs =
        [
            [],
            [],
        ];
        private readonly Dictionary<long, ulong> mine = [],
            theirs = [];
        private bool byeSent;

        /// <summary>Initializes a new instance of the <see cref="LockstepSession"/> class.</summary>
        /// <param name="link">The connection to the other machine.</param>
        /// <param name="local">The club this machine's player plays: the host plays home, the joiner away.</param>
        /// <param name="delay">The input delay in steps, at least one.</param>
        public LockstepSession(INetLink link, Side local, int delay)
        {
            this.link = link;
            Local = local;
            Delay = Math.Max(1, delay);
            for (long tick = 0; tick < Delay; tick++)
            {
                inputs[0][tick] = NetPad.Idle;
                inputs[1][tick] = NetPad.Idle;
            }

            NextSample = Delay;
        }

        /// <summary>Gets the club this machine's player plays.</summary>
        public Side Local { get; }

        /// <summary>Gets the input delay in steps.</summary>
        public int Delay { get; }

        /// <summary>Gets the step the next local input is for.</summary>
        public long NextSample { get; private set; }

        /// <summary>Gets a value indicating whether the other side said it was leaving.</summary>
        public bool PeerLeft { get; private set; }

        /// <summary>Gets a value indicating whether the connection is gone (closed or broken).</summary>
        public bool Disconnected => !link.Open;

        /// <summary>Gets the first checked step where the two worlds differed, or None while they agree.</summary>
        public Maybe<long> Desync { get; private set; }

        /// <summary>Gets the number of checksums compared so far.</summary>
        public int Compared { get; private set; }

        /// <summary>Takes every message that has arrived: the other side's inputs and checksums, or its goodbye.</summary>
        public void Pump()
        {
            while (link.Receive().TryGet(out byte[] bytes))
            {
                if (!NetMessage.Read(bytes).TryGet(out NetMessage message))
                {
                    continue;
                }

                switch (message)
                {
                    case NetMessage.StepInput input:
                        inputs[Remote][input.Tick] = input.Pad;
                        break;
                    case NetMessage.Check check:
                        theirs[check.Tick] = check.Sum;
                        Compare(check.Tick);
                        break;
                    case NetMessage.Bye:
                        PeerLeft = true;
                        break;
                    default:
                        break; // the lobby's messages arrive only before the match
                }
            }

            if (link.ClosedByPeer)
            {
                PeerLeft = true;
            }
        }

        /// <summary>Gives the local player's input for step <see cref="NextSample"/> and sends it.</summary>
        public void Submit(PadFrame pad)
        {
            inputs[LocalIndex][NextSample] = pad;
            link.Send(new NetMessage.StepInput(NextSample, pad).Write());
            LastSent = pad;
            NextSample++;
        }

        /// <summary>Gets the pad sent last, for finding the next one's presses.</summary>
        public PadFrame LastSent { get; private set; } = NetPad.Idle;

        /// <summary>Gets a value indicating whether both players' input for a step is in.</summary>
        public bool Ready(long tick)
        {
            return inputs[0].ContainsKey(tick) && inputs[1].ContainsKey(tick);
        }

        /// <summary>Gets the frame both machines play on a step that is <see cref="Ready"/>, and forgets older steps' input.</summary>
        public InputFrame Frame(long tick)
        {
            InputFrame frame = NetPad.Frame(inputs[0][tick], inputs[1][tick]);
            _ = inputs[0].Remove(tick - 1);
            _ = inputs[1].Remove(tick - 1);
            return frame;
        }

        /// <summary>Notes the world's checksum after a step and, on a check step, sends it and compares it with the other side's.</summary>
        public void AfterStep(long tick, ulong sum)
        {
            if (tick % CheckEvery != 0)
            {
                return;
            }

            mine[tick] = sum;
            link.Send(new NetMessage.Check(tick, sum).Write());
            Compare(tick);
        }

        /// <summary>Says goodbye to the other side and closes the connection.</summary>
        public void Leave()
        {
            if (!byeSent && link.Open)
            {
                byeSent = true;
                link.Send(new NetMessage.Bye().Write());
            }

            link.Dispose();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Leave();
        }

        private int LocalIndex => Local == Side.Home ? 0 : 1;

        private int Remote => 1 - LocalIndex;

        private void Compare(long tick)
        {
            if (!mine.TryGetValue(tick, out ulong a) || !theirs.TryGetValue(tick, out ulong b))
            {
                return;
            }

            _ = mine.Remove(tick);
            _ = theirs.Remove(tick);
            Compared++;
            if (a == b || (Desync.TryGet(out long first) && first <= tick))
            {
                return;
            }

            Desync = tick;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: network: desync at step {tick}: {WorldChecksum.Text(a)} here, {WorldChecksum.Text(b)} on the other side"
                )
            );
        }
    }
}
