using System.Numerics;

namespace StickAndPuck.App.Lighting
{
    /// <summary>What the cues do to the preset this step.</summary>
    /// <param name="Tone">Multiplies the preset's three tones.</param>
    /// <param name="Bands">Whether the ice bands show (they are reflections of the arena lights).</param>
    /// <param name="Flash">Whether a goal flash is lit this step, which turns bands and glass gold.</param>
    public readonly record struct CueLight(Vector3 Tone, bool Bands, bool Flash);
}
