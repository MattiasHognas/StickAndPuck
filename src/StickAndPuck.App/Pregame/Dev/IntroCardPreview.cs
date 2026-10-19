using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Pregame.Dev
{
    /// <summary>Fills the A22 stats card for SAP_SCENE=introcard: both crests and names, the club colour
    /// stripes, the five ratings from <see cref="ClubStrengths"/>, the playstyle badges from the clubs'
    /// tactics, and placeholder record, place, form and top scorer numbers.</summary>
    internal static class IntroCardPreview
    {
        private static readonly string[][] Form =
        [
            ["win", "win", "loss", "otloss", "win"],
            ["loss", "win", "loss", "loss", "none"],
        ];

        /// <summary>Draws the card with its placeholder numbers.</summary>
        public static void Draw(World world, IntroCardArt card)
        {
            card.DrawCard();
            card.DrawRowIcons();
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                ClubData club = world.Setup.Club(side);
                string key = side == Side.Home ? "home" : "away";
                int s = side == Side.Home ? 0 : 1;
                card.DrawCrest(side, club.Id);
                card.DrawInSlot(key + "_name", club.Name, card.Text);
                Rectangle stripe = card.Slot(key + "_stripe");
                KitColours kit = side == Side.Home ? club.Home : club.Away;
                Raylib.DrawRectangle(
                    (int)stripe.X,
                    (int)stripe.Y,
                    (int)stripe.Width,
                    (int)stripe.Height,
                    HudArt.Hex(kit.Primary)
                );

                ClubStrengths st = ClubStrengths.Of(club);
                (string Row, int Value)[] ratings =
                [
                    ("overall", st.Overall),
                    ("attack", st.Attack),
                    ("defence", st.Defence),
                    ("speed", st.Speed),
                    ("goalie", st.Goalie),
                ];
                foreach ((string row, int value) in ratings)
                {
                    card.DrawBar(side, row, value);
                    card.DrawValue(side, row, Num(value), card.Value);
                }

                card.DrawValue(side, "record", s == 0 ? "18-7-5" : "12-14-4", card.Text);
                card.DrawValue(side, "place", s == 0 ? "#2" : "#9", card.Value);
                IReadOnlyList<Rectangle> dots = card.RowRects("form", key + "_dots");
                for (int i = 0; i < dots.Count; i++)
                {
                    card.DrawForm(Form[s][i], dots[i]);
                }

                PlayerData scorer = club
                    .Roster.Where(p => p.Position != PlayingPosition.G)
                    .Skip(s)
                    .First();
                card.DrawValue(
                    side,
                    "scorer",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"#{scorer.Number} {scorer.LastName} {(s == 0 ? 17 : 11)}"
                    ),
                    card.Text
                );

                IReadOnlyList<Rectangle> badges = card.RowRects("style", key + "_badges");
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Style), badges[0]);
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Forecheck), badges[1]);
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Entry), badges[2]);
            }
        }

        private static string Num(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }
    }
}
