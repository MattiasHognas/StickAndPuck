using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>What the endings lay over the picture at the native 480x270: the title line in the game's font at
    /// twice its size on a dark band with gold trim, and the falling things drawn as whole pixels in palette
    /// colours (confetti and streamers in club colours, rain in three layers, camera flashes in the stands).</summary>
    internal sealed class EndingHud
    {
        private static readonly Color Band = new(0x18, 0x13, 0x1f, 0xe6);
        private static readonly Color Trim = new(0xc8, 0x96, 0x3c, 0xff);
        private static readonly Color Title = new(0xf2, 0xc1, 0x4e, 0xff);
        private static readonly Color Rain = new(0x7f, 0x9c, 0xae, 0xff);
        private static readonly Color RainFar = new(0x55, 0x5f, 0x6b, 0xff);
        private static readonly Color RainLit = new(0xf9, 0xda, 0x93, 0xff);
        private static readonly Color Flash = new(0xef, 0xe0, 0xc2, 0xff);

        /// <summary>Seconds into the scene the title comes up, and how long its band takes to open.</summary>
        private const float TitleAt = 1.4f,
            TitleOpen = 0.35f;

        private readonly (
            float X,
            float Y,
            float Speed,
            float Sway,
            float Phase,
            int Colour,
            bool Streamer
        )[] confetti;
        private readonly (float X, float Y, float Speed, int Layer)[] drops;

        public EndingHud(int seed)
        {
            Random dice = new(seed);
            confetti =
            [
                .. Enumerable
                    .Range(0, 170)
                    .Select(_ =>
                        (
                            (float)dice.NextDouble() * Game.Width,
                            (float)dice.NextDouble() * Game.Height,
                            18f + ((float)dice.NextDouble() * 26f),
                            3f + ((float)dice.NextDouble() * 9f),
                            (float)dice.NextDouble() * 6.28f,
                            dice.Next(3),
                            dice.NextDouble() < 0.18
                        )
                    ),
            ];
            drops =
            [
                .. Enumerable
                    .Range(0, 260)
                    .Select(i =>
                        (
                            (float)dice.NextDouble() * (Game.Width + 80),
                            (float)dice.NextDouble() * Game.Height,
                            0.7f + ((float)dice.NextDouble() * 0.6f),
                            i % 3
                        )
                    ),
            ];
        }

        /// <summary>The nearest palette colour to a club colour, so the HUD's pixels stay on the palette.</summary>
        public static Color Snap(string hex)
        {
            Color c = HudArt.Hex(hex);
            Vector3 v = new(c.R / 255f, c.G / 255f, c.B / 255f);
            Vector3 best = Palette.Current.MinBy(p => Vector3.DistanceSquared(p, v));
            return new Color(
                (byte)MathF.Round(best.X * 255f, MidpointRounding.ToEven),
                (byte)MathF.Round(best.Y * 255f, MidpointRounding.ToEven),
                (byte)MathF.Round(best.Z * 255f, MidpointRounding.ToEven),
                (byte)255
            );
        }

        /// <summary>The title band, coming up a moment into the scene.</summary>
        public static void DrawTitle(HudArt art, string text, float time)
        {
            if (time < TitleAt)
            {
                return;
            }

            const int scale = 2;
            int textWidth = art.Measure(text, scale);
            float open = Math.Clamp((time - TitleAt) / TitleOpen, 0f, 1f);
            int full = textWidth + 40;
            int w = Math.Max(2, (int)(full * open)) & ~1;
            int h = (art.LineHeight * scale) + 10;
            int x = (Game.Width - w) / 2,
                y = Game.Height - h - 14;
            DrawRectangle(x, y, w, h, Band);
            DrawRectangle(x, y, w, 1, Trim);
            DrawRectangle(x, y + h - 1, w, 1, Trim);
            if (open >= 1f)
            {
                DrawRectangle(x - 2, y + 3, 1, h - 6, Trim);
                DrawRectangle(x + w + 1, y + 3, 1, h - 6, Trim);
                art.DrawText(text, (Game.Width - textWidth) / 2, y + 6, Title, scale);
            }
        }

        /// <summary>Confetti and streamers falling over the frame, fluttering side to side, in up to three colours;
        /// a share of 0 to 1 draws that many of them.</summary>
        public void DrawConfetti(float time, Color[] colours, float share)
        {
            int n = (int)(confetti.Length * Math.Clamp(share, 0f, 1f));
            for (int i = 0; i < n; i++)
            {
                (
                    float x0,
                    float y0,
                    float speed,
                    float sway,
                    float phase,
                    int colour,
                    bool streamer
                ) = confetti[i];
                float y = ((y0 + (time * speed)) % (Game.Height + 12f)) - 6f;
                float x = x0 + (sway * MathF.Sin((time * 2.2f) + phase));
                Color c = colours[colour % colours.Length];
                int flip = (int)((time * 5f) + phase) % 3;
                if (streamer)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        DrawRectangle(
                            (int)(x + (2f * MathF.Sin((time * 4f) + phase + k))),
                            (int)y - k,
                            1,
                            1,
                            c
                        );
                    }
                }
                else
                {
                    DrawRectangle((int)x, (int)y, flip == 0 ? 1 : 2, flip == 2 ? 1 : 2, c);
                }
            }
        }

        /// <summary>Rain: three layers of streaks slanting down, the near ones longer and faster; drops inside a
        /// screen-space cone under the street lamp catch its light.</summary>
        public void DrawRain(float time, Vector2 lamp, Vector2 lampGround, float coneHalfWidth)
        {
            foreach ((float x0, float y0, float speed, int layer) in drops)
            {
                float fall = 260f + (layer * 120f);
                int len = 3 + (layer * 2);
                float y = ((y0 + (time * fall * speed)) % (Game.Height + len)) - len;
                float x = ((x0 - (y * 0.18f)) % (Game.Width + 80)) - 20f;
                float along = (y - lamp.Y) / MathF.Max(1f, lampGround.Y - lamp.Y);
                float cx = lamp.X + ((lampGround.X - lamp.X) * along);
                bool lit =
                    along is > 0f and < 1.05f
                    && MathF.Abs(x - cx) < coneHalfWidth * Math.Clamp(along, 0.08f, 1f);
                Color c = lit ? RainLit : (layer == 0 ? RainFar : Rain);
                for (int k = 0; k < len; k++)
                {
                    DrawPixel((int)(x - (k * 0.18f)), (int)y + k, c);
                }
            }
        }

        /// <summary>Camera flashes popping in the stands: a pixel cross for a frame or two each, inside a band of the frame.</summary>
        public static void DrawFlashes(float time, int top, int bottom, int seed)
        {
            int tick = (int)(time * 30f);
            for (int k = 0; k < 4; k++)
            {
                int h = Hash(seed, tick / 2, k);
                if (h % 3 != 0)
                {
                    continue;
                }

                int x = 8 + (Hash(seed + 1, tick / 2, k) % (Game.Width - 16));
                int y = top + (Hash(seed + 2, tick / 2, k) % Math.Max(1, bottom - top));
                DrawPixel(x, y, Flash);
                DrawPixel(x - 1, y, Flash);
                DrawPixel(x + 1, y, Flash);
                DrawPixel(x, y - 1, Flash);
                DrawPixel(x, y + 1, Flash);
            }
        }

        private static int Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)((a * 73856093) ^ (b * 19349663) ^ (c * 83492791));
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
