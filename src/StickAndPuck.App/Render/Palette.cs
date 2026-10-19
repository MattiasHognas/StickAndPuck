using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Render
{
    /// <summary>The palette the whole frame snaps to, read from the GIMP palette file the art uses
    /// (art/palette/stickandpuck_dusk.gpl), so the game and the Blender post pass always agree.</summary>
    public static class Palette
    {
        /// <summary>The most colours the post shader takes: the size of the palette uniform array in
        /// shaders/post.fs and shaders/web/post.fs, which must change with it. Grown from 64 for the NHL
        /// club kits (A16); WebGL 1 keeps a 128-entry vec3 array well inside its uniform budget.</summary>
        public const int MaxColours = 128;

        private static readonly Lazy<IReadOnlyList<Vector3>> current = new(() =>
            Load(Assets.Palette)
        );

        /// <summary>Gets the palette from <see cref="Assets.Palette"/>, loaded on first use.</summary>
        public static IReadOnlyList<Vector3> Current => current.Value;

        /// <summary>Gets the index of the first reserved kit colour: colours from here on are written by
        /// art/scripts/make_kits.py below its marker line, and only player pixels may snap to them,
        /// so adding club colours never changes how the arena and crowd look. Equals the palette
        /// size when the file has no kit block.</summary>
        public static int KitStart => kitStart.Value;

        private static readonly Lazy<int> kitStart = new(() => FindKitStart(Assets.Palette));

        /// <summary>No separators: Split then splits on white space.</summary>
        private static readonly char[] Whitespace = [];

        /// <summary>The comment line make_kits.py writes above the kit colours.</summary>
        private const string KitMarker = "# kit colours:";

        private static int FindKitStart(string path)
        {
            int count = 0;
            using StringReader reader = new(ResourceLoader.ReadText(path));
            while (reader.ReadLine() is string raw)
            {
                string line = raw.Trim();
                if (line.StartsWith(KitMarker, StringComparison.Ordinal))
                {
                    return count;
                }

                string[] parts = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                if (line.Length > 0 && char.IsDigit(line[0]) && parts.Length >= 3)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Gets the outline colour: the darkest palette colour (dark plum in the dusk palette).</summary>
        public static Vector3 Outline =>
            Current.MinBy(c => (c.X * 0.299f) + (c.Y * 0.587f) + (c.Z * 0.114f));

        /// <summary>Reads a GIMP .gpl palette: one "r g b [name]" line per colour, 0-255 each.</summary>
        public static IReadOnlyList<Vector3> Load(string path)
        {
            List<Vector3> colours = [];
            using StringReader reader = new(ResourceLoader.ReadText(path));
            while (reader.ReadLine() is string raw)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || !char.IsDigit(line[0]))
                {
                    continue;
                }

                string[] parts = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    continue;
                }

                colours.Add(
                    new Vector3(
                        byte.Parse(parts[0], CultureInfo.InvariantCulture) / 255f,
                        byte.Parse(parts[1], CultureInfo.InvariantCulture) / 255f,
                        byte.Parse(parts[2], CultureInfo.InvariantCulture) / 255f
                    )
                );
            }
            return (colours.Count == 0)
                ? throw new InvalidDataException($"{path} has no colours")
                : (
                    (colours.Count > MaxColours)
                        ? throw new InvalidDataException(
                            $"{path} has {colours.Count} colours; the post shader takes {MaxColours}"
                        )
                        : ((IReadOnlyList<Vector3>)colours.AsReadOnly())
                );
        }
    }
}
