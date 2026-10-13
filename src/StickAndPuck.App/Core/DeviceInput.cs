using System.Globalization;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

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

    const int ButtonCount = 18;

    static readonly KeyboardKey[] AllKeys = [.. Enum.GetValues<KeyboardKey>().Where(k => k != KeyboardKey.Null).Distinct()];
    static readonly HashSet<int> PendingKeys = [];
    static readonly uint[] PendingButtons = new uint[Pads];

    /// <summary>Gets the frame the current step reads.</summary>
    public static InputFrame Current { get; private set; } = InputFrame.Empty;

    /// <summary>Gets where frames come from while a replay plays: the frame for a step, or None to keep the last one.</summary>
    static Func<long, Maybe<InputFrame>> playback = _ => Maybe<InputFrame>.None;

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
        playback = _ => Maybe<InputFrame>.None;
        Playing = false;
    }

    /// <summary>Notes this drawn frame's presses, so the next step sees them even when this frame runs no step.</summary>
    public static void Poll()
    {
        if (Playing || !IsWindowReady()) return;
        foreach (var k in AllKeys)
            if (IsKeyPressed(k)) PendingKeys.Add((int)k);
        for (int pad = 0; pad < Pads; pad++)
        {
            if (!IsGamepadAvailable(pad)) continue;
            for (int b = 1; b < ButtonCount; b++)
                if (IsGamepadButtonPressed(pad, (GamepadButton)b)) PendingButtons[pad] |= 1u << b;
        }
    }

    /// <summary>Takes the frame for one fixed step: the replay's, or the devices' now with the presses noted since the last step.</summary>
    public static void Step(long tick)
    {
        if (Playing)
        {
            if (playback(tick).TryGet(out var played)) Current = played;
            else Current = Current.Held();    // presses last one step
            return;
        }
        if (!IsWindowReady())
        {
            Current = InputFrame.Empty;
            return;
        }

        int[] keys = AllKeys.Where(k => IsKeyDown(k)).Select(k => (int)k).Distinct().Order().ToArray();
        int[] pressed = PendingKeys.Order().ToArray();
        byte mouse = 0;
        for (int b = 0; b < 3; b++)
            if (IsMouseButtonDown((MouseButton)b)) mouse |= (byte)(1 << b);
        var pads = new PadFrame[Pads];
        for (int pad = 0; pad < Pads; pad++)
        {
            if (!IsGamepadAvailable(pad))
            {
                pads[pad] = PadFrame.None;
                continue;
            }
            uint held = 0;
            for (int b = 1; b < ButtonCount; b++)
                if (IsGamepadButtonDown(pad, (GamepadButton)b)) held |= 1u << b;
            float[] axes = new float[Axes];
            for (int a = 0; a < Axes; a++) axes[a] = MathF.Round(GetGamepadAxisMovement(pad, (GamepadAxis)a) * 4096f) / 4096f;
            pads[pad] = new PadFrame(true, held, PendingButtons[pad], axes);
        }
        PendingKeys.Clear();
        Array.Clear(PendingButtons);
        Current = new InputFrame(keys, pressed, mouse, pads, MenuPress: "");
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

    /// <summary>Whether a key is held.</summary>
    public static bool KeyDown(KeyboardKey key) => Array.BinarySearch(Current.Keys, (int)key) >= 0;

    /// <summary>Whether a key went down since the last step.</summary>
    public static bool KeyPressed(KeyboardKey key) => Array.BinarySearch(Current.Pressed, (int)key) >= 0;

    /// <summary>Whether a mouse button is held.</summary>
    public static bool MouseDown(MouseButton button) => (int)button < 3 && (Current.Mouse & (1 << (int)button)) != 0;

    /// <summary>Whether a Raylib gamepad slot holds a pad.</summary>
    public static bool PadAvailable(int pad) => Pad(pad).Available;

    /// <summary>Whether a pad button is held.</summary>
    public static bool PadDown(int pad, GamepadButton button) => Bit(Pad(pad).Down, button);

    /// <summary>Whether a pad button went down since the last step.</summary>
    public static bool PadPressed(int pad, GamepadButton button) => Bit(Pad(pad).Pressed, button);

    /// <summary>Gets a pad axis, -1 to 1.</summary>
    public static float PadAxis(int pad, GamepadAxis axis)
    {
        var p = Pad(pad);
        return p.Available && (int)axis is >= 0 and < Axes ? p.Axes[(int)axis] : 0f;
    }

    static PadFrame Pad(int pad) => pad is >= 0 and < Pads ? Current.Pads[pad] : PadFrame.None;

    static bool Bit(uint bits, GamepadButton button) => (int)button is > 0 and < ButtonCount && (bits & (1u << (int)button)) != 0;
}

/// <summary>One pad on one step.</summary>
/// <param name="Available">Whether the slot holds a pad.</param>
/// <param name="Down">Held buttons, one bit per <see cref="GamepadButton"/>.</param>
/// <param name="Pressed">Buttons that went down since the last step.</param>
/// <param name="Axes">The axes, rounded to 1/4096.</param>
public sealed record PadFrame(bool Available, uint Down, uint Pressed, float[] Axes)
{
    /// <summary>Gets an empty slot.</summary>
    public static PadFrame None { get; } = new(false, 0, 0, new float[DeviceInput.Axes]);
}

/// <summary>Every device on one step, as <see cref="DeviceInput"/> hands it to the game and the replay file
/// stores it, one line per change.</summary>
/// <param name="Keys">Held keys, sorted.</param>
/// <param name="Pressed">Keys that went down since the last step, sorted.</param>
/// <param name="Mouse">Held mouse buttons: bit 0 left, 1 right, 2 middle.</param>
/// <param name="Pads">The four pad slots.</param>
/// <param name="MenuPress">A scripted menu press, or empty.</param>
public sealed record InputFrame(int[] Keys, int[] Pressed, byte Mouse, PadFrame[] Pads, string MenuPress)
{
    /// <summary>Gets the frame with nothing connected or held.</summary>
    public static InputFrame Empty { get; } = new([], [], 0, [PadFrame.None, PadFrame.None, PadFrame.None, PadFrame.None], "");

    /// <summary>Gets this frame one step later with nothing new pressed: the same keys, buttons and sticks held.</summary>
    public InputFrame Held()
    {
        if (Pressed.Length == 0 && MenuPress.Length == 0 && Pads.All(p => p.Pressed == 0)) return this;
        return this with { Pressed = [], MenuPress = "", Pads = [.. Pads.Select(p => p with { Pressed = 0 })] };
    }

    /// <summary>Writes the frame as one line of text: "k:32,265 p:32 m:1 g0:down/pressed/x,y,... s:pause", empty parts left out.</summary>
    public string Write()
    {
        var b = new StringBuilder();
        if (Keys.Length > 0) b.Append(" k:").AppendJoin(',', Keys);
        if (Pressed.Length > 0) b.Append(" p:").AppendJoin(',', Pressed);
        if (Mouse != 0) b.Append(" m:").Append(Mouse);
        for (int i = 0; i < Pads.Length; i++)
        {
            var pad = Pads[i];
            if (!pad.Available) continue;
            b.Append(" g").Append(i).Append(':').Append(pad.Down.ToString("x", CultureInfo.InvariantCulture)).Append('/')
                .Append(pad.Pressed.ToString("x", CultureInfo.InvariantCulture)).Append('/')
                .AppendJoin(',', pad.Axes.Select(a => ((int)(a * 4096f)).ToString(CultureInfo.InvariantCulture)));
        }
        if (MenuPress.Length > 0) b.Append(" s:").Append(MenuPress);
        return b.Length == 0 ? "-" : b.ToString(1, b.Length - 1);
    }

    /// <summary>Reads a frame <see cref="Write"/> wrote.</summary>
    public static InputFrame Read(string text)
    {
        int[] keys = [], pressed = [];
        byte mouse = 0;
        string menu = "";
        var pads = new PadFrame[DeviceInput.Pads];
        Array.Fill(pads, PadFrame.None);
        foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, "-", StringComparison.Ordinal)) continue;
            int colon = part.IndexOf(':', StringComparison.Ordinal);
            string tag = part[..colon], value = part[(colon + 1)..];
            switch (tag)
            {
                case "k":
                    keys = Ints(value);
                    break;
                case "p":
                    pressed = Ints(value);
                    break;
                case "m":
                    mouse = byte.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "s":
                    menu = value;
                    break;
                case ['g', ..]:
                    string[] bits = value.Split('/');
                    float[] axes = [.. Ints(bits[2]).Select(a => a / 4096f)];
                    pads[int.Parse(tag[1..], CultureInfo.InvariantCulture)] = new PadFrame(
                        true, uint.Parse(bits[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture), uint.Parse(bits[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture), axes);
                    break;
                default:
                    throw new FormatException($"replay input '{part}' is not a known part");
            }
        }
        return new InputFrame(keys, pressed, mouse, pads, menu);
    }

    static int[] Ints(string text) => [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => int.Parse(t, CultureInfo.InvariantCulture))];
}
