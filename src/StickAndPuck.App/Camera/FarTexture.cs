using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Camera
{
    /// <summary>Loads a kit atlas so it holds still at broadcast range. A 64x64 atlas drawn point-sampled on
    /// a skater only a few dozen pixels tall skips texels: as he skates, a one-texel stripe lands on a
    /// pixel in one frame and between pixels in the next, so stripes flicker and crawl. This gives the
    /// texture its own mipmap chain (32x32, 16x16, ...) and has the GPU pick the level that fits the
    /// skater's size on screen, still point-sampled inside it, so far players read one texel per pixel.</summary>
    /// <remarks>
    /// The smaller levels are built here rather than by the GPU: averaging would blend kit colours into
    /// shades that are not on the palette. Each texel of a level takes the most common of the four
    /// texels under it; on a tie the colour that is rarer in the whole atlas wins, so thin stripes,
    /// numbers and crests survive the halving instead of the base colour eating them.
    /// </remarks>
    public static class FarTexture
    {
        /// <summary>Loads an atlas with its palette-safe mipmap chain and nearest-texel sampling at every level.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        public static unsafe Texture2D Load(string path)
#pragma warning restore S6640
        {
            Image source = ResourceLoader.Image(path);
            int w = source.Width,
                h = source.Height;
            if (w <= 0 || w != h || (w & (w - 1)) != 0)
            {
                // not a square power of two: no chain, plain point sampling as before
                Texture2D plain = LoadTextureFromImage(source);
                UnloadImage(source);
                SetTextureFilter(plain, TextureFilter.Point);
                return plain;
            }

            Color* colours = LoadImageColors(source);
            uint[] level = new uint[w * w];
            for (int i = 0; i < level.Length; i++)
            {
                level[i] = Pack(colours[i]);
            }

            UnloadImageColors(colours);
            UnloadImage(source);

            Dictionary<uint, int> rarity = Counts(level);
            List<uint[]> chain = [level];
            for (int size = w / 2; size >= 1; size /= 2)
            {
                chain.Add(Halve(chain[^1], size, rarity));
            }

            int total = chain.Sum(l => l.Length);
            uint* data = (uint*)MemAlloc((uint)(total * sizeof(uint)));
            int at = 0;
            foreach (uint[] l in chain)
            {
                l.CopyTo(new Span<uint>(data + at, l.Length));
                at += l.Length;
            }

            Image image = new()
            {
                Data = data,
                Width = w,
                Height = w,
                Mipmaps = chain.Count,
                Format = PixelFormat.UncompressedR8G8B8A8,
            };
            Texture2D texture = LoadTextureFromImage(image);
            UnloadImage(image);
            Rlgl.TextureParameters(
                texture.Id,
                Rlgl.TEXTURE_MIN_FILTER,
                Rlgl.TEXTURE_FILTER_MIP_NEAREST
            );
            Rlgl.TextureParameters(
                texture.Id,
                Rlgl.TEXTURE_MAG_FILTER,
                Rlgl.TEXTURE_FILTER_NEAREST
            );
            return texture;
        }

        /// <summary>How often each colour appears in the full-size atlas.</summary>
        private static Dictionary<uint, int> Counts(uint[] texels)
        {
            Dictionary<uint, int> counts = [];
            foreach (uint c in texels)
            {
                counts[c] = counts.GetValueOrDefault(c) + 1;
            }

            return counts;
        }

        /// <summary>One level down: each texel is the most common of its four, the rarer colour on a tie.</summary>
        private static uint[] Halve(uint[] from, int size, IReadOnlyDictionary<uint, int> rarity)
        {
            int fromSize = size * 2;
            uint[] to = new uint[size * size];
            Span<uint> four = stackalloc uint[4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = (y * 2 * fromSize) + (x * 2);
                    four[0] = from[i];
                    four[1] = from[i + 1];
                    four[2] = from[i + fromSize];
                    four[3] = from[i + fromSize + 1];
                    to[(y * size) + x] = Pick(four, rarity);
                }
            }

            return to;
        }

        private static uint Pick(ReadOnlySpan<uint> four, IReadOnlyDictionary<uint, int> rarity)
        {
            uint best = four[0];
            int bestVotes = 0;
            foreach (uint c in four)
            {
                int votes = 0;
                foreach (uint o in four)
                {
                    if (o == c)
                    {
                        votes++;
                    }
                }

                bool better =
                    votes > bestVotes
                    || (
                        votes == bestVotes
                        && rarity.GetValueOrDefault(c) < rarity.GetValueOrDefault(best)
                    );
                if (better)
                {
                    best = c;
                    bestVotes = votes;
                }
            }

            return best;
        }

        private static uint Pack(Color c)
        {
            return c.R | ((uint)c.G << 8) | ((uint)c.B << 16) | ((uint)c.A << 24);
        }
    }
}
