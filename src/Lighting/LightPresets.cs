using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.Lighting;

/// <summary>When in the day a match starts. Each start time has its own lighting.</summary>
public enum TimeOfDay
{
    /// <summary>An early afternoon start: high, cool daylight through the roof windows, bright ice.</summary>
    Early,

    /// <summary>A late afternoon start: the dusk look of the Blender broadcast render.</summary>
    Afternoon,

    /// <summary>An evening start: low warm arena lights, deep plum shadows, gold light bands.</summary>
    Late,
}

/// <summary>
/// One lighting setup. The three tones multiply lit surfaces in the toon shader (shadow, mid,
/// full); the band and glass colours are palette entries, so every frame still snaps cleanly.
/// </summary>
/// <param name="Time">The start time this preset is for.</param>
/// <param name="StartTimes">The face-off times a match with this preset can have, for the HUD and logs.</param>
/// <param name="LightDir">The direction the key light comes from.</param>
/// <param name="Shadow">The tone of surfaces facing away from the light.</param>
/// <param name="Mid">The tone of surfaces at a glancing angle.</param>
/// <param name="Full">The tone of surfaces facing the light.</param>
/// <param name="IceBand">The light bands across the ice.</param>
/// <param name="Glass">The glass band over the boards; alpha is how much it tints the crowd.</param>
/// <param name="Ice">The ice sheet itself, drawn flat in this palette grey so it stays cold whatever the key light does (W21).</param>
/// <param name="Scratch">The skate scratches, a shade off the ice; alpha is how faint they are (W21).</param>
public sealed record LightPreset(
    TimeOfDay Time, string[] StartTimes, Vector3 LightDir,
    Vector3 Shadow, Vector3 Mid, Vector3 Full, Color IceBand, Color Glass, Color Ice, Color Scratch);

/// <summary>The three presets, one per start time.</summary>
public static class LightPresets
{
    /// <summary>The ice in daylight and at dusk: the palette's pale cold grey (#ccc9d2), what the dusk render's ice snaps to.</summary>
    public static readonly Color BrightIce = new(0xcc, 0xc9, 0xd2, 0xff);

    /// <summary>The ice under the evening lights: one palette step darker (#bdb9c4), still cold.</summary>
    public static readonly Color DimIce = new(0xbd, 0xb9, 0xc4, 0xff);

    /// <summary>Early: cool, high and bright, pale bands, a light glass tint.</summary>
    public static readonly LightPreset Early = new(
        TimeOfDay.Early, ["13:00", "14:00", "15:15"], new Vector3(-0.2f, 0.95f, 0.3f),
        Shadow: new Vector3(0.62f, 0.66f, 0.76f),
        Mid: new Vector3(0.92f, 0.95f, 1.02f),
        Full: new Vector3(1.12f, 1.12f, 1.16f),
        IceBand: new Color(0xef, 0xe0, 0xc2, 0xff),     // #efe0c2 cream
        Glass: new Color(0x9e, 0x65, 0x5c, 70),         // #9e655c, thin
        Ice: BrightIce,
        Scratch: new Color(0xe3, 0xd8, 0xc6, 150));     // #e3d8c6, faint

    /// <summary>Afternoon: the dusk look the art was made in; the shader's own defaults.</summary>
    public static readonly LightPreset Afternoon = new(
        TimeOfDay.Afternoon, ["16:30", "17:00"], new Vector3(-0.45f, 0.75f, 0.5f),
        Shadow: new Vector3(0.42f, 0.40f, 0.48f),
        Mid: new Vector3(0.70f, 0.68f, 0.72f),
        Full: Vector3.One,
        IceBand: new Color(0xe3, 0xd8, 0xc6, 0xff),     // #e3d8c6, ArenaLook.DuskIceBand
        Glass: new Color(0x7a, 0x3e, 0x35, 110),        // ArenaLook.DuskGlass
        Ice: BrightIce,
        Scratch: new Color(0xe3, 0xd8, 0xc6, 150));     // #e3d8c6, faint

    /// <summary>Late: low warm light from the side, dark plum shadows, gold bands, heavy glass.</summary>
    public static readonly LightPreset Late = new(
        TimeOfDay.Late, ["19:00", "19:30"], new Vector3(-0.75f, 0.45f, 0.5f),
        Shadow: new Vector3(0.30f, 0.24f, 0.36f),
        Mid: new Vector3(0.60f, 0.50f, 0.58f),
        Full: new Vector3(1.0f, 0.90f, 0.86f),
        IceBand: new Color(0xf0, 0xb4, 0x8a, 0xff),     // #f0b48a peach
        Glass: new Color(0x61, 0x3b, 0x3a, 160),        // #613b3a, thick
        Ice: DimIce,
        Scratch: new Color(0xcc, 0xc9, 0xd2, 150));     // #ccc9d2, the bright ice grey, faint

    /// <summary>Gets the preset for a start time.</summary>
    /// <param name="time">The start time.</param>
    /// <returns>Its preset.</returns>
    public static LightPreset For(TimeOfDay time)
    {
        return time switch
        {
            TimeOfDay.Early => Early,
            TimeOfDay.Late => Late,
            _ => Afternoon,
        };
    }
}
