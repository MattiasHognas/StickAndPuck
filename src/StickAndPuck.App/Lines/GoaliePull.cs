using System.Collections.Immutable;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Lines
{
    /// <summary>Pulling the goalie for an extra attacker. A club pulls its goalie when a penalty call is
    /// delayed against the other club (the whistle goes as soon as they touch the puck, so the net is
    /// safe), and, trailing by a goal or two late in the last period of regulation, to press for the
    /// equaliser. AI clubs do both by themselves; a club with a human on it pulls for a delayed call
    /// by itself and otherwise on the button (G on the keyboard, back on a gamepad), which also
    /// brings the goalie back. The goalie comes back at the next whistle, which covers a goal at
    /// either end. <see cref="LinesSystem"/> runs <see cref="Update"/> each step; the penalties
    /// count an extra attacker when they balance the skaters at a faceoff.</summary>
    public static class GoaliePull
    {
        private static readonly ImmutableArray<Side> BothSides = [Side.Home, Side.Away];

        /// <summary>Gets whether a club has pulled its goalie.</summary>
        public static bool IsPulled(World world, Side side)
        {
            return Bench.Board(world).PulledGoalie[(int)side].HasValue;
        }

        /// <summary>Gets the extra attacker who came on for the goalie, if he is still on the ice.</summary>
        public static Maybe<Player> ExtraAttacker(World world, Side side)
        {
            LinesBoard b = Bench.Board(world);
            int id = b.ExtraAttacker[(int)side];
            return id >= 0 && id < world.Players.Count && world.Players[id].OnIce
                ? world.Players[id]
                : Maybe.None<Player>();
        }

        /// <summary>Gets how long before the end of the last period the AI pulls its goalie when trailing: two minutes, shortened with the period.</summary>
        public static float PullSeconds(World world)
        {
            return LinesTuning.PullSeconds
                * world.Setup.PeriodSeconds
                / PenaltyTuning.FullPeriodSeconds;
        }

        /// <summary>Pulls a club's goalie: he leaves for the bench and the freshest forward on the bench comes
        /// on through the bench door as the extra attacker. Nothing happens unless play is running
        /// with the goalie on the ice.</summary>
        public static bool Pull(World world, Side side)
        {
            if (
                !world.Setup.Rules.GoaliePull
                || !MatchFlow.IsRunning(world)
                || world.Match.Phase != MatchPhase.Play
                || IsPulled(world, side)
            )
            {
                return false;
            }

            if (world.OnIce(side).FirstOrDefault(p => p.IsGoalie) is not { } goalie)
            {
                return false;
            }

            LinesBoard b = Bench.Board(world);
            Bench.SendOff(world, goalie);
            Maybe<Player> extra = Bench.SendOn(world, side, Group.Forwards, onTheFly: true);
            b.PulledGoalie[(int)side] = goalie;
            b.ExtraAttacker[(int)side] = extra.Select(static e => e.Id).Or(-1);
            b.Last[(int)side] = ("EXTRA", world.Time);
            Log(
                world,
                $"{world.Setup.Club(side).ShortName} pulls {goalie.Data.LastName}"
                    + (extra.TryGet(out Player e) ? $", {e.Data.LastName} comes on" : string.Empty)
            );
            return true;
        }

        /// <summary>Brings a pulled goalie back into his crease and sends the extra attacker off, if he is still out there.</summary>
        public static bool Return(World world, Side side)
        {
            LinesBoard b = Bench.Board(world);
            if (!b.PulledGoalie[(int)side].TryGet(out Player goalie))
            {
                return false;
            }

            b.PulledGoalie[(int)side] = Maybe.None<Player>();
            // the extra attacker goes off, or whoever took his place in a line change since
            int skaters = world.OnIce(side).Count(p => !p.IsGoalie);
            if (Bench.Surplus(world, side, skaters - 1).TryGet(out Player extra))
            {
                Bench.SendOff(world, extra);
            }

            b.ExtraAttacker[(int)side] = -1;
            int end = FaceoffFormation.DefendedEnd(world.Match, side);
            goalie.OnIce = true;
            goalie.Position = new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0);
            goalie.Velocity = Vector3.Zero;
            goalie.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            goalie.Command = default;
            goalie.Begin(PlayerAction.None);
            Log(world, $"{world.Setup.Club(side).ShortName} goalie {goalie.Data.LastName} back in");
            return true;
        }

        /// <summary>One step: the goalie comes back at any stoppage; in play the AI, a delayed call or the button pulls him.</summary>
        internal static void Update(World world)
        {
            MatchState m = world.Match;
            LinesBoard b = Bench.Board(world);
            bool play = m.Phase == MatchPhase.Play;
            foreach (Side side in BothSides)
            {
                if (!play)
                {
                    _ = Return(world, side);
                    continue;
                }

                Side other = side == Side.Home ? Side.Away : Side.Home;
                bool delayedAgainstOther = PenaltyCalls.Delayed(world).Any(d => d.Side == other);
                bool human = world.Players.Exists(p => p.Side == side && p.Slot >= 0);
                if (
                    !IsPulled(world, side)
                    && (delayedAgainstOther || (!human && WantsExtraAttacker(world, side)))
                )
                {
                    _ = Pull(world, side);
                }
            }
            if (play)
            {
                Buttons(world, b);
            }
        }

        /// <summary>Whether an AI club trailing late wants the extra attacker: last period of regulation, inside the window, down one or two, and the puck out of its own zone or on its stick.</summary>
        private static bool WantsExtraAttacker(World world, Side side)
        {
            MatchState m = world.Match;
            if (m.Period != world.Setup.Periods || m.Clock > PullSeconds(world))
            {
                return false;
            }

            int deficit = m.Score[(int)Other(side)] - m.Score[(int)side];
            if (deficit is < 1 or > LinesTuning.PullMaxDeficit)
            {
                return false;
            }

            Puck pk = world.Puck;
            if (
                pk.Carrier >= 0
                && pk.Carrier < world.Players.Count
                && world.Players[pk.Carrier].Side == side
            )
            {
                return true;
            }

            int end = FaceoffFormation.DefendedEnd(m, side);
            return pk.Position.X * end < RinkSpec.BlueLineX;
        }

        /// <summary>The pull button of each human slot toggles the goalie of that slot's club.</summary>
        private static void Buttons(World world, LinesBoard b)
        {
            if (!IsWindowReady())
            {
                return;
            }

            IReadOnlyList<InputSlot> inputs = world.Setup.Inputs;
            bool padFree = !inputs.Any(i => i.Device == InputDevice.Gamepad1);
            for (int slot = 0; slot < inputs.Count && slot < b.PullHeld.Length; slot++)
            {
                bool held = inputs[slot].Device switch
                {
                    InputDevice.KeyboardMouse => DeviceInput.KeyDown(KeyboardKey.G)
                        || (padFree && PadBack(0)),
                    InputDevice.Gamepad1 => PadBack(0),
                    InputDevice.Gamepad2 => PadBack(1),
                    InputDevice.None => false,
                    _ => throw new NotSupportedException("Unhandled input device"),
                };
                bool pressed = held && !b.PullHeld[slot];
                b.PullHeld[slot] = held;
                if (!pressed)
                {
                    continue;
                }

                Side side = inputs[slot].Side;
                _ = IsPulled(world, side) ? Return(world, side) : Pull(world, side);
            }
        }

        private static bool PadBack(int pad)
        {
            return DeviceInput.PadAvailable(pad)
                && DeviceInput.PadDown(pad, GamepadButton.MiddleLeft);
        }

        private static Side Other(Side side)
        {
            return side == Side.Home ? Side.Away : Side.Home;
        }

        private static void Log(World world, string text)
        {
            if (MatchFlow.State(world).Log)
            {
                Console.WriteLine($"LINES: {text}");
            }
        }
    }
}
