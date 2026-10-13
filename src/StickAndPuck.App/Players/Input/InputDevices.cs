using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>The buttons a device reports in one step, before presses are found.</summary>
internal struct DeviceState
{
    public Vector2 Move;
    public bool Sprint, Shoot, Pass, Check, Deke, Switch, Line, Punch, Block, Accept, Pause;
}

/// <summary>Turns one input slot's device into a <see cref="PlayerCommand"/>. Presses are found by
/// comparing held buttons with the previous fixed step rather than with Raylib's per-frame
/// pressed flags, so a press is seen exactly once whether a frame runs no step or several.</summary>
internal sealed class DeviceReader
{
    DeviceState last;

    /// <summary>Reads the device and builds this step's command.</summary>
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
            Deke = now.Deke,
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

    /// <summary>The keyboard and mouse through the key bindings; by default arrows or WASD skate, shift sprints, space or left click shoots, E or right click passes, Q checks, C switches, L changes line, ctrl blocks, F accepts a fight, P pauses. The mouse buttons stay on shoot and pass.</summary>
    public static DeviceState KeyboardMouse(Settings settings)
    {
        var state = KeyboardMouse(settings, k => DeviceInput.KeyDown(k));
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
            Deke = Key(GameAction.Deke),
            Switch = Key(GameAction.Switch),
            Line = Key(GameAction.LineChange),
            Punch = shoot,
            Block = Key(GameAction.Block),
            Accept = Key(GameAction.AcceptFight),
            Pause = Key(GameAction.Pause),
        };
    }

    /// <summary>A gamepad through the button bindings; the left stick always skates, the bound buttons (the d-pad by default) too. By default right trigger sprints, A shoots, X passes, B checks, Y switches and accepts, LB changes line, RB blocks, start pauses.</summary>
    public static DeviceState Gamepad(int index, Settings settings)
    {
        int pad = Gamepads.Slot(index);
        if (pad < 0) return default;
        bool Button(GameAction action) => PadDown(pad, KeyBindings.Of(settings, action).Button);

        var move = Stick(DeviceInput.PadAxis(pad, GamepadAxis.LeftX), DeviceInput.PadAxis(pad, GamepadAxis.LeftY));
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
            Deke = Button(GameAction.Deke),
            Switch = Button(GameAction.Switch),
            Line = Button(GameAction.LineChange),
            Punch = shoot,
            Block = Button(GameAction.Block),
            Accept = Button(GameAction.AcceptFight),
            Pause = Button(GameAction.Pause),
        };
    }

    /// <summary>Whether a pad button is held; the triggers also count through their axis, which rests at -1.</summary>
    public static bool PadDown(int pad, GamepadButton button)
    {
        if (button == GamepadButton.Unknown || !DeviceInput.PadAvailable(pad)) return false;
        if (DeviceInput.PadDown(pad, button)) return true;
        return button switch
        {
            GamepadButton.RightTrigger2 => DeviceInput.PadAxis(pad, GamepadAxis.RightTrigger) > 0f,
            GamepadButton.LeftTrigger2 => DeviceInput.PadAxis(pad, GamepadAxis.LeftTrigger) > 0f,
            _ => false,
        };
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
            Pause = a.Pause || b.Pause,
        };
    }

    /// <summary>A round dead zone, rescaled so the stick still reaches full length just past it.</summary>
    /// <returns>The move vector, 0 to 1 long. Stick down is toward the camera (+z), as on screen.</returns>
    public static Vector2 Stick(float x, float y)
    {
        var v = new Vector2(x, y);
        float length = v.Length();
        if (length < StickDeadZone) return Vector2.Zero;
        return v / length * Math.Min(1f, (length - StickDeadZone) / (1f - StickDeadZone));
    }
}
