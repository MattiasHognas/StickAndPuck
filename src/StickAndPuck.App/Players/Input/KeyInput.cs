using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>
/// W39: the bound key and pad button of one action, for code outside the input system that reads a
/// single action itself: kit swap, camera zoom and the pause button.
/// </summary>
internal static class KeyInput
{
    /// <summary>Whether the action's key or pad button (on any pad) went down this frame.</summary>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <param name="action">The action.</param>
    /// <returns>Whether it was pressed.</returns>
    public static bool Pressed(Settings settings, GameAction action)
    {
        var b = KeyBindings.Of(settings, action);
        if ((b.Key != KeyboardKey.Null && DeviceInput.KeyPressed(b.Key)) || (b.Alt != KeyboardKey.Null && DeviceInput.KeyPressed(b.Alt))) return true;
        if (b.Button == GamepadButton.Unknown) return false;
        for (int pad = 0; pad < Gamepads.MaxSlots; pad++)    // W38: any slot a pad landed in
            if (DeviceInput.PadAvailable(pad) && DeviceInput.PadPressed(pad, b.Button)) return true;
        return false;
    }

    /// <summary>Whether the action's key is held.</summary>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <param name="action">The action.</param>
    /// <returns>Whether it is held.</returns>
    public static bool KeyDown(Settings settings, GameAction action)
    {
        var b = KeyBindings.Of(settings, action);
        return (b.Key != KeyboardKey.Null && DeviceInput.KeyDown(b.Key)) || (b.Alt != KeyboardKey.Null && DeviceInput.KeyDown(b.Alt));
    }

    /// <summary>Whether the action's button is held on this pad.</summary>
    /// <param name="settings">The settings holding the bindings.</param>
    /// <param name="action">The action.</param>
    /// <param name="pad">The gamepad index.</param>
    /// <returns>Whether it is held.</returns>
    public static bool PadDown(Settings settings, GameAction action, int pad)
    {
        return InputDevices.PadDown(pad, KeyBindings.Of(settings, action).Button);
    }
}
