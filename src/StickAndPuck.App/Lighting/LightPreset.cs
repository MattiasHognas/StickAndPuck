using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.App.Lighting
{
    /// <summary>One lighting setup. The three tones multiply lit surfaces in the toon shader (shadow, mid,
    /// full); the band and glass colours are palette entries, so every frame still snaps cleanly.</summary>
    /// <param name="Time">The start time this preset is for.</param>
    /// <param name="StartTimes">The face-off times a match with this preset can have, for the HUD and logs.</param>
    /// <param name="LightDir">The direction the key light comes from.</param>
    /// <param name="Shadow">The tone of surfaces facing away from the light.</param>
    /// <param name="Mid">The tone of surfaces at a glancing angle.</param>
    /// <param name="Full">The tone of surfaces facing the light.</param>
    /// <param name="IceBand">The light bands across the ice.</param>
    /// <param name="Glass">The glass band over the boards; alpha is how much it tints the crowd.</param>
    /// <param name="Ice">The ice sheet itself, drawn flat in this palette grey so it stays cold whatever the key light does.</param>
    /// <param name="Scratch">The skate scratches, a shade off the ice; alpha is how faint they are.</param>
    public sealed record LightPreset(
        TimeOfDay Time,
        string[] StartTimes,
        Vector3 LightDir,
        Vector3 Shadow,
        Vector3 Mid,
        Vector3 Full,
        Color IceBand,
        Color Glass,
        Color Ice,
        Color Scratch
    );
}
