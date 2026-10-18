using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Input;

namespace StickAndPuck.App.Network
{
    /// <summary>A player's input as the network carries it: one gamepad with the default button bindings.
    /// Each machine reads its own player's device (keyboard and mouse or a pad, through that machine's
    /// own key bindings) and turns it into this pad; both machines then play the same two pads, the
    /// host's as the first gamepad (the home club) and the joiner's as the second (the visitors), with
    /// the default bindings. So neither machine's key bindings or devices can make the two play the
    /// match differently.</summary>
    internal static class NetPad
    {
        private const float Scale = 4096f;

        /// <summary>Gets the pad of a player who presses nothing.</summary>
        public static PadFrame Idle { get; } = Make(0, 0, 0, 0);

        /// <summary>Turns one step of a player's device into the pad sent for them.</summary>
        /// <param name="state">The device's buttons and stick.</param>
        /// <param name="pull">Whether the pull-the-goalie button (G, or back on a pad) is held.</param>
        /// <param name="accept">Whether a menu accept (enter) is held: it skips replays and the intermission like A.</param>
        /// <param name="last">The pad sent for the step before, for the presses.</param>
        public static PadFrame Encode(DeviceState state, bool pull, bool accept, PadFrame last)
        {
            uint down = 0;
            void Set(GameAction action, bool on)
            {
                if (on)
                {
                    down |= Bit(KeyBindings.Defaults[action].Button);
                }
            }

            Set(GameAction.Sprint, state.Sprint);
            Set(GameAction.Shoot, state.Shoot || state.Punch || accept);
            Set(GameAction.Pass, state.Pass);
            Set(GameAction.Check, state.Check);
            Set(GameAction.Deke, state.Deke);
            Set(GameAction.Switch, state.Switch);
            Set(GameAction.LineChange, state.Line);
            Set(GameAction.Block, state.Block);
            Set(GameAction.AcceptFight, state.Accept);
            if (pull)
            {
                down |= Bit(GamepadButton.MiddleLeft);
            }

            // the receiving side reads the stick through the dead zone again, so the stick is lengthened past it
            Vector2 move = state.Move;
            float length = move.Length();
            Vector2 stick =
                length < 1e-4f
                    ? Vector2.Zero
                    : move
                        / length
                        * (
                            InputDevices.StickDeadZone
                            + ((1f - InputDevices.StickDeadZone) * Math.Min(length, 1f))
                        );
            return Make(down, down & ~last.Down, Quantize(stick.X), Quantize(stick.Y));
        }

        /// <summary>Builds a pad from the bits the network carries.</summary>
        public static PadFrame Make(uint down, uint pressed, short x, short y)
        {
            float[] axes = new float[DeviceInput.Axes];
            axes[(int)GamepadAxis.LeftX] = x / Scale;
            axes[(int)GamepadAxis.LeftY] = y / Scale;
            return new PadFrame(Available: true, down, pressed, axes);
        }

        /// <summary>Gets one stick axis (0 left x, 1 left y) as the network carries it.</summary>
        public static short AxisBits(PadFrame pad, int axis)
        {
            return Quantize(pad.Axes[axis]);
        }

        /// <summary>Gets the frame both machines play on a step: the home player's pad first, the visitors' second.</summary>
        public static InputFrame Frame(PadFrame home, PadFrame away)
        {
            return new InputFrame([], [], 0, [home, away, PadFrame.None, PadFrame.None], "");
        }

        private static uint Bit(GamepadButton button)
        {
            return button == GamepadButton.Unknown ? 0u : 1u << (int)button;
        }

        private static short Quantize(float v)
        {
            return (short)
                Math.Clamp(MathF.Round(v * Scale, MidpointRounding.ToEven), -Scale, Scale);
        }
    }
}
