
// Stick & Puck: a pixel-art hockey game drawn in 3D. Everything renders to a 480x270 target
// that is scaled up by whole pixels, so the window size never changes the look.
// OpenGL picks its graphics card when the window is made, so a game that should run on another
// card than it started on starts itself again on that card first (see GpuLaunch).

namespace StickAndPuck.App.Core
{
    /// <summary>A graphics card found on the machine.</summary>
    /// <param name="Id">The stable id saved in <see cref="Settings.Gpu"/>: the PCI address on Linux, the adapter's vendor:device on Windows.</param>
    /// <param name="Name">The name to show, like "NVIDIA GeForce RTX 5070 Ti".</param>
    /// <param name="Vendor">The PCI vendor id in lower-case hex, like "10de".</param>
    /// <param name="IsDefault">Whether the machine draws on this card when nothing says otherwise.</param>
    public sealed record GpuCard(string Id, string Name, string Vendor, bool IsDefault);
}
