using Raylib_cs;

namespace StickAndPuck.App.Core
{
    /// <summary>What one action is bound to: a keyboard key, a second key (WASD next to the arrows, say) and a
    /// gamepad button. <see cref="KeyboardKey.Null"/> and <see cref="GamepadButton.Unknown"/> mean unbound.</summary>
    public sealed record KeyBinding(KeyboardKey Key, KeyboardKey Alt, GamepadButton Button);
}
