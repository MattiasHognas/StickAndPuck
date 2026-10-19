using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Pregame
{
    /// <summary>The A22 art for the pre-game stats card (art/scripts/make_intro_card.py): the versus panel,
    /// its stat icons, form dots and playstyle badges, with every rect read from intro_card.json so the
    /// card and the game share one layout. Rects come back in screen pixels at the card's position;
    /// text goes through the HUD font (<see cref="HudArt"/>).</summary>
    internal sealed class IntroCardArt
    {
        private readonly Dictionary<string, Rectangle> slots = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> rects = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle[]> lists = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> statCells = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> formCells = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, Rectangle> styleCells = [with(StringComparer.Ordinal)];
        private readonly List<string> rows = [];
        private Texture2D card,
            icons;

        private IntroCardArt(HudArt hud)
        {
            Hud = hud;
        }

        /// <summary>Gets the HUD art (font and crests) the card draws its text and crests with.</summary>
        public HudArt Hud { get; }

        /// <summary>Gets where the card's top-left corner sits on screen (centred at 480x270).</summary>
        public (int X, int Y) Position { get; private set; }

        /// <summary>Gets the card's size.</summary>
        public (int W, int H) Size => (card.Width, card.Height);

        /// <summary>Gets the stat rows top to bottom: overall, attack, defence, speed, goalie (the bar rows), record, place, form, scorer, style.</summary>
        public IReadOnlyList<string> Rows => rows;

        /// <summary>Gets the label colour (cream).</summary>
        public Color Text { get; private set; } = HudArt.Hex("#efe0c2");

        /// <summary>Gets the value colour (gold).</summary>
        public Color Value { get; private set; } = HudArt.Hex("#f2c14e");

        /// <summary>Gets the colour for a missing value (grey).</summary>
        public Color Muted { get; private set; } = HudArt.Hex("#8a8790");

        /// <summary>Gets the home bars' fill.</summary>
        public Color BarHome { get; private set; } = HudArt.Hex("#f2c14e");

        /// <summary>Gets the away bars' fill.</summary>
        public Color BarAway { get; private set; } = HudArt.Hex("#7f9cae");

        /// <summary>Loads the card. Needs an open window.</summary>
        public static IntroCardArt Load(HudArt hud)
        {
            IntroCardArt art = new(hud);
            using JsonDocument doc = JsonDocument.Parse(
                ResourceLoader.ReadText(Assets.IntroCardLayout)
            );
            JsonElement root = doc.RootElement;
            JsonElement c = root.GetProperty("card");
            art.card = ResourceLoader.Texture(Assets.IntroCard);
            JsonElement pos = c.GetProperty("position");
            art.Position = (pos[0].GetInt32(), pos[1].GetInt32());
            foreach (JsonProperty slot in c.GetProperty("slots").EnumerateObject())
            {
                art.slots[slot.Name] = Rect(slot.Value);
            }

            foreach (JsonElement row in c.GetProperty("rows").EnumerateArray())
            {
                string name = row.GetProperty("name").GetString() ?? "";
                art.rows.Add(name);
                foreach (JsonProperty p in row.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    if (p.Value[0].ValueKind == JsonValueKind.Array)
                    {
                        art.lists[Key(name, p.Name)] = [.. p.Value.EnumerateArray().Select(Rect)];
                    }
                    else
                    {
                        art.rects[Key(name, p.Name)] = Rect(p.Value);
                    }
                }
            }

            JsonElement i = root.GetProperty("icons");
            art.icons = ResourceLoader.Texture(Assets.IntroIcons);
            Read(i.GetProperty("stat"), art.statCells);
            Read(i.GetProperty("form"), art.formCells);
            Read(i.GetProperty("style"), art.styleCells);

            JsonElement col = root.GetProperty("colours");
            art.Text = HudArt.Hex(col.GetProperty("text").GetString() ?? "#efe0c2");
            art.Value = HudArt.Hex(col.GetProperty("value").GetString() ?? "#f2c14e");
            art.Muted = HudArt.Hex(col.GetProperty("muted").GetString() ?? "#8a8790");
            art.BarHome = HudArt.Hex(col.GetProperty("barHome").GetString() ?? "#f2c14e");
            art.BarAway = HudArt.Hex(col.GetProperty("barAway").GetString() ?? "#7f9cae");
            return art;
        }

        /// <summary>The playstyle badge key for a club's style, e.g. shootFromAnywhere.</summary>
        public static string StyleKey(PlayStyle style)
        {
            return Camel(style.ToString());
        }

        /// <summary>The playstyle badge key for a forecheck, e.g. forecheck_trap.</summary>
        public static string StyleKey(Forecheck forecheck)
        {
            return "forecheck_" + Camel(forecheck.ToString());
        }

        /// <summary>The playstyle badge key for a zone entry, e.g. entry_dumpAndChase.</summary>
        public static string StyleKey(ZoneEntry entry)
        {
            return "entry_" + Camel(entry.ToString());
        }

        /// <summary>Gets a card slot (home_crest, home_name, home_stripe, the away ones, vs) on screen.</summary>
        public Rectangle Slot(string name)
        {
            return Screen(slots[name]);
        }

        /// <summary>Gets one rect of a row on screen: icon, home_value, away_value, home_bar, away_bar.</summary>
        public Rectangle RowRect(string row, string key)
        {
            return Screen(rects[Key(row, key)]);
        }

        /// <summary>Gets a row's list of rects on screen: the form row's home_dots and away_dots (five each,
        /// oldest first), the style row's home_badges and away_badges (three each, nearest the chip first).</summary>
        public IReadOnlyList<Rectangle> RowRects(string row, string key)
        {
            return [.. lists[Key(row, key)].Select(Screen)];
        }

        /// <summary>Draws the empty card.</summary>
        public void DrawCard()
        {
            DrawTexture(card, Position.X, Position.Y, Color.White);
        }

        /// <summary>Draws a club's crest at 2x into its well.</summary>
        public void DrawCrest(Side side, string clubId)
        {
            Rectangle r = Slot(Prefix(side) + "_crest");
            Hud.DrawCrest(clubId, (int)r.X, (int)r.Y, 2);
        }

        /// <summary>Draws every row's stat icon in its chip.</summary>
        public void DrawRowIcons()
        {
            foreach (string row in rows.Where(statCells.ContainsKey))
            {
                Rectangle r = RowRect(row, "icon");
                DrawTextureRec(icons, statCells[row], new(r.X, r.Y), Color.White);
            }
        }

        /// <summary>Draws a 0-100 rating bar, home growing left from the chip, away right.</summary>
        public void DrawBar(Side side, string row, int value)
        {
            Rectangle well = RowRect(row, Prefix(side) + "_bar");
            int w = (int)
                Math.Round(well.Width * Math.Clamp(value, 0, 100) / 100.0, MidpointRounding.ToEven);
            int x = side == Side.Home ? (int)(well.X + well.Width) - w : (int)well.X;
            Color fill = side == Side.Home ? BarHome : BarAway;
            DrawRectangle(x, (int)well.Y, w, (int)well.Height - 1, fill);
            DrawRectangle(x, (int)well.Y, w, 1, Lighter(fill));
        }

        /// <summary>Draws a row's value text, home right-aligned towards the chip and away left-aligned.</summary>
        public void DrawValue(Side side, string row, string text, Color colour)
        {
            Rectangle slot = RowRect(row, Prefix(side) + "_value");
            text = text.ToUpperInvariant();
            int x =
                side == Side.Home ? (int)(slot.X + slot.Width) - Hud.Measure(text) : (int)slot.X;
            Hud.DrawText(text, x, (int)slot.Y - 2, colour);
        }

        /// <summary>Draws text centred in a card slot such as home_name.</summary>
        public void DrawInSlot(string slot, string text, Color colour)
        {
            Hud.DrawInSlot(text, Slot(slot), colour);
        }

        /// <summary>Draws one form dot: win, loss, otloss or none.</summary>
        public void DrawForm(string result, Rectangle at)
        {
            DrawTextureRec(icons, formCells[result], new(at.X, at.Y), Color.White);
        }

        /// <summary>Draws a playstyle badge (see <see cref="StyleKey(PlayStyle)"/>).</summary>
        public void DrawStyle(string key, Rectangle at)
        {
            if (styleCells.TryGetValue(key, out Rectangle src))
            {
                DrawTextureRec(icons, src, new(at.X, at.Y), Color.White);
            }
        }

        /// <summary>Releases the textures.</summary>
        public void Unload()
        {
            UnloadTexture(card);
            UnloadTexture(icons);
        }

        private Rectangle Screen(Rectangle r)
        {
            return new Rectangle(Position.X + r.X, Position.Y + r.Y, r.Width, r.Height);
        }

        private static string Prefix(Side side)
        {
            return side == Side.Home ? "home" : "away";
        }

        private static string Key(string row, string key)
        {
            return row + "." + key;
        }

        private static string Camel(string name)
        {
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static Color Lighter(Color c)
        {
            return new Color(
                (byte)Math.Min(255, c.R + 40),
                (byte)Math.Min(255, c.G + 40),
                (byte)Math.Min(255, c.B + 40),
                c.A
            );
        }

        private static Rectangle Rect(JsonElement a)
        {
            return new Rectangle(
                a[0].GetInt32(),
                a[1].GetInt32(),
                a[2].GetInt32(),
                a[3].GetInt32()
            );
        }

        private static void Read(JsonElement cells, Dictionary<string, Rectangle> into)
        {
            foreach (JsonProperty cell in cells.EnumerateObject())
            {
                into[cell.Name] = Rect(cell.Value);
            }
        }
    }
}
