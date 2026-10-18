namespace StickAndPuck.App.Benches
{
    /// <summary>Which clip, and where in it, a bench figure or staff member shows.</summary>
    /// <param name="Model">The model: <see cref="BenchClips.Skater"/>, <see cref="BenchClips.Goalie"/> or <see cref="BenchClips.Staff"/>.</param>
    /// <param name="Clip">The clip's name without the model's prefix.</param>
    /// <param name="Seconds">How far into the clip (s).</param>
    /// <param name="Loop">Whether the clip loops (else it holds its last frame).</param>
    /// <param name="Hold">A frame held for the whole reaction, or -1 to play the clip.</param>
    internal readonly record struct BenchLook(
        int Model,
        string Clip,
        float Seconds,
        bool Loop = true,
        int Hold = -1
    );
}
