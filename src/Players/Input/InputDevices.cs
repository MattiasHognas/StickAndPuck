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

    /// <summary>Arrows or WASD skate, shift sprints, space or left click shoots, E or right click passes, Q checks, C switches, L changes line, ctrl blocks, F accepts a fight, P pauses.</summary>
    /// <returns>What is held.</returns>
    public static DeviceState KeyboardMouse()
    {
        var move = new Vector2(
            Held(KeyboardKey.Right, KeyboardKey.D) - Held(KeyboardKey.Left, KeyboardKey.A),
            Held(KeyboardKey.Down, KeyboardKey.S) - Held(KeyboardKey.Up, KeyboardKey.W));
        bool shoot = IsKeyDown(KeyboardKey.Space) || IsMouseButtonDown(MouseButton.Left);
        return new DeviceState
        {
            Move = move.LengthSquared() > 1 ? Vector2.Normalize(move) : move,
            Sprint = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift),
            Shoot = shoot,
            Pass = IsKeyDown(KeyboardKey.E) || IsMouseButtonDown(MouseButton.Right),
            Check = IsKeyDown(KeyboardKey.Q),
            Switch = IsKeyDown(KeyboardKey.C),
            Line = IsKeyDown(KeyboardKey.L),
            Punch = shoot,
            Block = IsKeyDown(KeyboardKey.LeftControl),
            Accept = IsKeyDown(KeyboardKey.F),
            Pause = IsKeyDown(KeyboardKey.P),
        };
    }

    /// <summary>Left stick or d-pad skates, right trigger sprints, A shoots, X passes, B checks, Y switches, LB changes line, RB blocks, start pauses.</summary>
    /// <param name="pad">The gamepad index.</param>
    /// <returns>What is held, or nothing when no pad is connected.</returns>
    public static DeviceState Gamepad(int pad)
    {
        if (!IsGamepadAvailable(pad)) return default;
        var move = Stick(GetGamepadAxisMovement(pad, GamepadAxis.LeftX), GetGamepadAxisMovement(pad, GamepadAxis.LeftY));
        if (move == Vector2.Zero)
        {
            move = new Vector2(
                Down(pad, GamepadButton.LeftFaceRight) - Down(pad, GamepadButton.LeftFaceLeft),
                Down(pad, GamepadButton.LeftFaceDown) - Down(pad, GamepadButton.LeftFaceUp));
            if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
        }
        bool a = IsGamepadButtonDown(pad, GamepadButton.RightFaceDown), y = IsGamepadButtonDown(pad, GamepadButton.RightFaceUp);
        return new DeviceState
        {
            Move = move,
            Sprint = GetGamepadAxisMovement(pad, GamepadAxis.RightTrigger) > 0f,   // triggers rest at -1
            Shoot = a,
            Pass = IsGamepadButtonDown(pad, GamepadButton.RightFaceLeft),
            Check = IsGamepadButtonDown(pad, GamepadButton.RightFaceRight),
            Switch = y,
            Line = IsGamepadButtonDown(pad, GamepadButton.LeftTrigger1),
            Punch = a,
            Block = IsGamepadButtonDown(pad, GamepadButton.RightTrigger1),
            Accept = y,
            Pause = IsGamepadButtonDown(pad, GamepadButton.MiddleRight),
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

    static float Held(KeyboardKey a, KeyboardKey b)
    {
        return IsKeyDown(a) || IsKeyDown(b) ? 1f : 0f;
    }

    static float Down(int pad, GamepadButton button)
    {
        return IsGamepadButtonDown(pad, button) ? 1f : 0f;
    }
}
