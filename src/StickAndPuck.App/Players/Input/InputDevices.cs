using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Input
{
    /// <summary>Reads keyboard, mouse and gamepads into <see cref="DeviceState"/>.</summary>
    internal static class InputDevices
    {
        /// <summary>The round dead zone of a stick, as a fraction of its reach.</summary>
        internal const float StickDeadZone = 0.2f;

        /// <summary>The keyboard and mouse through the key bindings; by default arrows or WASD skate, shift sprints, space or left click shoots, E or right click passes, Q checks, C switches, L changes line, ctrl blocks, F accepts a fight, P pauses. The mouse buttons stay on shoot and pass.</summary>
        public static DeviceState KeyboardMouse(Settings settings)
        {
            DeviceState state = KeyboardMouse(settings, DeviceInput.KeyDown);
            state.Shoot |= DeviceInput.MouseDown(MouseButton.Left);
            state.Punch = state.Shoot;
            state.Pass |= DeviceInput.MouseDown(MouseButton.Right);
            return state;
        }

        /// <summary>The keyboard through the key bindings, with the key test given, so it can be checked without a window.</summary>
        public static DeviceState KeyboardMouse(Settings settings, Func<KeyboardKey, bool> down)
        {
            bool Key(GameAction action)
            {
                KeyBinding b = KeyBindings.Of(settings, action);
                return (b.Key != KeyboardKey.Null && down(b.Key))
                    || (b.Alt != KeyboardKey.Null && down(b.Alt));
            }

            Vector2 move = new(
                (Key(GameAction.SkateRight) ? 1f : 0f) - (Key(GameAction.SkateLeft) ? 1f : 0f),
                (Key(GameAction.SkateDown) ? 1f : 0f) - (Key(GameAction.SkateUp) ? 1f : 0f)
            );
            bool shoot = Key(GameAction.Shoot);
            return new DeviceState
            {
                Move = move.LengthSquared() > 1 ? Vector2.Normalize(move) : move,
                Sprint = Key(GameAction.Sprint),
                Shoot = shoot,
                Pass = Key(GameAction.Pass),
                Check = Key(GameAction.Check),
                Deke = Key(GameAction.Deke),
                Switch = Key(GameAction.Switch),
                Line = Key(GameAction.LineChange),
                Punch = shoot,
                Block = Key(GameAction.Block),
                Accept = Key(GameAction.AcceptFight),
                TimeOut = Key(GameAction.TimeOut),
                Pause = Key(GameAction.Pause),
            };
        }

        /// <summary>The gamepad in a Raylib slot (-1 for none, see <see cref="Gamepads.Slot"/>) through the button bindings; the left stick always skates, the bound buttons (the d-pad by default) too. By default right trigger sprints, A shoots, X passes, B checks, Y switches and accepts, LB changes line, RB blocks, start pauses.</summary>
        public static DeviceState Gamepad(int pad, Settings settings)
        {
            if (pad < 0)
            {
                return default;
            }

            bool Button(GameAction action)
            {
                return PadDown(pad, KeyBindings.Of(settings, action).Button);
            }

            Vector2 move = Stick(
                DeviceInput.PadAxis(pad, GamepadAxis.LeftX),
                DeviceInput.PadAxis(pad, GamepadAxis.LeftY)
            );
            if (move == Vector2.Zero)
            {
                move = new Vector2(
                    (Button(GameAction.SkateRight) ? 1f : 0f)
                        - (Button(GameAction.SkateLeft) ? 1f : 0f),
                    (Button(GameAction.SkateDown) ? 1f : 0f)
                        - (Button(GameAction.SkateUp) ? 1f : 0f)
                );
                if (move.LengthSquared() > 1)
                {
                    move = Vector2.Normalize(move);
                }
            }
            bool shoot = Button(GameAction.Shoot);
            return new DeviceState
            {
                Move = move,
                Sprint = Button(GameAction.Sprint),
                Shoot = shoot,
                Pass = Button(GameAction.Pass),
                Check = Button(GameAction.Check),
                Deke = Button(GameAction.Deke),
                Switch = Button(GameAction.Switch),
                Line = Button(GameAction.LineChange),
                Punch = shoot,
                Block = Button(GameAction.Block),
                Accept = Button(GameAction.AcceptFight),
                TimeOut = Button(GameAction.TimeOut),
                Pause = Button(GameAction.Pause),
            };
        }

        /// <summary>Whether a pad button is held; the triggers also count through their axis, which rests at -1.</summary>
        public static bool PadDown(int pad, GamepadButton button)
        {
            return button != GamepadButton.Unknown
                && DeviceInput.PadAvailable(pad)
                && (
                    DeviceInput.PadDown(pad, button)
                    || button switch
                    {
                        GamepadButton.RightTrigger2 => DeviceInput.PadAxis(
                            pad,
                            GamepadAxis.RightTrigger
                        ) > 0f,
                        GamepadButton.LeftTrigger2 => DeviceInput.PadAxis(
                            pad,
                            GamepadAxis.LeftTrigger
                        ) > 0f,
                        GamepadButton.Unknown
                        or GamepadButton.LeftFaceUp
                        or GamepadButton.LeftFaceRight
                        or GamepadButton.LeftFaceDown
                        or GamepadButton.LeftFaceLeft
                        or GamepadButton.RightFaceUp
                        or GamepadButton.RightFaceRight
                        or GamepadButton.RightFaceDown
                        or GamepadButton.RightFaceLeft
                        or GamepadButton.LeftTrigger1
                        or GamepadButton.RightTrigger1
                        or GamepadButton.MiddleLeft
                        or GamepadButton.Middle
                        or GamepadButton.MiddleRight
                        or GamepadButton.LeftThumb
                        or GamepadButton.RightThumb => false,
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(button),
                            button,
                            "unknown button"
                        ),
                    }
                );
        }

        /// <summary>Both devices at once, for one human who may pick up either.</summary>
        public static DeviceState Merge(DeviceState a, DeviceState b)
        {
            return new DeviceState
            {
                Move = a.Move.LengthSquared() >= b.Move.LengthSquared() ? a.Move : b.Move,
                Sprint = a.Sprint || b.Sprint,
                Shoot = a.Shoot || b.Shoot,
                Pass = a.Pass || b.Pass,
                Check = a.Check || b.Check,
                Deke = a.Deke || b.Deke,
                Switch = a.Switch || b.Switch,
                Line = a.Line || b.Line,
                Punch = a.Punch || b.Punch,
                Block = a.Block || b.Block,
                Accept = a.Accept || b.Accept,
                TimeOut = a.TimeOut || b.TimeOut,
                Pause = a.Pause || b.Pause,
            };
        }

        /// <summary>A round dead zone, rescaled so the stick still reaches full length just past it.</summary>
        /// <returns>The move vector, 0 to 1 long. Stick down is toward the camera (+z), as on screen.</returns>
        public static Vector2 Stick(float x, float y)
        {
            Vector2 v = new(x, y);
            float length = v.Length();
            return length < StickDeadZone
                ? Vector2.Zero
                : v / length * Math.Min(1f, (length - StickDeadZone) / (1f - StickDeadZone));
        }
    }
}
