namespace StickAndPuck.App.Audio
{
    /// <summary>How one effect is played.</summary>
    /// <param name="File">The file name in art/audio/effects/.</param>
    /// <param name="Volume">Its level in the mix, 0 to 1.</param>
    /// <param name="Gap">The shortest time in seconds between two starts, so a burst of events plays it once.</param>
    /// <param name="Spatial">Whether it pans with its position on the ice.</param>
    internal readonly record struct SfxInfo(string File, float Volume, float Gap, bool Spatial);
}
