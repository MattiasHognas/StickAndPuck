using Raylib_cs;

namespace StickAndPuck.App.Arena;

/// <summary>
/// The arena tones that follow the lighting. The arena draws with these every frame, so W18
/// lighting presets and light cues only set them; the defaults are the dusk look of the
/// Blender broadcast render. Each world has its own (<see cref="Core.World.Look"/>, W56).
/// </summary>
public sealed class ArenaLook
{
    /// <summary>The brick-toned glass band above the boards (Mattias picked option A); alpha is how much it tints the crowd.</summary>
    public static readonly Color DuskGlass = new(0x7a, 0x3e, 0x35, 110);

    /// <summary>The flat diagonal light bands across the ice, one shade lighter than the ice.</summary>
    public static readonly Color DuskIceBand = new(0xe3, 0xd8, 0xc6, 255);

    /// <summary>Gets or sets the glass tone. The glass is drawn flat (unlit) in this colour and alpha.</summary>
    public Color Glass { get; set; } = DuskGlass;

    /// <summary>Gets or sets the ice band tone. Alpha 0 hides the bands.</summary>
    public Color IceBand { get; set; } = DuskIceBand;

    /// <summary>The ice sheet at dusk: the pale cold grey the arena's ice snaps to (#ccc9d2).</summary>
    public static readonly Color DuskIce = new(0xcc, 0xc9, 0xd2, 255);

    /// <summary>Gets or sets the ice sheet tone. The ice is drawn flat (unlit) in this colour, so it never takes the key light's warmth (W21).</summary>
    public Color Ice { get; set; } = DuskIce;

    /// <summary>Gets or sets the skate scratch tone; alpha is how faint the scratches are (W21).</summary>
    public Color Scratch { get; set; } = new(0xe3, 0xd8, 0xc6, 150);
}
