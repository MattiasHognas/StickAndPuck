using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Players;

/// <summary>The buttons a device reports in one step, before presses are found.</summary>
internal struct DeviceState
{
    public Vector2 Move;
    public bool Sprint, Shoot, Pass, Check, Switch, Line, Punch, Block, Accept, Pause;
}

/// <summary>
/// W4a: turns one input slot's device into a <see cref="PlayerCommand"/>. Presses are found by
/// comparing held buttons with the previous fixed step rather than with Raylib's per-frame
/// pressed flags, so a press is seen exactly once whether a frame runs no step or several.
/// </summary>
internal sealed class DeviceReader
{
    DeviceState last;

    /// <summary>Reads the device and builds this step's command.</summary>
    /// <param name="now">What the device holds this step.</param>
    /// <returns>The command, with Pass, Check, Switch, Line, Punch, Accept and Pause true only on the step they went down.</returns>
    public PlayerCommand Next(DeviceState now)
    {
        var command = new PlayerCommand
        {
            Move = now.Move,
            Sprint = now.Sprint,
            Shoot = now.Shoot,
            Block = now.Block,
            Pass = now.Pass && !last.Pass,
            Check = now.Check && !last.Check,
            SwitchPlayer = now.Switch && !last.Switch,
            ChangeLine = now.Line && !last.Line,
            Punch = now.Punch && !last.Punch,
            AcceptFight = now.Accept && !last.Accept,
            Pause = now.Pause && !last.Pause,
        };
        last = now;
        return command;
    }
}

/// <summary>Reads keyboard, mouse and gamepads into <see cref="DeviceState"/>.</summary>
internal static class InputDevices
{
    const float StickDeadZone = 0.2f;

    /// <summary>The keyboard and mouse through the key bindings (W39); by default arrows or WASD skate, shift sprints, space or left click shoots, E or right click passes, Q checks, C switches, L changes line, ctrl blocks, F accepts a fight, P pauses. The mouse buttons stay on shoot and pass.</summary>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <returns>What is held.</returns>
    public static DeviceState KeyboardMouse(Settings settings)
    {
        var state = KeyboardMouse(settings, k => IsKeyDown(k));
        state.Shoot |= IsMouseButtonDown(MouseButton.Left);
        state.Punch = state.Shoot;
        state.Pass |= IsMouseButtonDown(MouseButton.Right);
        return state;
    }

    /// <summary>The keyboard through the key bindings, with the key test given, so it can be checked without a window.</summary>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <param name="down">Whether a key is held.</param>
    /// <returns>What is held.</returns>
    public static DeviceState KeyboardMouse(Settings settings, Func<KeyboardKey, bool> down)
    {
        bool Key(GameAction action)
        {
            var b = KeyBindings.Of(settings, action);
            return (b.Key != KeyboardKey.Null && down(b.Key)) || (b.Alt != KeyboardKey.Null && down(b.Alt));
        }

        var move = new Vector2(
            (Key(GameAction.SkateRight) ? 1f : 0f) - (Key(GameAction.SkateLeft) ? 1f : 0f),
            (Key(GameAction.SkateDown) ? 1f : 0f) - (Key(GameAction.SkateUp) ? 1f : 0f));
        bool shoot = Key(GameAction.Shoot);
        return new DeviceState
        {
            Move = move.LengthSquared() > 1 ? Vector2.Normalize(move) : move,
            Sprint = Key(GameAction.Sprint),
            Shoot = shoot,
            Pass = Key(GameAction.Pass),
            Check = Key(GameAction.Check),
            Switch = Key(GameAction.Switch),
            Line = Key(GameAction.LineChange),
            Punch = shoot,
            Block = Key(GameAction.Block),
            Accept = Key(GameAction.AcceptFight),
            Pause = Key(GameAction.Pause),
        };
    }

    /// <summary>A gamepad through the button bindings (W39); the left stick always skates, the bound buttons (the d-pad by default) too. By default right trigger sprints, A shoots, X passes, B checks, Y switches and accepts, LB changes line, RB blocks, start pauses.</summary>
    /// <param name="index">0 for the first connected pad, 1 for the second (W38: not the Raylib slot).</param>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <returns>What is held, or nothing when that pad is not connected.</returns>
    public static DeviceState Gamepad(int index, Settings settings)
    {
        int pad = Gamepads.Slot(index);
        if (pad < 0) return default;
        bool Button(GameAction action) => PadDown(pad, KeyBindings.Of(settings, action).Button);

        var move = Stick(GetGamepadAxisMovement(pad, GamepadAxis.LeftX), GetGamepadAxisMovement(pad, GamepadAxis.LeftY));
        if (move == Vector2.Zero)
        {
            move = new Vector2(
                (Button(GameAction.SkateRight) ? 1f : 0f) - (Button(GameAction.SkateLeft) ? 1f : 0f),
                (Button(GameAction.SkateDown) ? 1f : 0f) - (Button(GameAction.SkateUp) ? 1f : 0f));
            if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
        }
        bool shoot = Button(GameAction.Shoot);
        return new DeviceState
        {
            Move = move,
            Sprint = Button(GameAction.Sprint),
            Shoot = shoot,
            Pass = Button(GameAction.Pass),
            Check = Button(GameAction.Check),
            Switch = Button(GameAction.Switch),
            Line = Button(GameAction.LineChange),
            Punch = shoot,
            Block = Button(GameAction.Block),
            Accept = Button(GameAction.AcceptFight),
            Pause = Button(GameAction.Pause),
        };
    }

    /// <summary>Whether a pad button is held; the triggers also count through their axis, which rests at -1.</summary>
    /// <param name="pad">The gamepad index.</param>
    /// <param name="button">The button, or <see cref="GamepadButton.Unknown"/> for none.</param>
    /// <returns>Whether it is held.</returns>
    public static bool PadDown(int pad, GamepadButton button)
    {
        if (button == GamepadButton.Unknown || !IsGamepadAvailable(pad)) return false;
        if (IsGamepadButtonDown(pad, button)) return true;
        return button switch
        {
            GamepadButton.RightTrigger2 => GetGamepadAxisMovement(pad, GamepadAxis.RightTrigger) > 0f,
            GamepadButton.LeftTrigger2 => GetGamepadAxisMovement(pad, GamepadAxis.LeftTrigger) > 0f,
            _ => false,
        };
    }

    /// <summary>Both devices at once, for one human who may pick up either.</summary>
    /// <param name="a">One device.</param>
    /// <param name="b">The other.</param>
    /// <returns>The longer stick and every button held on either.</returns>
    public static DeviceState Merge(DeviceState a, DeviceState b)
    {
        return new DeviceState
        {
            Move = a.Move.LengthSquared() >= b.Move.LengthSquared() ? a.Move : b.Move,
            Sprint = a.Sprint || b.Sprint,
            Shoot = a.Shoot || b.Shoot,
            Pass = a.Pass || b.Pass,
            Check = a.Check || b.Check,
            Switch = a.Switch || b.Switch,
            Line = a.Line || b.Line,
            Punch = a.Punch || b.Punch,
            Block = a.Block || b.Block,
            Accept = a.Accept || b.Accept,
            Pause = a.Pause || b.Pause,
        };
    }

    /// <summary>A round dead zone, rescaled so the stick still reaches full length just past it.</summary>
    /// <param name="x">Stick x, -1 to 1.</param>
    /// <param name="y">Stick y, -1 (up) to 1 (down).</param>
    /// <returns>The move vector, 0 to 1 long. Stick down is toward the camera (+z), as on screen.</returns>
    public static Vector2 Stick(float x, float y)
    {
        var v = new Vector2(x, y);
        float length = v.Length();
        if (length < StickDeadZone) return Vector2.Zero;
        return v / length * Math.Min(1f, (length - StickDeadZone) / (1f - StickDeadZone));
    }
}
