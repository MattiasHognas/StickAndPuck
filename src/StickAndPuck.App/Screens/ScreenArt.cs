using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Screens
{
    /// <summary>The A3 art the screens draw with: the HUD's <see cref="HudArt"/> (font, panels, crests, icons) plus
    /// the title logo, and a few layout helpers. Everything lands on whole pixels at 480x270.</summary>
    internal sealed class ScreenArt
    {
        /// <summary>The height of one menu row.</summary>
        public const int Row = 14;

        /// <summary>Muted text for items that cannot be picked (palette grey).</summary>
        public static readonly Color Muted = new(0x8a, 0x87, 0x90, 0xff);

        private static readonly Color FormUp = new(0x56, 0x96, 0x5c, 0xff);
        private static readonly Color FormDown = new(0xe9, 0x6f, 0x5d, 0xff);

        // the HUD target blends alpha into itself, so this reads as about half dark over the rink
        private static readonly Color Shade = new(0x18, 0x13, 0x1f, 0xd8);
        private static readonly Color Band = new(0x18, 0x13, 0x1f, 0xff);

        // the dressing room under its panel, multiplied down to about a third so cream text reads over it
        private static readonly Color RoomDim = new(0x40, 0x3c, 0x4c, 0xff);
        private static readonly Color PanelLight = new(0x33, 0x29, 0x3a, 0xff);

        private Texture2D title;
        private Maybe<Texture2D> dressingRoom;
        private Maybe<Texture2D> coach;
        private Maybe<Texture2D> coachMoods;
        private Maybe<Texture2D> roomJersey;
        private Maybe<Texture2D> roomTrim;

        private ScreenArt(HudArt hud)
        {
            Hud = hud;
        }

        public HudArt Hud { get; }

        /// <summary>Gets the cream text colour.</summary>
        public Color Text => Hud.Text;

        /// <summary>Gets the gold highlight colour.</summary>
        public Color Gold => Hud.Score;

        /// <summary>Loads the art. Needs an open window.</summary>
        public static ScreenArt Load()
        {
            ScreenArt art = new(HudArt.Load())
            {
                title = ResourceLoader.Texture("art.ui.title.png"),
                dressingRoom = Optional(Assets.DressingRoomBackdrop),
                coach = Optional(Assets.CoachPortrait),
                coachMoods = Optional(Assets.CoachMoods),
                roomJersey = Optional(Assets.DressingRoomJersey),
                roomTrim = Optional(Assets.DressingRoomTrim),
            };
            return art;
        }

        /// <summary>Draws the dressing room backdrop over the whole frame (A20), the jerseys in the stalls
        /// tinted with a kit's colours; false while there is none yet.</summary>
        public bool DrawDressingRoom(KitColours kit)
        {
            if (!dressingRoom.HasValue)
            {
                return false;
            }

            DrawRoom(kit, new Rectangle(0, 0, Game.Width, Game.Height), Color.White);
            return true;
        }

        /// <summary>Draws a panel over the dressing room backdrop: the room shows through it, dimmed, so the
        /// text over it stays readable (an opaque draw, so the rink never shows through the HUD target).
        /// Returns where the body starts, like <see cref="Panel"/>.</summary>
        public (int X, int Top) RoomPanel(KitColours kit, string heading, int w, int h, int y)
        {
            int x = (Game.Width - w) / 2;
            DrawRoom(kit, new Rectangle(x, y, w, h), RoomDim);
            DrawRectangleLines(x, y, w, h, Band);
            DrawRectangle(x + 1, y + 1, w - 2, 1, PanelLight);
            DrawRectangle(x + 1, y + 1, 1, h - 2, PanelLight);
            if (heading.Length == 0)
            {
                return (x, y + 6);
            }

            Centred(heading, Game.Width / 2, y + 5, Gold);
            return (x, y + 5 + Row + 2);
        }

        /// <summary>One region of the backdrop and its jersey masks, every layer multiplied by a shade.</summary>
        private void DrawRoom(KitColours kit, Rectangle area, Color shade)
        {
            if (!dressingRoom.TryGet(out Texture2D room))
            {
                return;
            }

            Vector2 at = new(area.X, area.Y);
            DrawTextureRec(room, area, at, shade);
            if (roomJersey.TryGet(out Texture2D jersey))
            {
                DrawTextureRec(jersey, area, at, Multiply(HudArt.Hex(kit.Primary), shade));
            }

            if (roomTrim.TryGet(out Texture2D trim))
            {
                DrawTextureRec(trim, area, at, Multiply(HudArt.Hex(kit.Secondary), shade));
            }
        }

        private static Color Multiply(Color a, Color b)
        {
            return new Color(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, 255);
        }

        /// <summary>Draws the coach's portrait in a box (A20), or until it exists a framed placeholder: a dark
        /// box with a stick icon and the label under it.</summary>
        public void DrawCoach(int x, int y, int w, int h, string label)
        {
            DrawCoach(x, y, w, h, label, CoachMood.Neutral);
        }

        /// <summary>Draws the coach's portrait with a mood (A20's cell in <see cref="Assets.CoachMoods"/>), the
        /// plain portrait when there is no mood sheet, or the framed placeholder.</summary>
        public void DrawCoach(int x, int y, int w, int h, string label, CoachMood mood)
        {
            if (coachMoods.TryGet(out Texture2D moods))
            {
                int cell = moods.Width / Enum.GetValues<CoachMood>().Length;
                DrawTexturePro(
                    moods,
                    new Rectangle((int)mood * cell, 0, cell, moods.Height),
                    new Rectangle(x, y, w, h),
                    new(0, 0),
                    0,
                    Color.White
                );
                return;
            }

            if (coach.TryGet(out Texture2D tex))
            {
                DrawTexturePro(
                    tex,
                    new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(x, y, w, h),
                    new(0, 0),
                    0,
                    Color.White
                );
                return;
            }

            Hud.DrawPanel("panel_gold", x, y, w, h);
            DrawRectangle(x + 3, y + 3, w - 6, h - 6, Band);
            Hud.DrawIcon("stick", x + ((w - 8) / 2), y + ((h - 8) / 2) - 6);
            Centred(label, x + (w / 2), y + h - 16, Muted);
        }

        private static Maybe<Texture2D> Optional(string path)
        {
            return ResourceLoader.Exists(path)
                ? ResourceLoader.Texture(path)
                : Maybe.None<Texture2D>();
        }

        /// <summary>Darkens the rink behind a screen.</summary>
        public static void Dim()
        {
            DrawRectangle(0, 0, Game.Width, Game.Height, Shade);
        }

        /// <summary>Draws the Stick and Puck logo centred at the given top.</summary>
        public void DrawTitle(int y)
        {
            DrawTexture(title, (Game.Width - title.Width) / 2, y, Color.White);
        }

        /// <summary>Draws text centred on a column.</summary>
        public void Centred(string text, int cx, int y, Color colour)
        {
            text = Fit(text);
            Hud.DrawText(text, cx - (Hud.Measure(text) / 2), y, colour);
        }

        /// <summary>Draws text with its right edge on a column.</summary>
        public void Right(string text, int right, int y, Color colour)
        {
            text = Fit(text);
            Hud.DrawText(text, right - Hud.Measure(text), y, colour);
        }

        /// <summary>Draws text with its left edge on a column.</summary>
        public void Left(string text, int x, int y, Color colour)
        {
            Hud.DrawText(Fit(text), x, y, colour);
        }

        /// <summary>Shortens text to a width, ending it with a dot when letters had to go.</summary>
        public string Clip(string text, int width)
        {
            return Hud.Clip(text, width);
        }

        /// <summary>A club's name in a width: the full name when it fits, else the name without its city
        /// ("Columbus Blue Jackets" shows BLUE JACKETS), shortened with a dot only when even that is too long.</summary>
        public string ClubName(string name, int width)
        {
            return Hud.Measure(name) <= width
                ? Hud.Printable(name)
                : Clip(TeamSelectScreen.Nickname(name), width);
        }

        /// <summary>Gets the ink of a season player's development mark (W132): green for "+2", red for "-1", the form arrow's colours.</summary>
        public static Color DevelopmentInk(int points)
        {
            return points > 0 ? FormUp : FormDown;
        }

        /// <summary>Draws a season player's form arrow with its left edge on a column: a green arrow up for a
        /// positive nudge, a red one down for a negative, nothing when level. Returns its width.</summary>
        public static int FormArrow(int nudge, int x, int y)
        {
            if (nudge == 0)
            {
                return 0;
            }

            if (nudge > 0)
            {
                DrawTriangle(new(x + 3, y), new(x, y + 4), new(x + 6, y + 4), FormUp);
                DrawRectangle(x + 2, y + 4, 3, 3, FormUp);
            }
            else
            {
                DrawRectangle(x + 2, y, 3, 3, FormDown);
                DrawTriangle(new(x, y + 3), new(x + 3, y + 7), new(x + 6, y + 3), FormDown);
            }
            return 7;
        }

        /// <summary>Draws a 0 to 100 rating as a bar with its number on the right.</summary>
        public void Bar(int value, int x, int y, int w, Color ink)
        {
            value = Math.Clamp(value, 0, 100);
            DrawRectangle(x, y + 3, w, 5, Band);
            DrawRectangle(x, y + 3, Math.Max(1, value * w / 100), 5, ink);
            Left(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x + w + 4,
                y,
                Text
            );
        }

        /// <summary>Upper-cases text. The letters the pixel font lacks are swapped for their plain form (Á becomes
        /// A) where <see cref="HudArt"/> measures and draws, against the glyphs the font really has, so a
        /// glyph added to the font shows without a change here.</summary>
        public static string Fit(string text)
        {
            return text.ToUpperInvariant();
        }

        /// <summary>Draws a panel with a heading in gold, centred on the frame, and returns where its body starts.</summary>
        public (int X, int Top) Panel(string heading, int w, int h, int y, string panel = "panel")
        {
            int x = (Game.Width - w) / 2;
            Hud.DrawPanel(panel, x, y, w, h);
            if (heading.Length == 0)
            {
                return (x, y + 6);
            }

            Centred(heading, Game.Width / 2, y + 5, Gold);
            return (x, y + 5 + Row + 2);
        }

        /// <summary>Draws a menu: one row per item, the selected one in gold with an arrow, values right-aligned with arrows either side.</summary>
        public void Menu(IReadOnlyList<MenuItem> items, int selected, int x, int w, int top)
        {
            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                int y = top + (i * Row);
                bool on = i == selected;
                Color ink = (!item.Enabled) ? Muted : (on ? Gold : Text);
                if (on)
                {
                    Hud.DrawIcon("arrow_right", x + 6, y + 2);
                }

                Left(item.Label, x + 18, y, ink);
                if (item.Value.HasValue)
                {
                    string value = item.Value.Or("");
                    string shown = on ? $"< {value} >" : value;
                    Right(shown, x + w - 10, y, ink);
                }
            }
        }

        /// <summary>Draws the key hints along the bottom edge.</summary>
        public void Hint(string hint)
        {
            DrawRectangle(0, Game.Height - 15, Game.Width, 15, Band);
            Centred(hint, Game.Width / 2, Game.Height - 13, Muted);
        }

        /// <summary>Releases the textures.</summary>
        public void Unload()
        {
            UnloadTexture(title);
            if (dressingRoom.TryGet(out Texture2D room))
            {
                UnloadTexture(room);
            }

            if (coach.TryGet(out Texture2D portrait))
            {
                UnloadTexture(portrait);
            }

            foreach (Maybe<Texture2D> extra in new[] { coachMoods, roomJersey, roomTrim })
            {
                if (extra.TryGet(out Texture2D t))
                {
                    UnloadTexture(t);
                }
            }

            Hud.Unload();
        }
    }
}
