using Raylib_cs;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Core;

/// <summary>An action a key or gamepad button can be bound to on the key-mapping page.</summary>
public enum GameAction
{
    /// <summary>Skate up the screen.</summary>
    SkateUp,

    /// <summary>Skate down the screen.</summary>
    SkateDown,

    /// <summary>Skate left.</summary>
    SkateLeft,

    /// <summary>Skate right.</summary>
    SkateRight,

    /// <summary>Sprint while held.</summary>
    Sprint,

    /// <summary>Shoot (hold to wind up); punches in a fight.</summary>
    Shoot,

    /// <summary>Pass.</summary>
    Pass,

    /// <summary>Body check.</summary>
    Check,

    /// <summary>Deke past a defender with the puck.</summary>
    Deke,

    /// <summary>Switch player.</summary>
    Switch,

    /// <summary>Change line.</summary>
    LineChange,

    /// <summary>Block in a fight.</summary>
    Block,

    /// <summary>Accept a fight.</summary>
    AcceptFight,

    /// <summary>Swap the kits.</summary>
    Kit,

    /// <summary>Toggle the camera zoom.</summary>
    Zoom,

    /// <summary>Pause the match.</summary>
    Pause,
}

/// <summary>What one action is bound to: a keyboard key, a second key (WASD next to the arrows, say) and a
/// gamepad button. <see cref="KeyboardKey.Null"/> and <see cref="GamepadButton.Unknown"/> mean unbound.</summary>
public sealed record KeyBinding(KeyboardKey Key, KeyboardKey Alt, GamepadButton Button);

/// <summary>The key and gamepad bindings, saved in settings.json with the rest of <see cref="Settings"/>.
/// Only rebound actions are stored; every other action, and every action of an older settings
/// file, falls back to <see cref="Defaults"/>. The mouse (left click shoots, right click passes),
/// the left stick, escape (back and pause in menus), G (pull the goalie) and F12 (screenshot)
/// stay fixed, so no mapping can lock the menus.</summary>
public static class KeyBindings
{
    /// <summary>The actions in the order the key-mapping page lists them.</summary>
    public static readonly IReadOnlyList<GameAction> All = Enum.GetValues<GameAction>();

    /// <summary>The defaults: arrows or WASD skate, shift sprints, space shoots, E passes, Q checks, C switches, L changes line, ctrl blocks, F accepts a fight, tab swaps kits, F6 zooms, P pauses; on a pad the d-pad skates, right trigger sprints, A shoots, X passes, B checks, Y switches and accepts, LB changes line, RB blocks, start pauses.</summary>
    public static readonly IReadOnlyDictionary<GameAction, KeyBinding> Defaults = new Dictionary<GameAction, KeyBinding>
    {
        [GameAction.SkateUp] = new(KeyboardKey.Up, KeyboardKey.W, GamepadButton.LeftFaceUp),
        [GameAction.SkateDown] = new(KeyboardKey.Down, KeyboardKey.S, GamepadButton.LeftFaceDown),
        [GameAction.SkateLeft] = new(KeyboardKey.Left, KeyboardKey.A, GamepadButton.LeftFaceLeft),
        [GameAction.SkateRight] = new(KeyboardKey.Right, KeyboardKey.D, GamepadButton.LeftFaceRight),
        [GameAction.Sprint] = new(KeyboardKey.LeftShift, KeyboardKey.RightShift, GamepadButton.RightTrigger2),
        [GameAction.Shoot] = new(KeyboardKey.Space, KeyboardKey.Null, GamepadButton.RightFaceDown),
        [GameAction.Pass] = new(KeyboardKey.E, KeyboardKey.Null, GamepadButton.RightFaceLeft),
        [GameAction.Check] = new(KeyboardKey.Q, KeyboardKey.Null, GamepadButton.RightFaceRight),
        [GameAction.Deke] = new(KeyboardKey.R, KeyboardKey.Null, GamepadButton.RightTrigger1),
        [GameAction.Switch] = new(KeyboardKey.C, KeyboardKey.Null, GamepadButton.RightFaceUp),
        [GameAction.LineChange] = new(KeyboardKey.L, KeyboardKey.Null, GamepadButton.LeftTrigger1),
        [GameAction.Block] = new(KeyboardKey.LeftControl, KeyboardKey.Null, GamepadButton.RightTrigger1),
        [GameAction.AcceptFight] = new(KeyboardKey.F, KeyboardKey.Null, GamepadButton.RightFaceUp),
        [GameAction.Kit] = new(KeyboardKey.Tab, KeyboardKey.Null, GamepadButton.Unknown),
        [GameAction.Zoom] = new(KeyboardKey.F6, KeyboardKey.Null, GamepadButton.Unknown),
        [GameAction.Pause] = new(KeyboardKey.P, KeyboardKey.Null, GamepadButton.MiddleRight),
    };

    /// <summary>The binding of one action under these settings.</summary>
    public static KeyBinding Of(Settings settings, GameAction action)
    {
        return settings.Keys.TryGetValue(action, out var b) && b is { } ? b : Defaults[action];
    }

    /// <summary>Binds a keyboard key to an action, replacing both of its keys.</summary>
    public static Settings WithKey(Settings settings, GameAction action, KeyboardKey key)
    {
        return With(settings, action, Of(settings, action) with { Key = key, Alt = KeyboardKey.Null });
    }

    /// <summary>Binds a gamepad button to an action.</summary>
    public static Settings WithButton(Settings settings, GameAction action, GamepadButton button)
    {
        return With(settings, action, Of(settings, action) with { Button = button });
    }

    /// <summary>A short upper-case name for the page, such as SPACE, LEFT CTRL or F6.</summary>
    public static string KeysName(KeyBinding binding)
    {
        string k = KeyName(binding.Key), a = KeyName(binding.Alt);
        return binding.Alt == KeyboardKey.Null ? k : binding.Key == KeyboardKey.Null ? a : $"{k}/{a}";
    }

    /// <summary>A short upper-case name of a keyboard key.</summary>
    public static string KeyName(KeyboardKey key) => key switch
    {
        KeyboardKey.Null => "-",
        KeyboardKey.LeftShift => Text.Get(K.KeyLShift),
        KeyboardKey.RightShift => Text.Get(K.KeyRShift),
        KeyboardKey.LeftControl => Text.Get(K.KeyLCtrl),
        KeyboardKey.RightControl => Text.Get(K.KeyRCtrl),
        KeyboardKey.LeftAlt => Text.Get(K.KeyLAlt),
        KeyboardKey.RightAlt => Text.Get(K.KeyRAlt),
        KeyboardKey.LeftSuper => Text.Get(K.KeyLSuper),
        KeyboardKey.RightSuper => Text.Get(K.KeyRSuper),
        KeyboardKey.PageUp => Text.Get(K.KeyPgUp),
        KeyboardKey.PageDown => Text.Get(K.KeyPgDown),
        KeyboardKey.CapsLock => Text.Get(K.KeyCaps),
        KeyboardKey.Backspace => Text.Get(K.KeyBksp),
        >= KeyboardKey.Zero and <= KeyboardKey.Nine => ((int)key - (int)KeyboardKey.Zero).ToString(System.Globalization.CultureInfo.InvariantCulture),
        >= KeyboardKey.Kp0 and <= KeyboardKey.Kp9 => Text.Format(K.KeyKp, (int)key - (int)KeyboardKey.Kp0),
        _ => key.ToString().ToUpperInvariant(),
    };

    /// <summary>A short name of a gamepad button, Xbox style.</summary>
    public static string ButtonName(GamepadButton button) => button switch
    {
        GamepadButton.LeftFaceUp => Text.Get(K.PadUp),
        GamepadButton.LeftFaceDown => Text.Get(K.PadDown),
        GamepadButton.LeftFaceLeft => Text.Get(K.PadLeft),
        GamepadButton.LeftFaceRight => Text.Get(K.PadRight),
        GamepadButton.RightFaceDown => Text.Get(K.PadA),
        GamepadButton.RightFaceRight => Text.Get(K.PadB),
        GamepadButton.RightFaceLeft => Text.Get(K.PadX),
        GamepadButton.RightFaceUp => Text.Get(K.PadY),
        GamepadButton.LeftTrigger1 => Text.Get(K.PadLb),
        GamepadButton.LeftTrigger2 => Text.Get(K.PadLt),
        GamepadButton.RightTrigger1 => Text.Get(K.PadRb),
        GamepadButton.RightTrigger2 => Text.Get(K.PadRt),
        GamepadButton.MiddleLeft => Text.Get(K.PadBack),
        GamepadButton.Middle => Text.Get(K.PadGuide),
        GamepadButton.MiddleRight => Text.Get(K.PadStart),
        GamepadButton.LeftThumb => Text.Get(K.PadLs),
        GamepadButton.RightThumb => Text.Get(K.PadRs),
        _ => "-",
    };

    static Settings With(Settings settings, GameAction action, KeyBinding binding)
    {
        var keys = new Dictionary<GameAction, KeyBinding>(settings.Keys) { [action] = binding };
        if (binding == Defaults[action]) keys.Remove(action);
        return settings with { Keys = keys };
    }
}
