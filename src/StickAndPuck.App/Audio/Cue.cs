using System.Runtime.InteropServices;

namespace StickAndPuck.App.Audio
{
    /// <summary>One effect to start.</summary>
    /// <param name="Sound">The effect.</param>
    /// <param name="Volume">Its level for this hit, 0 to 1, on top of the mix level.</param>
    /// <param name="Pitch">Playback pitch, 1 as written.</param>
    /// <param name="Delay">Seconds to wait before it starts.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Cue(
        Sfx Sound,
        float Volume = 1,
        float Pitch = 1,
        float Delay = 0
    );
}
