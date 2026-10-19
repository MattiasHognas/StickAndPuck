using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Pregame
{
    /// <summary>Draws the pre-game stats card (<see cref="TeamCard"/>) with the A22 art (<see cref="IntroCardArt"/>):
    /// both crests, names and kit stripes, the five rating bars, the season rows (a grey dash outside a
    /// season), the form dots and the playstyle with its badges.</summary>
    internal static class TeamCardView
    {
        /// <summary>A season row with nothing to show.</summary>
        private const string Dash = "-";

        /// <summary>Draws the card, pushed down by <see cref="IntroCardArt.Slide"/>.</summary>
        public static void Draw(World world, TeamCard data, IntroCardArt card)
        {
            card.DrawCard();
            card.DrawRowIcons();
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                TeamCardSide t = data.Of(side);
                string key = side == Side.Home ? "home" : "away";
                card.DrawCrest(side, t.ClubId);
                card.DrawInSlot(key + "_name", t.Name, card.Text);
                ClubData club = world.Setup.Club(side);
                Rectangle stripe = card.Slot(key + "_stripe");
                KitColours kit = side == Side.Home ? club.Home : club.Away;
                Raylib.DrawRectangle(
                    (int)stripe.X,
                    (int)stripe.Y,
                    (int)stripe.Width,
                    (int)stripe.Height,
                    HudArt.Hex(kit.Primary)
                );

                ClubStrengths st = t.Strengths;
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
                    card.DrawValue(
                        side,
                        row,
                        value.ToString(CultureInfo.InvariantCulture),
                        card.Value
                    );
                }

                Season(card, side, "record", t.Record, card.Text);
                Season(card, side, "place", t.Place, card.Value);
                Season(card, side, "scorer", t.Scorer, card.Text);
                if (t.Form.Count > 0)
                {
                    IReadOnlyList<Rectangle> dots = card.RowRects("form", key + "_dots");
                    for (int i = 0; i < dots.Count && i < t.Form.Count; i++)
                    {
                        card.DrawForm(t.Form[i], dots[i]);
                    }
                }
                else
                {
                    card.DrawValue(side, "form", Dash, card.Muted);
                }

                card.DrawValue(side, "style", ClubStrengths.StyleName(st.Plays.Style), card.Text);
                IReadOnlyList<Rectangle> badges = card.RowRects("style", key + "_badges");
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Style), badges[0]);
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Forecheck), badges[1]);
                card.DrawStyle(IntroCardArt.StyleKey(st.Plays.Entry), badges[2]);
            }
        }

        private static void Season(
            IntroCardArt card,
            Side side,
            string row,
            Maybe<string> value,
            Color colour
        )
        {
            if (value.TryGet(out string text))
            {
                card.DrawValue(side, row, text, colour);
            }
            else
            {
                card.DrawValue(side, row, Dash, card.Muted);
            }
        }
    }
}
