namespace StickAndPuck.App.Core
{
    /// <summary>One human input slot: which device, and which club it plays for.</summary>
    public sealed record InputSlot(InputDevice Device, Side Side);
}
