using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core
{
    /// <summary>The keyboard, mouse buttons and gamepads as one fixed step sees them. Every system and
    /// screen reads devices here, never from Raylib, so a match depends only on these frames: the
    /// replay writer saves them with their step and the replay reader plays them back. Game.cs calls
    /// <see cref="Poll"/> once per drawn frame and <see cref="Step"/> before every fixed step. A press
    /// counts on the first step after it, however many frames or steps that takes, so a press is never
    /// lost on a fast screen or doubled on a slow one. Stick axes are rounded to 1/4096.</summary>
    public static class DeviceInput
    {
        /// <summary>Raylib's gamepad slots.</summary>
        public const int Pads = 4;

        /// <summary>The axes kept per pad.</summary>
        public const int Axes = 6;

        private const int ButtonCount = 18;

        private static readonly KeyboardKey[] AllKeys =
        [
            .. Enum.GetValues<KeyboardKey>().Where(k => k != KeyboardKey.Null).Distinct(),
        ];
        private static readonly HashSet<int> PendingKeys = [];
        private static byte pendingMouse;
        private static readonly uint[] PendingButtons = new uint[Pads];

        /// <summary>Gets the frame the current step reads.</summary>
        public static InputFrame Current { get; private set; } = InputFrame.Empty;

        /// <summary>Gets where frames come from while a replay plays: the frame for a step, or None to keep the last one.</summary>
        private static Func<long, Maybe<InputFrame>> playback = _ => Maybe.None<InputFrame>();

        /// <summary>Gets a value indicating whether a replay supplies the frames.</summary>
        public static bool Playing { get; private set; }

        /// <summary>Lets a replay supply the frames from now on.</summary>
        public static void Play(Func<long, Maybe<InputFrame>> frames)
        {
            playback = frames;
            Playing = true;
        }

        /// <summary>Goes back to reading the devices.</summary>
        public static void StopPlaying()
        {
            playback = _ => Maybe.None<InputFrame>();
            Playing = false;
        }

        /// <summary>Clears devices and replay state before starting another browser session.</summary>
        public static void Reset()
        {
            StopPlaying();
            Current = InputFrame.Empty;
            PendingKeys.Clear();
            pendingMouse = 0;
            Array.Clear(PendingButtons);
        }

        /// <summary>Notes this drawn frame's presses, so the next step sees them even when this frame runs no step.</summary>
        public static void Poll()
        {
            if (Playing || !IsWindowReady())
            {
                return;
            }

            int queued;
            while ((queued = GetKeyPressed()) != 0)
            {
                _ = PendingKeys.Add(queued);
            }

            foreach (KeyboardKey k in AllKeys.Where(k => IsKeyPressed(k)))
            {
                _ = PendingKeys.Add((int)k);
            }

            for (int button = 0; button < 3; button++)
            {
                if (IsMouseButtonPressed((MouseButton)button))
                {
                    NoteMousePress(button);
                }
            }

            for (int pad = 0; pad < Pads; pad++)
            {
                if (!IsGamepadAvailable(pad))
                {
                    continue;
                }

                for (int b = 1; b < ButtonCount; b++)
                {
                    if (IsGamepadButtonPressed(pad, (GamepadButton)b))
                    {
                        PendingButtons[pad] |= 1u << b;
                    }
                }
            }
        }

        /// <summary>Takes the frame for one fixed step: the replay's, or the devices' now with the presses noted since the last step.</summary>
        public static void Step(long tick)
        {
            if (Playing)
            {
                StepPlayback(tick);
                return;
            }
            if (!IsWindowReady())
            {
                Current = InputFrame.Empty;
                return;
            }

            Current = ReadFrameFromDevices();
        }

        private static void StepPlayback(long tick)
        {
            if (playback(tick).TryGet(out InputFrame played))
            {
                Current = played;
            }
            else
            {
                Current = Current.Held(); // presses last one step
            }
        }

        private static InputFrame ReadFrameFromDevices()
        {
            int[] keys = [.. AllKeys.Where(k => IsKeyDown(k)).Cast<int>().Distinct().Order()];
            int[] pressed = [.. PendingKeys.Order()];
            byte mouse = pendingMouse;
            for (int b = 0; b < 3; b++)
            {
                if (IsMouseButtonDown((MouseButton)b))
                {
                    mouse |= (byte)(1 << b);
                }
            }

            PadFrame[] pads = ReadPadFrames();
            PendingKeys.Clear();
            pendingMouse = 0;
            Array.Clear(PendingButtons);
            return new InputFrame(keys, pressed, mouse, pads, MenuPress: "");
        }

        private static PadFrame[] ReadPadFrames()
        {
            PadFrame[] pads = new PadFrame[Pads];
            for (int pad = 0; pad < Pads; pad++)
            {
                if (!IsGamepadAvailable(pad))
                {
                    pads[pad] = PadFrame.None;
                    continue;
                }
                uint held = 0;
                for (int b = 1; b < ButtonCount; b++)
                {
                    if (IsGamepadButtonDown(pad, (GamepadButton)b))
                    {
                        held |= 1u << b;
                    }
                }

                float[] axes = new float[Axes];
                for (int a = 0; a < Axes; a++)
                {
                    axes[a] =
                        MathF.Round(
                            GetGamepadAxisMovement(pad, (GamepadAxis)a) * 4096f,
                            MidpointRounding.ToEven
                        ) / 4096f;
                }

                pads[pad] = new PadFrame(Available: true, held, PendingButtons[pad], axes);
            }
            return pads;
        }

        /// <summary>Keeps a browser mouse tap until a simulation step consumes it.</summary>
        public static void NoteMousePress(int button)
        {
            if (!Playing && button is >= 0 and < 3)
            {
                pendingMouse |= (byte)(1 << button);
            }
        }

        /// <summary>Starts from a given frame, for a replay's first step and for the frame a replay was recorded from.</summary>
        public static void Set(InputFrame frame)
        {
            Current = frame;
        }

        /// <summary>Notes a scripted menu press (SAP_SCREEN_KEYS) on this step's frame, so the replay presses it too.</summary>
        public static void Scripted(string button)
        {
            Current = Current with { MenuPress = button };
        }

        /// <summary>Whether a key is held or was tapped since the last step.</summary>
        public static bool KeyDown(KeyboardKey key)
        {
            return Array.BinarySearch(Current.Keys, (int)key) >= 0 || KeyPressed(key);
        }

        /// <summary>Whether a key went down since the last step.</summary>
        public static bool KeyPressed(KeyboardKey key)
        {
            return Array.BinarySearch(Current.Pressed, (int)key) >= 0;
        }

        /// <summary>Whether a mouse button is held.</summary>
        public static bool MouseDown(MouseButton button)
        {
            return (int)button < 3 && (Current.Mouse & (1 << (int)button)) != 0;
        }

        /// <summary>Whether a Raylib gamepad slot holds a pad.</summary>
        public static bool PadAvailable(int pad)
        {
            return Pad(pad).Available;
        }

        /// <summary>Whether a pad button is held.</summary>
        public static bool PadDown(int pad, GamepadButton button)
        {
            return Bit(Pad(pad).Down, button);
        }

        /// <summary>Whether a pad button went down since the last step.</summary>
        public static bool PadPressed(int pad, GamepadButton button)
        {
            return Bit(Pad(pad).Pressed, button);
        }

        /// <summary>Gets a pad axis, -1 to 1.</summary>
        public static float PadAxis(int pad, GamepadAxis axis)
        {
            PadFrame p = Pad(pad);
            return p.Available && (int)axis is >= 0 and < Axes ? p.Axes[(int)axis] : 0f;
        }

        private static PadFrame Pad(int pad)
        {
            return pad is >= 0 and < Pads ? Current.Pads[pad] : PadFrame.None;
        }

        private static bool Bit(uint bits, GamepadButton button)
        {
            return (int)button is > 0 and < ButtonCount && (bits & (1u << (int)button)) != 0;
        }
    }
}
