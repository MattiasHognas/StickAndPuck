using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Hud
{
    /// <summary>The A3 HUD art from art/ui as A3 exported it: pixel font, scorebug frame with its slots,
    /// 9-slice panels, icons and club crests. Layout numbers come from ui.json, so the art and the
    /// game share one source. Every draw lands on whole pixels at the native 480x270.</summary>
    internal sealed class HudArt
    {
        /// <summary>Text colour from ui.json (cream).</summary>
        public Color Text { get; private set; } = Hex("#efe0c2");

        /// <summary>Score colour from ui.json (gold).</summary>
        public Color Score { get; private set; } = Hex("#f2c14e");

        /// <summary>Where the scorebug's top-left corner sits on screen.</summary>
        public (int X, int Y) BugPosition { get; private set; } = (8, 8);

        /// <summary>The scorebug frame's size.</summary>
        public (int W, int H) BugSize => (scorebug.Width, scorebug.Height);

        /// <summary>The font's line height in pixels.</summary>
        public int LineHeight => font.BaseSize;

        private readonly Dictionary<string, Rectangle> slots = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> crests = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> icons = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, (Texture2D Texture, int Border)> panels =
        [
            with(StringComparer.Ordinal),
        ];
        private string crestFallback = "generic";
        private Font font;
        private Texture2D scorebug,
            crestSheet,
            iconSheet;

        /// <summary>Loads the layout and textures. Needs an open window.</summary>
        public static HudArt Load()
        {
            HudArt art = new();

            string jsonText = ResourceLoader.ReadText(Assets.UiLayout);
            using JsonDocument doc = JsonDocument.Parse(jsonText);
            JsonElement root = doc.RootElement;

            art.font = ResourceLoader.Font(
                root.GetProperty("font").GetProperty("file").GetString()
            );
            SetTextureFilter(art.font.Texture, TextureFilter.Point);

            JsonElement bug = root.GetProperty("scorebug");
            art.scorebug = ResourceLoader.Texture(bug.GetProperty("file").GetString());
            JsonElement pos = bug.GetProperty("position");
            art.BugPosition = (pos[0].GetInt32(), pos[1].GetInt32());
            art.Text = Hex(bug.GetProperty("textColour").GetString());
            art.Score = Hex(bug.GetProperty("scoreColour").GetString());
            ReadCells(bug.GetProperty("slots"), art.slots);

            foreach (JsonProperty panel in root.GetProperty("panels").EnumerateObject())
            {
                art.panels[panel.Name] = (
                    ResourceLoader.Texture(panel.Value.GetProperty("file").GetString()),
                    panel.Value.GetProperty("border")[0].GetInt32()
                );
            }

            JsonElement icon = root.GetProperty("icons");
            art.iconSheet = ResourceLoader.Texture(icon.GetProperty("file").GetString());
            ReadCells(icon.GetProperty("cells"), art.icons);

            JsonElement crest = root.GetProperty("crests");
            art.crestSheet = ResourceLoader.Texture(crest.GetProperty("file").GetString());
            ReadCells(crest.GetProperty("cells"), art.crests);
            art.crestFallback = crest.GetProperty("fallback").GetString() ?? "generic";
            return art;
        }

        /// <summary>Gets a scorebug slot in screen pixels.</summary>
        public Rectangle Slot(string name)
        {
            Rectangle r = slots[name];
            return new Rectangle(BugPosition.X + r.X, BugPosition.Y + r.Y, r.Width, r.Height);
        }

        /// <summary>Draws the empty scorebug frame.</summary>
        public void DrawScorebug()
        {
            DrawTexture(scorebug, BugPosition.X, BugPosition.Y, Color.White);
        }

        /// <summary>Draws a club's 16x16 crest, or the generic one for a club without art.</summary>
        public void DrawCrest(string clubId, int x, int y)
        {
            if (!crests.TryGetValue(clubId, out Rectangle src))
            {
                src = crests[crestFallback];
            }

            DrawTextureRec(crestSheet, src, new(x, y), Color.White);
        }

        /// <summary>Draws a club's crest scaled by a whole factor (the intro card's wells take it at 2x).</summary>
        public void DrawCrest(string clubId, int x, int y, int scale)
        {
            if (!crests.TryGetValue(clubId, out Rectangle src))
            {
                src = crests[crestFallback];
            }

            DrawTexturePro(
                crestSheet,
                src,
                new Rectangle(x, y, src.Width * scale, src.Height * scale),
                new(0, 0),
                0,
                Color.White
            );
        }

        /// <summary>Draws one 8x8 icon.</summary>
        public void DrawIcon(string name, int x, int y)
        {
            if (icons.TryGetValue(name, out Rectangle src))
            {
                DrawTextureRec(iconSheet, src, new(x, y), Color.White);
            }
        }

        /// <summary>Draws a 9-slice panel.</summary>
        public void DrawPanel(string name, int x, int y, int w, int h)
        {
            (Texture2D tex, int b) = panels[name];
            NPatchInfo info = new()
            {
                Source = new Rectangle(0, 0, tex.Width, tex.Height),
                Left = b,
                Top = b,
                Right = b,
                Bottom = b,
                Layout = NPatchLayout.NinePatch,
            };
            DrawTextureNPatch(tex, info, new Rectangle(x, y, w, h), new(0, 0), 0, Color.White);
        }

        /// <summary>Measures text in the pixel font, without the trailing one-pixel gap.</summary>
        public int Measure(string text)
        {
            return text.Length == 0 ? 0 : (int)MeasureTextEx(font, text, font.BaseSize, 0).X - 1;
        }

        /// <summary>Draws text with its top-left corner on a whole pixel. The font has no lowercase, so text is upper-cased.</summary>
        public void DrawText(string text, int x, int y, Color colour)
        {
            DrawTextEx(font, text.ToUpperInvariant(), new(x, y), font.BaseSize, 0, colour);
        }

        /// <summary>Draws text centred in a 7 px capital slot from the scorebug layout.</summary>
        public void DrawInSlot(string text, Rectangle slot, Color colour)
        {
            text = text.ToUpperInvariant();
            int x = (int)slot.X + (((int)slot.Width - Measure(text)) / 2);
            DrawText(text, x, (int)slot.Y - 2, colour);
        }

        /// <summary>Releases the textures.</summary>
        public void Unload()
        {
            UnloadFont(font);
            UnloadTexture(scorebug);
            UnloadTexture(crestSheet);
            UnloadTexture(iconSheet);
            foreach ((Texture2D tex, int _) in panels.Values)
            {
                UnloadTexture(tex);
            }
        }

        /// <summary>Parses "#rrggbb".</summary>
        public static Color Hex(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color((v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff, 255);
        }

        private static void ReadCells(JsonElement cells, Dictionary<string, Rectangle> into)
        {
            foreach (JsonProperty cell in cells.EnumerateObject())
            {
                JsonElement a = cell.Value;
                into[cell.Name] = new Rectangle(
                    a[0].GetInt32(),
                    a[1].GetInt32(),
                    a[2].GetInt32(),
                    a[3].GetInt32()
                );
            }
        }
    }
}
