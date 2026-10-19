using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Input;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The network match in the game: lives on <see cref="AppState"/>, and the game loop asks it before
    /// every fixed step whether the step may run (<see cref="Ready"/>) and tells it each step's checksum
    /// (<see cref="AfterStep"/>). Before a step it reads this machine's player from the devices, sends it
    /// for the step <see cref="LockstepSession.Delay"/> ahead, and holds the step until the other side's
    /// input for it is in; the step then plays both pads as the device frame (<see cref="DeviceInput.PlayRemote"/>).
    /// A checksum that differs ends the match with a desync message, as does the other side leaving or
    /// going quiet. Escape (or P, or start) twice leaves. When the screens move on to another world
    /// (the results) the session closes. With no network match every step runs as before.
    /// SAP_NET_DESYNC=step nudges this machine's players after that step, to see a desync caught.</summary>
    internal sealed class NetPlay
    {
        /// <summary>Seconds without the other side's input before the match counts as lost.</summary>
        private const double LostAfter = 15;

        /// <summary>Seconds a step may wait before the "waiting" banner shows.</summary>
        private const double WaitBanner = 0.3;

        /// <summary>Seconds the "press again to leave" banner stays up.</summary>
        private const double LeaveWindow = 3;

        private readonly Stopwatch stalled = new();
        private readonly Stopwatch leavePrompt = new();
        private Maybe<LockstepSession> session;
        private Maybe<World> match;
        private bool started,
            leaving;

        /// <summary>Gets the step after which SAP_NET_DESYNC nudges this machine's players, to see a desync caught; -1 for never.</summary>
        private static long DesyncAt =>
            long.TryParse(
                Env.Get("SAP_NET_DESYNC"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long step
            )
                ? step
                : -1;

        /// <summary>Gets this game's version, which both machines must share.</summary>
        public static string Version { get; } =
            typeof(NetPlay).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                is { } v
                ? v.InformationalVersion
                : "";

        /// <summary>Gets the lobby's address, port and delay.</summary>
        public NetLobby Lobby { get; } = new();

        /// <summary>Gets a value indicating whether a network match is being played.</summary>
        public bool Active => session.HasValue;

        /// <summary>Gets a value indicating whether a step has waited long enough for the other side to say so.</summary>
        public bool Waiting => Active && started && stalled.Elapsed.TotalSeconds > WaitBanner;

        /// <summary>Gets a value indicating whether the "press again to leave" banner is up.</summary>
        public bool LeavePrompt =>
            Active && leavePrompt.IsRunning && leavePrompt.Elapsed.TotalSeconds < LeaveWindow;

        /// <summary>Gets the club this machine's player plays, while a match is on.</summary>
        public Maybe<Side> Local => session.Select(s => s.Local);

        /// <summary>Gets a value indicating whether a world is the network match being played.</summary>
        public bool Owns(World world)
        {
            return match.Is(m => ReferenceEquals(m, world));
        }

        /// <summary>Gets the local device a slot's rumble goes to. Outside a network match (no local side) that is the
        /// slot's own device. In one, both machines play two virtual pads whose numbers say nothing about this
        /// machine's devices: the local player's slot rumbles the device they actually play with (no rumble
        /// for keyboard and mouse), and the other player's slot rumbles nothing here.</summary>
        /// <param name="slot">The slot the rumble is for.</param>
        /// <param name="local">The club this machine's player plays in a network match; none otherwise.</param>
        /// <param name="localDevice">This machine's player's device.</param>
        public static InputDevice RumbleDevice(
            InputSlot slot,
            Maybe<Side> local,
            InputDevice localDevice
        )
        {
            bool network = local.TryGet(out Side mine);
            bool mineAndPad =
                slot.Side == mine
                && slot.Device != InputDevice.None
                && localDevice is InputDevice.Gamepad1 or InputDevice.Gamepad2;
            return !network ? slot.Device
                : mineAndPad ? localDevice
                : InputDevice.None;
        }

        /// <summary>Gets the local device a slot of a world rumbles; see <see cref="RumbleDevice(InputSlot, Maybe{Side}, InputDevice)"/>.</summary>
        public InputDevice RumbleDevice(World world, InputSlot slot)
        {
            return RumbleDevice(
                slot,
                Owns(world) ? Local : Maybe.None<Side>(),
                LocalDevice(world.App)
            );
        }

        /// <summary>Starts lockstep on a match world the screens just started, over a session with the other side.</summary>
        public void Begin(World world, LockstepSession lockstep)
        {
            End();
            session = lockstep;
            match = world;
            started = false;
            leaving = false;
            leavePrompt.Reset();
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: network: match on, playing {lockstep.Local}, input delay {lockstep.Delay} steps, seed {world.Seed}"
                )
            );
        }

        /// <summary>Gets a value indicating whether the world's next step may run now.</summary>
        public bool Ready(World world)
        {
            if (!session.TryGet(out LockstepSession s))
            {
                return true;
            }

            if (!Owns(world))
            {
                End(); // the screens moved on (the results, or the menus): the match is over
                return true;
            }

            if (!started)
            {
                started = true;
                DeviceInput.PlayRemote(tick =>
                    s.Ready(tick) ? s.Frame(tick) : Maybe.None<InputFrame>()
                );
                stalled.Restart();
            }

            s.Pump();
            if (s.Desync.TryGet(out long at))
            {
                Stop(world, Text.Format(K.NetDesync, at));
                return false;
            }

            while (s.NextSample <= world.Tick + s.Delay)
            {
                s.Submit(Sample(world, s.LastSent));
            }

            if (leaving)
            {
                Console.WriteLine("SAP: network: you left the match");
                Stop(world, Maybe.None<string>());
                return false;
            }

            if (s.Ready(world.Tick))
            {
                stalled.Restart();
                return true;
            }

            if (s.PeerLeft || s.Disconnected)
            {
                Stop(world, Text.Get(s.PeerLeft ? K.NetOpponentLeft : K.NetConnectionLost));
            }
            else if (stalled.Elapsed.TotalSeconds > LostAfter)
            {
                Console.WriteLine("SAP: network: no input from the other side, giving up");
                Stop(world, Text.Get(K.NetConnectionLost));
            }

            return false;
        }

        /// <summary>Notes the checksum after a step of the network match.</summary>
        public void AfterStep(World world, ulong sum)
        {
            if (Owns(world) && session.TryGet(out LockstepSession s))
            {
                s.AfterStep(world.Tick - 1, sum);
                if (world.Tick - 1 == DesyncAt)
                {
                    foreach (Player p in world.Players)
                    {
                        p.Position += new Vector3(0.5f, 0, 0);
                    }

                    Console.WriteLine("SAP: network: SAP_NET_DESYNC nudged this side's players");
                }
            }
        }

        /// <summary>Closes the session, if any, and hands the frames back to the devices.</summary>
        public void End()
        {
            if (session.TryGet(out LockstepSession s))
            {
                s.Leave();
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: network: session closed after {s.Compared} matching checksums"
                    )
                );
                if (DeviceInput.Remote)
                {
                    DeviceInput.StopPlaying();
                }
            }

            session = Maybe.None<LockstepSession>();
            match = Maybe.None<World>();
            started = false;
        }

        /// <summary>Ends the match early and goes back to the menus, with a message when there is one.</summary>
        private void Stop(World world, Maybe<string> message)
        {
            End();
            if (message.TryGet(out string text))
            {
                Console.WriteLine($"SAP: network: {text}");
                world.App.Screens.ToMenus(new NetMessageScreen(world.App, text));
            }
            else
            {
                world.App.Screens.ToMenus();
            }
        }

        /// <summary>Reads this machine's player from the devices, through this machine's own settings and key bindings, as the pad to send.</summary>
        private PadFrame Sample(World world, PadFrame last)
        {
            AppState app = world.App;
            InputFrame shared = DeviceInput.Current;
            DeviceInput.Set(DeviceInput.ReadLocal());
            try
            {
                Settings mine = app.Screens.Settings;
                InputDevice device = LocalDevice(app);
                DeviceState state = device switch
                {
                    InputDevice.KeyboardMouse => InputDevices.Merge(
                        InputDevices.KeyboardMouse(mine),
                        InputDevices.Gamepad(app.Pads.Slot(0), mine)
                    ),
                    InputDevice.Gamepad1 => InputDevices.Gamepad(app.Pads.Slot(0), mine),
                    InputDevice.Gamepad2 => InputDevices.Gamepad(app.Pads.Slot(1), mine),
                    InputDevice.None => default,
                    _ => throw new NotSupportedException(
                        $"Unhandled input device {device} in network input"
                    ),
                };
                bool pull = DeviceInput.KeyDown(KeyboardKey.G) || AnyPad(GamepadButton.MiddleLeft);
                bool accept =
                    DeviceInput.KeyDown(KeyboardKey.Enter)
                    || DeviceInput.KeyDown(KeyboardKey.KpEnter);
                if (
                    DeviceInput.KeyPressed(KeyboardKey.Escape)
                    || KeyInput.Pressed(mine, GameAction.Pause)
                )
                {
                    AskToLeave();
                }

                return NetPad.Encode(state, pull, accept, last);
            }
            finally
            {
                DeviceInput.Set(shared);
            }
        }

        /// <summary>Gets this machine's player's device; SAP_HUMAN=0 lets the AI play this side.</summary>
        private static InputDevice LocalDevice(AppState app)
        {
            return Env.Is("SAP_HUMAN", "0") ? InputDevice.None : app.Screens.Prefs.Controls;
        }

        private void AskToLeave()
        {
            if (LeavePrompt)
            {
                leaving = true;
                return;
            }

            leavePrompt.Restart();
        }

        private static bool AnyPad(GamepadButton button)
        {
            for (int pad = 0; pad < DeviceInput.Pads; pad++)
            {
                if (DeviceInput.PadAvailable(pad) && DeviceInput.PadDown(pad, button))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
