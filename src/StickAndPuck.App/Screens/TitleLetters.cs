using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Screens
{
    /// <summary>The title intro's art: the logo cut into its letters (art/ui/title_letters.png, drawn by
    /// art/scripts/title_logo.py through make_ui.py), each with its place in the assembled logo, a mask of the
    /// logo's gold for the shine, and the dithered fade from black. Needs an open window.</summary>
    internal sealed class TitleLetters
    {
        private static readonly Color Black = new(0x18, 0x13, 0x1f, 0xff);
        private static readonly Color ShineCore = new(0xff, 0xf7, 0xdc, 0xff);
        private static readonly Color ShineEdge = new(0xf9, 0xda, 0x93, 0xff);

        // 4x4 ordered dither, as the pixel pass uses
        private static readonly int[] Bayer =
        [
            0,
            8,
            2,
            10,
            12,
            4,
            14,
            6,
            3,
            11,
            1,
            9,
            15,
            7,
            13,
            5,
        ];

        private readonly List<(Rectangle Source, Vector2 Offset)> pieces = [];
        private readonly Texture2D[] fades = new Texture2D[15];
        private Texture2D sheet;
        private Rectangle shine;

        private TitleLetters() { }

        /// <summary>Gets the assembled logo's width, in pixels.</summary>
        public int Width { get; private set; }

        /// <summary>Loads the letters and builds the fade patterns.</summary>
        public static TitleLetters Load()
        {
            TitleLetters art = new();
            using JsonDocument doc = JsonDocument.Parse(ResourceLoader.ReadText(Assets.UiLayout));
            JsonElement root = doc.RootElement;
            art.Width = root.GetProperty("title").GetProperty("size")[0].GetInt32();
            JsonElement letters = root.GetProperty("title_letters");
            art.sheet = ResourceLoader.Texture(letters.GetProperty("file").GetString());
            foreach (JsonElement piece in letters.GetProperty("pieces").EnumerateArray())
            {
                JsonElement r = piece.GetProperty("rect"),
                    o = piece.GetProperty("offset");
                art.pieces.Add(
                    (
                        new Rectangle(
                            r[0].GetInt32(),
                            r[1].GetInt32(),
                            r[2].GetInt32(),
                            r[3].GetInt32()
                        ),
                        new Vector2(o[0].GetInt32(), o[1].GetInt32())
                    )
                );
            }

            JsonElement s = letters.GetProperty("shine");
            art.shine = new Rectangle(
                s[0].GetInt32(),
                s[1].GetInt32(),
                s[2].GetInt32(),
                s[3].GetInt32()
            );
            for (int level = 1; level < 16; level++)
            {
                Image img = GenImageColor(4, 4, Color.Blank);
                for (int i = 0; i < 16; i++)
                {
                    if (Bayer[i] >= level)
                    {
                        ImageDrawPixel(ref img, i % 4, i / 4, Black);
                    }
                }

                Texture2D tex = LoadTextureFromImage(img);
                UnloadImage(img);
                SetTextureFilter(tex, TextureFilter.Point);
                SetTextureWrap(tex, TextureWrap.Repeat);
                art.fades[level - 1] = tex;
            }

            return art;
        }

        /// <summary>Covers the frame in black with the arena showing through a dithered share of it.</summary>
        /// <param name="reveal">0 all black, 1 nothing drawn.</param>
        public void DrawBlack(float reveal)
        {
            int level = (int)MathF.Ceiling(Math.Clamp(reveal, 0, 1) * 16);
            if (level >= 16)
            {
                return;
            }

            if (level <= 0)
            {
                DrawRectangle(0, 0, Game.Width, Game.Height, Black);
                return;
            }

            DrawTextureRec(
                fades[level - 1],
                new Rectangle(0, 0, Game.Width, Game.Height),
                Vector2.Zero,
                Color.White
            );
        }

        /// <summary>Darkens the arena behind the logo with a dithered shade, softer toward its edges, so the letters
        /// read over a busy stand (the home tifo comes down the frame past them, W142).</summary>
        /// <param name="strength">0 nothing, 1 the full shade.</param>
        /// <param name="x">The logo's left edge.</param>
        /// <param name="y">The logo's top row.</param>
        public void DrawBacking(float strength, int x, int y)
        {
            const int Rings = 4,
                Ring = 4;
            const float Darkest = 0.75f; // the share of black pixels behind the letters at full strength
            int h = (int)shine.Height;
            for (int ring = Rings - 1; ring >= 0; ring--)
            {
                float dark = Math.Clamp(strength, 0, 1) * Darkest * (Rings - ring) / Rings;
                int level = 16 - (int)MathF.Round(dark * 16, MidpointRounding.ToEven);
                if (level >= 16)
                {
                    continue;
                }

                int pad = (ring + 1) * Ring;
                Rectangle area = new(x - pad, y - pad, Width + (2 * pad), h + (2 * pad));
                DrawTextureRec(fades[level - 1], area, new Vector2(area.X, area.Y), Color.White);
            }
        }

        /// <summary>Draws the letters that have started to drop, with the logo's top left at (x, y).</summary>
        public void DrawLetters(TitleIntro intro, int x, int y)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!intro.Drop(i, out int dy))
                {
                    continue;
                }

                (Rectangle source, Vector2 offset) = pieces[i];
                DrawTextureRec(
                    sheet,
                    source,
                    new Vector2(x + offset.X, y + offset.Y + dy),
                    Color.White
                );
            }
        }

        /// <summary>Lights the gold inside a diagonal band, <paramref name="band"/> pixels along the logo at its top
        /// row and a pixel further right every two rows down: a bright core with a lighter gold edge.</summary>
        public void DrawShine(float band, int x, int y)
        {
            if (band < -40 || band > Width + 80)
            {
                return;
            }

            int h = (int)shine.Height;
            for (int row = 0; row < h; row++)
            {
                float c = band + (row * 0.5f);
                Span(row, c - 3, c + 3, ShineEdge, x, y);
                Span(row, c - 1, c + 1, ShineCore, x, y);
            }
        }

        private void Span(int row, float from, float to, Color tint, int x, int y)
        {
            int a = Math.Max(0, (int)MathF.Ceiling(from));
            int b = Math.Min(Width - 1, (int)MathF.Floor(to));
            if (b < a)
            {
                return;
            }

            DrawTextureRec(
                sheet,
                new Rectangle(shine.X + a, shine.Y + row, b - a + 1, 1),
                new Vector2(x + a, y + row),
                tint
            );
        }

        /// <summary>Releases the textures.</summary>
        public void Unload()
        {
            UnloadTexture(sheet);
            foreach (Texture2D t in fades)
            {
                UnloadTexture(t);
            }
        }
    }
}
