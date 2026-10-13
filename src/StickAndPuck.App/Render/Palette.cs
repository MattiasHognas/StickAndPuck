using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Render;

/// <summary>
/// The palette the whole frame snaps to, read from the GIMP palette file the art uses
/// (art/palette/stickandpuck_dusk.gpl), so the game and the Blender post pass always agree.
/// </summary>
public static class Palette
{
    /// <summary>The most colours the post shader takes; the palette may grow to about 48 for club kits.</summary>
    public const int MaxColours = 64;

    static IReadOnlyList<Vector3>? current;

    /// <summary>Gets the palette from <see cref="Assets.Palette"/>, loaded on first use.</summary>
    public static IReadOnlyList<Vector3> Current => current ??= Load(Assets.Palette);

    /// <summary>
    /// Gets the index of the first reserved kit colour: colours from here on are written by
    /// art/scripts/make_kits.py below its marker line, and only player pixels may snap to them,
    /// so adding club colours never changes how the arena and crowd look. Equals the palette
    /// size when the file has no kit block.
    /// </summary>
    public static int KitStart => kitStart ??= FindKitStart(Assets.Palette);

    static int? kitStart;

    /// <summary>The comment line make_kits.py writes above the kit colours.</summary>
    const string KitMarker = "# kit colours:";

    static int FindKitStart(string path)
    {
        int count = 0;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith(KitMarker, StringComparison.Ordinal)) return count;
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (line.Length > 0 && char.IsDigit(line[0]) && parts.Length >= 3) count++;
        }
        return count;
    }

    /// <summary>Gets the outline colour: the darkest palette colour (dark plum in the dusk palette).</summary>
    public static Vector3 Outline => Current.MinBy(c => c.X * 0.299f + c.Y * 0.587f + c.Z * 0.114f);

    /// <summary>Reads a GIMP .gpl palette: one "r g b [name]" line per colour, 0-255 each.</summary>
    /// <param name="path">The .gpl file.</param>
    /// <returns>The colours in file order, 0-1 per channel.</returns>
    public static IReadOnlyList<Vector3> Load(string path)
    {
        var colours = new List<Vector3>();
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || !char.IsDigit(line[0])) continue;     // header, Name:, Columns:, comments
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            colours.Add(new Vector3(
                byte.Parse(parts[0], CultureInfo.InvariantCulture) / 255f,
                byte.Parse(parts[1], CultureInfo.InvariantCulture) / 255f,
                byte.Parse(parts[2], CultureInfo.InvariantCulture) / 255f));
        }
        if (colours.Count == 0) throw new InvalidDataException($"{path} has no colours");
        if (colours.Count > MaxColours) throw new InvalidDataException($"{path} has {colours.Count} colours; the post shader takes {MaxColours}");
        return colours.AsReadOnly();
    }
}
