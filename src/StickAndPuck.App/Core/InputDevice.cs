namespace StickAndPuck.App.Core
{
    /// <summary>The device a human input slot reads.</summary>
    public enum InputDevice
    {
        /// <summary>Slot not used: AI plays.</summary>
        None,

        /// <summary>Keyboard and mouse.</summary>
        KeyboardMouse,

        /// <summary>The first gamepad.</summary>
        Gamepad1,

        /// <summary>The second gamepad.</summary>
        Gamepad2,
    }
}
