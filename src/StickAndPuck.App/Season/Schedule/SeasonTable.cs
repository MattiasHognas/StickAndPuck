using System.Collections.Immutable;
using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>A plain standings panel in the A3 pixel font: rank, club, games, regulation and overtime wins
    /// and losses, goals and points, with the human's club in gold. The standings
    /// screen uses the same <see cref="SeasonState.Table"/> and the same header: the round of the human's next
    /// game, or the final table once the season is over.</summary>
    internal static class SeasonTable
    {
        private const int Line = 12,
            Pad = 6;

        private static readonly ImmutableArray<(string Head, int X)> Columns =
        [
            (K.ColGp, 150),
            (K.ColW, 174),
            (K.ColOtw, 194),
            (K.ColOtl, 222),
            (K.ColL, 250),
            (K.ColGoals, 268),
            (K.ColPts, 314),
        ];

        /// <summary>Draws the table centred on the 480x270 frame.</summary>
        public static void Draw(HudArt art, SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            if (season.League == League.Nhl)
            {
                DrawNhl(art, season, clubs);
                return;
            }

            IReadOnlyList<StandingRow> table = season.Table;
            int w = 340,
                h = (Pad * 2) + ((table.Count + 2) * Line);
            int x = (Game.Width - w) / 2,
                y = Math.Max(2, (Game.Height - h) / 2);
            art.DrawPanel("panel", x, y, w, h);

            int top = y + Pad - 2;
            string title = LeagueText
                .Of(season.League)
                .TableHeading(season.IsOver, Screens.StandingsScreen.Round(season), season.Rounds);
            art.DrawText(title, x + Pad + 4, top, art.Score);
            top += Line;
            foreach ((string head, int cx) in Columns)
            {
                art.DrawText(Text.Get(head), x + cx, top, art.Text);
            }

            for (int i = 0; i < table.Count; i++)
            {
                StandingRow r = table[i];
                top += Line;
                Color ink = string.Equals(r.ClubId, season.UserClub, StringComparison.Ordinal)
                    ? art.Score
                    : art.Text;
                string name = clubs
                    .FirstOrNone(c => string.Equals(c.Id, r.ClubId, StringComparison.Ordinal))
                    .Select(c => c.Name)
                    .Or(r.ClubId);
                art.DrawText(Num(i + 1), x + Pad + 4, top, ink);
                art.DrawText(name, x + Pad + 22, top, ink);
                string[] cells =
                [
                    Num(r.Played),
                    Num(r.Wins),
                    Num(r.OvertimeWins),
                    Num(r.OvertimeLosses),
                    Num(r.Losses),
                    string.Create(CultureInfo.InvariantCulture, $"{r.GoalsFor}-{r.GoalsAgainst}"),
                    Num(r.Points),
                ];
                for (int c = 0; c < cells.Length; c++)
                {
                    art.DrawText(cells[c], x + Columns[c].X, top, ink);
                }
            }
        }

        /// <summary>The NHL standings by division (<see cref="NhlStandingsDraw"/>) under the same header.</summary>
        private static void DrawNhl(HudArt art, SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            int w = 468,
                h = (Pad * 2) + ((NhlStandingsDraw.Lines(season) + 1) * Line);
            int x = (Game.Width - w) / 2,
                y = Math.Max(2, (Game.Height - h) / 2);
            art.DrawPanel("panel", x, y, w, h);
            string title = LeagueText
                .Of(season.League)
                .TableHeading(season.IsOver, Screens.StandingsScreen.Round(season), season.Rounds);
            art.DrawText(title, x + Pad + 4, y + Pad - 2, art.Score);
            _ = NhlStandingsDraw.Draw(
                art,
                season,
                clubs,
                x + Pad,
                y + Pad - 2 + Line,
                w - (Pad * 2),
                Line,
                new NhlStandingsDraw.Inks(art.Text, art.Score, Raylib.ColorAlpha(art.Text, 0.5f))
            );
        }

        private static string Num(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
