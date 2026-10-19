using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>The NHL bracket: seven columns, the first conference's first round on the left working in
    /// to the final in the middle and the second conference's first round on the right, each series
    /// as its two clubs with their playoff tag (A1, WC2) and wins, the human's club in gold and series
    /// losers dimmed. Rounds not drawn yet show as empty slots. Used by the bracket screen and the
    /// bracket panel after a playoff game.</summary>
    internal static class NhlBracketDraw
    {
        private const int Columns = 7;

        /// <summary>Draws the bracket in an area: the round names on the first line, the series below it.</summary>
        public static void Draw(
            HudArt art,
            PlayoffState playoffs,
            IReadOnlyList<ClubData> clubs,
            int x,
            int top,
            int width,
            int height,
            int line,
            (Color Ink, Color Gold, Color Muted) inks
        )
        {
            int column = width / Columns;
            string[] titles =
            [
                Text.Get(K.RoundFirstShort),
                Text.Get(K.RoundSecondShort),
                Text.Get(K.RoundConferenceShort),
                Text.Get(K.RoundFinal),
            ];
            for (int c = 0; c < Columns; c++)
            {
                int round = Math.Min(c, Columns - 1 - c);
                bool drawn = round < playoffs.Rounds.Count;
                Centred(
                    art,
                    titles[round],
                    x + (c * column) + (column / 2),
                    top,
                    drawn ? inks.Ink : inks.Muted
                );
            }

            int area = top + line + 2,
                areaH = height - line - 2;
            DrawConferences(art, playoffs, x, column, area, inks.Gold);
            for (int round = 0; round < PlayoffState.RoundCount; round++)
            {
                int slots = 8 >> round; // series in the round
                for (int s = 0; s < slots; s++)
                {
                    // the first half of the round's series is the first conference, drawn from the left
                    bool left = s < slots / 2 || round == PlayoffState.RoundCount - 1;
                    int c =
                        round == PlayoffState.RoundCount - 1
                            ? 3
                            : (left ? round : Columns - 1 - round);
                    int perSide = Math.Max(1, slots / 2),
                        index = left ? s : s - (slots / 2);
                    int slotH = areaH / perSide;
                    int sy = area + (index * slotH) + (slotH / 2) - line;
                    Maybe<PlayoffSeries> series =
                        round < playoffs.Rounds.Count && s < playoffs.Rounds[round].Series.Count
                            ? playoffs.Rounds[round].Series[s]
                            : Maybe.None<PlayoffSeries>();
                    DrawSeries(
                        art,
                        playoffs,
                        series,
                        clubs,
                        x + (c * column),
                        column,
                        sy,
                        line,
                        inks
                    );
                }
            }
        }

        /// <summary>The conference names over the inner columns of each side, where the second round leaves room.</summary>
        private static void DrawConferences(
            HudArt art,
            PlayoffState playoffs,
            int x,
            int column,
            int y,
            Color gold
        )
        {
            if (playoffs.Conferences.Count < 2)
            {
                return;
            }

            Centred(art, playoffs.Conferences[0].ToUpperInvariant(), x + (2 * column), y, gold);
            Centred(art, playoffs.Conferences[1].ToUpperInvariant(), x + (5 * column), y, gold);
        }

        private static void DrawSeries(
            HudArt art,
            PlayoffState playoffs,
            Maybe<PlayoffSeries> maybe,
            IReadOnlyList<ClubData> clubs,
            int x,
            int column,
            int y,
            int line,
            (Color Ink, Color Gold, Color Muted) inks
        )
        {
            if (!maybe.TryGet(out PlayoffSeries series))
            {
                Left(art, "-", x + 22, y, inks.Muted);
                Left(art, "-", x + 22, y + line, inks.Muted);
                return;
            }

            DrawSide(
                art,
                playoffs,
                series,
                series.High,
                series.HighWins,
                clubs,
                x,
                column,
                y,
                inks
            );
            DrawSide(
                art,
                playoffs,
                series,
                series.Low,
                series.LowWins,
                clubs,
                x,
                column,
                y + line,
                inks
            );
        }

        private static void DrawSide(
            HudArt art,
            PlayoffState playoffs,
            PlayoffSeries series,
            string club,
            int wins,
            IReadOnlyList<ClubData> clubs,
            int x,
            int column,
            int y,
            (Color Ink, Color Gold, Color Muted) inks
        )
        {
            Color ink = string.Equals(club, playoffs.UserClub, StringComparison.Ordinal)
                ? inks.Gold
                : inks.Ink;
            if (
                series.IsOver
                && !series.Winner.Is(w => string.Equals(w, club, StringComparison.Ordinal))
            )
            {
                ink = Raylib.ColorAlpha(ink, 0.4f);
            }

            Right(art, Schedule.NhlStandingsDraw.TagText(playoffs.TagOf(club)), x + 19, y, ink);
            Left(art, Schedule.NhlStandingsDraw.Short(clubs, club), x + 22, y, ink);
            Right(art, wins.ToString(CultureInfo.InvariantCulture), x + column - 4, y, ink);
        }

        private static void Left(HudArt art, string text, int x, int y, Color ink)
        {
            art.DrawText(ScreenArt.Fit(text), x, y, ink);
        }

        private static void Right(HudArt art, string text, int right, int y, Color ink)
        {
            text = ScreenArt.Fit(text);
            art.DrawText(text, right - art.Measure(text), y, ink);
        }

        private static void Centred(HudArt art, string text, int cx, int y, Color ink)
        {
            text = ScreenArt.Fit(text);
            art.DrawText(text, cx - (art.Measure(text) / 2), y, ink);
        }
    }
}
