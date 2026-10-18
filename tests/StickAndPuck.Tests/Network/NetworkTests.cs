using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Network;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Input;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Players.Switching;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Versus;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Network
{
    /// <summary>
    /// Online versus. Two lockstep peers talk over a real TCP connection on the loopback address: the
    /// joiner says hello, the host answers with the match, and both play it step by step from each
    /// other's scripted input, checking that their world checksums agree at every step; a world nudged on
    /// one side only is caught as a desync by both. The wire format and the network pad are checked on
    /// their own. The peers drive the human slots from the network pads directly rather than through
    /// <see cref="DeviceInput"/>, which is shared by every test running at the same time.
    /// </summary>
    public sealed class NetworkTests : CheckedTest
    {
        private const int Steps = 600;

        [Test]
        public void MessagesRoundTrip()
        {
            NetMessage[] messages =
            [
                new NetMessage.Hello(NetMessage.Protocol, "1.2.3", Human: true),
                new NetMessage.Welcome(4, "header text"),
                new NetMessage.Refuse("NetVersionDiffers"),
                new NetMessage.Check(1234, 0xfedcba9876543210UL),
                new NetMessage.Bye(),
            ];
            foreach (NetMessage m in messages)
            {
                Check(
                    NetMessage.Read(m.Write()).Is(r => r.Equals(m)),
                    $"{m.GetType().Name} reads back as written"
                );
            }

            PadFrame pad = NetPad.Make(0x30, 0x10, 2048, -4096);
            Check(
                NetMessage
                    .Read(new NetMessage.StepInput(77, pad).Write())
                    .Is(r =>
                        r is NetMessage.StepInput i
                        && i.Tick == 77
                        && i.Pad.Down == 0x30
                        && i.Pad.Pressed == 0x10
                        && Math.Abs(i.Pad.Axes[0] - 0.5f) < 1e-6f
                        && Math.Abs(i.Pad.Axes[1] + 1f) < 1e-6f
                    ),
                "a step's input reads back with its step, buttons and stick"
            );
            Check(!NetMessage.Read([99, 1, 2]).HasValue, "bytes that are no message are dropped");
        }

        [Test]
        public void PadCarriesTheDevice()
        {
            DeviceState state = new()
            {
                Move = Vector2.Normalize(new Vector2(1, -1)) * 0.6f,
                Shoot = true,
                Pass = true,
            };
            PadFrame first = NetPad.Encode(state, pull: true, accept: false, NetPad.Idle);
            Vector2 read = InputDevices.Stick(first.Axes[0], first.Axes[1]);
            Check(
                Vector2.Distance(read, state.Move) < 0.002f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the stick reads back through the dead zone as it was sent ({read} for {state.Move})"
                )
            );
            uint shoot = 1u << (int)KeyBindings.Defaults[GameAction.Shoot].Button,
                pass = 1u << (int)KeyBindings.Defaults[GameAction.Pass].Button,
                back = 1u << (int)GamepadButton.MiddleLeft;
            Check(
                first.Down == (shoot | pass | back) && first.Pressed == first.Down,
                "shoot and pass go on their default pad buttons, the goalie pull on back, all pressed on the first step"
            );
            PadFrame second = NetPad.Encode(state, pull: false, accept: false, first);
            Check(
                second.Pressed == 0 && second.Down == (shoot | pass),
                "held buttons are not pressed again on the next step"
            );
        }

        [Test]
        public void SessionWaitsForTheOtherSide()
        {
            (LoopLink a, LoopLink b) = LoopLink.Pair();
            using LockstepSession home = new(a, Side.Home, 3);
            using LockstepSession away = new(b, Side.Away, 3);
            Check(home.Ready(0) && home.Ready(2), "the first delay steps play with idle pads");
            home.Submit(NetPad.Idle);
            Check(!home.Ready(3), "step 3 waits for the visitors' input");
            away.Submit(NetPad.Make(4, 4, 0, 0));
            home.Pump();
            Check(
                home.Ready(3) && home.Frame(3).Pads[1].Down == 4,
                "once it arrives, step 3 plays the visitors' pad second"
            );
            away.Leave();
            home.Pump();
            Check(home.PeerLeft, "a goodbye is noticed");
        }

        [Test]
        public void LoopbackPeersStayInStep()
        {
            (Peer host, Peer joiner) = Connect();
            Run(host, joiner, Steps, divergeAt: -1);
            int same = host.Sums.Zip(joiner.Sums).TakeWhile(p => p.First == p.Second).Count();
            Check(
                host.Sums.Count >= Steps && joiner.Sums.Count >= Steps && same >= Steps,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"both peers ran {Steps} lockstep steps over loopback and agree on the checksum at every one ({same}, {host.Sums.Count} and {joiner.Sums.Count} run)"
                )
            );
            Check(
                !host.Session.Desync.HasValue && !joiner.Session.Desync.HasValue,
                "no desync is reported"
            );
            Check(
                host.Session.Compared >= (Steps / LockstepSession.CheckEvery) - 2
                    && joiner.Session.Compared >= (Steps / LockstepSession.CheckEvery) - 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the sides exchanged and compared their checksums ({host.Session.Compared} and {joiner.Session.Compared})"
                )
            );
            Check(
                host.Moved && joiner.Moved,
                "each side's scripted player skated its own controlled skater"
            );
            Check(
                string.Equals(
                    host.World.Setup.Home.Id,
                    joiner.World.Setup.Home.Id,
                    StringComparison.Ordinal
                )
                    && host.World.Seed == joiner.World.Seed,
                "the joiner plays the host's clubs and seed"
            );
            host.Session.Dispose();
            joiner.Session.Dispose();
        }

        [Test]
        public void DivergenceIsCaught()
        {
            const int At = 200;
            (Peer host, Peer joiner) = Connect();
            Run(host, joiner, Steps, divergeAt: At);
            bool hostCaught = host.Session.Desync.TryGet(out long hostAt);
            bool joinerCaught = joiner.Session.Desync.TryGet(out long joinerAt);
            bool caught = hostCaught && joinerCaught;
            Check(
                caught
                    && hostAt == joinerAt
                    && hostAt >= At
                    && hostAt <= At + LockstepSession.CheckEvery,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"skaters nudged on the joiner's side at step {At} is caught by both as a desync at the next check ({(caught ? hostAt : -1)}, {(caught ? joinerAt : -1)})"
                )
            );
            host.Session.Dispose();
            joiner.Session.Dispose();
        }

        /// <summary>Connects a host and a joiner over loopback and builds both worlds from the host's match.</summary>
        private (Peer Host, Peer Joiner) Connect()
        {
            IReadOnlyList<ClubData> clubs = Clubs.LoadAll();
            AppState hostApp = new();
            using NetHost listening = NetHost.Listen(0);
            using NetJoin join = NetJoin.Connect(
                "127.0.0.1",
                listening.Port,
                new NetMessage.Hello(NetMessage.Protocol, NetPlay.Version, Human: true)
            );
            Stopwatch clock = Stopwatch.StartNew();
            Maybe<LockstepSession> hostSession = Maybe.None<LockstepSession>();
            Maybe<ReplayHeader> sent = Maybe.None<ReplayHeader>();
            while (clock.Elapsed.TotalSeconds < 10)
            {
                if (
                    !hostSession.HasValue
                    && listening.Poll().TryGet(out (TcpLink Link, NetMessage.Hello Hello) joined)
                )
                {
                    ReplayHeader header = NetMatch.Host(
                        hostApp,
                        clubs[2],
                        clubs[5],
                        joined.Hello.Human
                    );
                    joined.Link.Send(new NetMessage.Welcome(3, NetMatch.Write(header)).Write());
                    hostSession = new LockstepSession(joined.Link, Side.Home, 3);
                    sent = header;
                }

                if (join.Poll().TryGet(out NetMessage.Welcome welcome))
                {
                    Check(
                        NetMatch
                            .Read(welcome.Header, clubs, out string problem)
                            .TryGet(out ReplayHeader received),
                        $"the joiner reads the host's match ({problem})"
                    );
                    Check(
                        sent.Is(h => h.Seed == received.Seed) && welcome.Delay == 3,
                        "the host's seed and input delay arrive"
                    );
                    LockstepSession joinSession = new(join.TakeLink(), Side.Away, welcome.Delay);
                    ReplayHeader hostHeader = sent.Or(received);
                    return (
                        new Peer(Build(clubs, hostHeader), hostSession.Or(joinSession), Script(1)),
                        new Peer(Build(clubs, received), joinSession, Script(2))
                    );
                }

                Thread.Sleep(1);
            }

            throw new TimeoutException("the loopback peers did not connect");
        }

        /// <summary>Runs both peers in turn, the host a little faster so the joiner's input is waited for, until both ran the steps.</summary>
        private static void Run(Peer host, Peer joiner, int steps, long divergeAt)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < 120)
            {
                for (int i = 0; i < 3; i++)
                {
                    _ = host.TryStep(steps);
                }

                for (int i = 0; i < 2; i++)
                {
                    if (joiner.World.Tick == divergeAt)
                    {
                        foreach (Player p in joiner.World.Players)
                        {
                            p.Position += new Vector3(0.5f, 0, 0);
                        }
                    }

                    _ = joiner.TryStep(steps);
                }

                bool done = host.World.Tick >= steps && joiner.World.Tick >= steps;
                bool split = host.Session.Desync.HasValue && joiner.Session.Desync.HasValue;
                if (done || split)
                {
                    host.Session.Pump();
                    joiner.Session.Pump();
                    return;
                }
            }

            throw new TimeoutException("the lockstep peers stopped stepping");
        }

        /// <summary>Builds a peer's world from the match header, as the screens build one.</summary>
        private static World Build(IReadOnlyList<ClubData> clubs, ReplayHeader header)
        {
            MatchSetup setup = header.Setup.TryGet(out ReplaySetup s)
                ? s.ToSetup(clubs)
                : throw new InvalidDataException("no setup");
            Settings settings = ScreenPrefsFile.FromJson(header.Prefs).Settings;
            World world = new(setup, settings) { Seed = header.Seed, App = new AppState() };
            Scenes.AddStarters(world);
            VersusMode.AssignSlots(world);
            MatchFlow.Begin(world);
            return world;
        }

        /// <summary>A player who skates in a slow figure and shoots now and then, different for each seed.</summary>
        private static Func<long, DeviceState> Script(int seed)
        {
            return tick => new DeviceState
            {
                Move = new Vector2(
                    MathF.Round(SimMath.Cos(tick / (37f * seed)) * 8, MidpointRounding.ToEven) / 8,
                    MathF.Round(SimMath.Sin(tick / (53f + seed)) * 8, MidpointRounding.ToEven) / 8
                ),
                Sprint = tick % 200 < 60,
                Shoot = tick % (90 + seed) < 12,
                Pass = tick % (130 + (7 * seed)) == 0,
            };
        }

        /// <summary>One machine of the loopback match: its world, systems and session.</summary>
        private sealed class Peer
        {
            private readonly ISystem[] systems;
            private readonly NetPadSlots pads = new();
            private readonly Func<long, DeviceState> script;
            private readonly Vector3 start;

            public Peer(World world, LockstepSession session, Func<long, DeviceState> script)
            {
                World = world;
                Session = session;
                this.script = script;
                int seed = world.Seed;
                systems =
                [
                    pads,
                    new PositioningSystem(
                        Seeds.Dice(seed, "positioning", PositioningSystem.Fallback)
                    ),
                    new DefenceSystem(Seeds.Dice(seed, "defence", DefenceSystem.Fallback)),
                    new DecisionSystem(Seeds.Dice(seed, "decisions", DecisionSystem.Fallback)),
                    new SwitchingSystem(),
                    new SkatingSystem(),
                    new CheckingSystem(Seeds.Dice(seed, "checking", CheckingSystem.Fallback)),
                    new PuckHandlingSystem(
                        Seeds.Dice(seed, "puckhandling", PuckHandlingSystem.Fallback)
                    ),
                    new GoalieSystem { Dice = Seeds.Dice(seed, "goalie", GoalieSystem.Fallback) },
                    new PuckPhysics(),
                    new MatchRules(),
                ];
                foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
                {
                    s.Start(world);
                }

                start = Mine.Select(p => p.Position).Or(Vector3.Zero);
            }

            public World World { get; }

            public LockstepSession Session { get; }

            public List<ulong> Sums { get; } = [];

            /// <summary>Gets a value indicating whether this side's controlled skater left where it started.</summary>
            public bool Moved =>
                Mine.Is(p => Vector3.Distance(p.Position, start) > 1f) || pads.Commanded[Slot];

            private int Slot => Session.Local == Side.Home ? 0 : 1;

            private Maybe<Player> Mine => World.Controlled(Slot);

            /// <summary>Sends this side's input ahead and runs the next step if both inputs for it are in.</summary>
            public bool TryStep(int steps)
            {
                Session.Pump();
                if (World.Tick >= steps || Session.Desync.HasValue)
                {
                    return false;
                }

                while (Session.NextSample <= World.Tick + Session.Delay)
                {
                    Session.Submit(
                        NetPad.Encode(
                            script(Session.NextSample),
                            pull: false,
                            accept: false,
                            Session.LastSent
                        )
                    );
                }

                if (!Session.Ready(World.Tick))
                {
                    return false;
                }

                pads.Frame = Session.Frame(World.Tick);
                Game.StepWorld(World, systems);
                ulong sum = WorldChecksum.Of(World);
                Sums.Add(sum);
                Session.AfterStep(World.Tick - 1, sum);
                return true;
            }
        }

        /// <summary>The input system's work for the two network pads, read from the step's frame rather than from the shared devices.</summary>
        private sealed class NetPadSlots : ISystem
        {
            private readonly DeviceReader[] readers = [new(), new()];

            public InputFrame Frame { get; set; } = InputFrame.Empty;

            public bool[] Commanded { get; } = new bool[2];

            public void Update(World world, float dt)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    PadFrame pad = Frame.Pads[slot];
                    bool Down(GameAction action)
                    {
                        return (pad.Down & (1u << (int)KeyBindings.Defaults[action].Button)) != 0;
                    }

                    DeviceState state = new()
                    {
                        Move = InputDevices.Stick(pad.Axes[0], pad.Axes[1]),
                        Sprint = Down(GameAction.Sprint),
                        Shoot = Down(GameAction.Shoot),
                        Pass = Down(GameAction.Pass),
                        Punch = Down(GameAction.Shoot),
                    };
                    PlayerCommand command = readers[slot].Next(state);
                    if (world.Controlled(slot).TryGet(out Player player))
                    {
                        player.Command = command;
                        Commanded[slot] |= command.Move != Vector2.Zero;
                    }
                }
            }
        }

        /// <summary>One end of an in-memory link, for the session's own checks.</summary>
        private sealed class LoopLink(
            Queue<byte[]> inbox,
            Queue<byte[]> outbox,
            bool[] open,
            int end
        ) : INetLink
        {
            public bool Open => open[end];

            public bool ClosedByPeer => !open[1 - end];

            public static (LoopLink A, LoopLink B) Pair()
            {
                Queue<byte[]> ab = new(),
                    ba = new();
                bool[] open = [true, true];
                return (new LoopLink(ba, ab, open, 0), new LoopLink(ab, ba, open, 1));
            }

            public void Send(byte[] message)
            {
                if (Open)
                {
                    outbox.Enqueue(message);
                }
            }

            public Maybe<byte[]> Receive()
            {
                return inbox.TryDequeue(out byte[] m) ? m : Maybe.None<byte[]>();
            }

            public void Dispose()
            {
                open[end] = false;
            }
        }
    }
}
