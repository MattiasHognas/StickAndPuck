using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena
{
    /// <summary>A17: the home club's crest at centre ice in an NHL arena, as real NHL rinks paint the home
    /// club's logo there. The crest is the club's 16x16 cell of art/ui/crests.png (A16, the same art the
    /// HUD uses, located through ui.json), a decal lying flat on the ice plane in world space: every crest
    /// pixel a 0.5 m square, so the 8 m crest sits inside the 9.14 m centre circle and the broadcast camera
    /// foreshortens it exactly as it does the circle and the lines. It takes the ice's light (<see cref="Tone"/>)
    /// and lets some ice through, faded like a logo painted under the ice. Drawn on the ice with the bands
    /// and scratches: depth test, no depth write, so players drawn earlier stay in front and the centre
    /// line and dot go over it.</summary>
    internal sealed class CentreIce
    {
        private const float Pixel = 0.5f,
            Y = 0f;

        /// <summary>How much of the crest shows over the ice, of 255: painted-under-ice logos read a little faded.</summary>
        private const byte Fade = 200;

        private readonly Dictionary<string, Rectangle> cells = [with(StringComparer.Ordinal)];
        private string fallback = "generic";
        private Texture2D sheet;

        /// <summary>Loads the crest sheet and its cells from ui.json. Needs an open window.</summary>
        public void Load()
        {
            string jsonText = ResourceLoader.ReadText(Assets.UiLayout);
            using JsonDocument doc = JsonDocument.Parse(jsonText);
            JsonElement crests = doc.RootElement.GetProperty("crests");
            sheet = ResourceLoader.Texture(crests.GetProperty("file").GetString() ?? "");
            SetTextureFilter(sheet, TextureFilter.Point);
            foreach (JsonProperty cell in crests.GetProperty("cells").EnumerateObject())
            {
                JsonElement a = cell.Value;
                cells[cell.Name] = new Rectangle(
                    a[0].GetInt32(),
                    a[1].GetInt32(),
                    a[2].GetInt32(),
                    a[3].GetInt32()
                );
            }
            fallback = crests.GetProperty("fallback").GetString() ?? "generic";
        }

        /// <summary>Gets the crest's tone for an ice tone: white under the dusk lights the crest art is
        /// painted for, and dimmed or shifted as the ice sheet is (the warm-up's dimmed lights, the lighting
        /// moods), since the crest is drawn flat like the ice; its alpha is the under-ice fade.</summary>
        public static Color Tone(Color ice)
        {
            static byte F(byte v, byte dusk)
            {
                return (byte)Math.Min(255, v * 255 / dusk);
            }

            Color dusk = ArenaLook.DuskIce;
            return new Color(F(ice.R, dusk.R), F(ice.G, dusk.G), F(ice.B, dusk.B), Fade);
        }

        /// <summary>Draws a club's crest at centre ice in the given tone. The caller has turned depth writes off.</summary>
        public void Draw(string clubId, Color tone)
        {
            if (
                sheet.Id == 0
                || !(
                    cells.TryGetValue(clubId, out Rectangle src)
                    || cells.TryGetValue(fallback, out src)
                )
            )
            {
                return;
            }

            float x = src.Width * Pixel / 2f,
                z = src.Height * Pixel / 2f;
            float u0 = src.X / sheet.Width,
                u1 = (src.X + src.Width) / sheet.Width,
                v0 = src.Y / sheet.Height,
                v1 = (src.Y + src.Height) / sheet.Height;
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableBackfaceCulling();
            Rlgl.SetTexture(sheet.Id);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tone.R, tone.G, tone.B, tone.A);
            Rlgl.TexCoord2f(u0, v0);
            Rlgl.Vertex3f(-x, Y, -z);
            Rlgl.TexCoord2f(u0, v1);
            Rlgl.Vertex3f(-x, Y, z);
            Rlgl.TexCoord2f(u1, v1);
            Rlgl.Vertex3f(x, Y, z);
            Rlgl.TexCoord2f(u1, v0);
            Rlgl.Vertex3f(x, Y, -z);
            Rlgl.End();
            Rlgl.SetTexture(0);
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableBackfaceCulling();
        }

        /// <summary>Frees the crest sheet.</summary>
        public void Unload()
        {
            if (sheet.Id != 0)
            {
                UnloadTexture(sheet);
            }

            sheet = default;
        }
    }
}
