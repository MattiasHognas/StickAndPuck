using Raylib_cs;

namespace StickAndPuck.Arena;

/// <summary>
/// The arena tones that follow the lighting. The arena draws with these every frame, so W18
/// lighting presets and light cues only set them; the defaults are the dusk look of the
/// Blender broadcast render.
/// </summary>
public static class ArenaLook
{
    /// <summary>The brick-toned glass band above the boards (Mattias picked option A); alpha is how much it tints the crowd.</summary>
    public static readonly Color DuskGlass = new(0x7a, 0x3e, 0x35, 110);

    /// <summary>The flat diagonal light bands across the ice, one shade lighter than the ice.</summary>
    public static readonly Color DuskIceBand = new(0xe3, 0xd8, 0xc6, 255);

    /// <summary>Gets or sets the glass tone. The glass is drawn flat (unlit) in this colour and alpha.</summary>
    public static Color Glass { get; set; } = DuskGlass;

    /// <summary>Gets or sets the ice band tone. Alpha 0 hides the bands.</summary>
    public static Color IceBand { get; set; } = DuskIceBand;
}
