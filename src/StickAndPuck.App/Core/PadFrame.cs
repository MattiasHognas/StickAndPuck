using Raylib_cs;

namespace StickAndPuck.App.Core
{
    /// <summary>One pad on one step.</summary>
    /// <param name="Available">Whether the slot holds a pad.</param>
    /// <param name="Down">Held buttons, one bit per <see cref="GamepadButton"/>.</param>
    /// <param name="Pressed">Buttons that went down since the last step.</param>
    /// <param name="Axes">The axes, rounded to 1/4096.</param>
    public sealed record PadFrame(bool Available, uint Down, uint Pressed, float[] Axes)
    {
        /// <summary>Gets an empty slot.</summary>
        public static PadFrame None { get; } =
            new(Available: false, 0, 0, new float[DeviceInput.Axes]);
    }
}
