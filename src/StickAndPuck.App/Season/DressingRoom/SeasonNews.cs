using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>Placeholder news from the season itself, at priority 0: the club's place in the table, a
    /// streak of two or more wins or losses, and in the playoffs the score of the club's series. The news
    /// deck (W130) adds its events over these.</summary>
    internal sealed class SeasonNews : IDressingRoomSource
    {
        /// <summary>The shortest run of wins or losses that makes the news.</summary>
        public const int StreakNews = 2;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            SeasonState season = context.Season;
            string user = context.UserClub;
            if (
                season.Playoffs.TryGet(out PlayoffState playoffs)
                && Series(playoffs, user).TryGet(out (int Round, PlayoffSeries Series) found)
            )
            {
                PlayoffSeries series = found.Series;
                bool high = string.Equals(series.High, user, StringComparison.Ordinal);
                report.AddNews(
                    new DressingRoomEntry(
                        Text.Format(
                            K.NewsSeries,
                            PlayoffState.RoundTitle(found.Round),
                            high ? series.HighWins : series.LowWins,
                            high ? series.LowWins : series.HighWins
                        ),
                        Highlight: true
                    )
                );
            }

            IReadOnlyList<StandingRow> table = season.Table;
            for (int i = 0; i < table.Count; i++)
            {
                if (
                    string.Equals(table[i].ClubId, user, StringComparison.Ordinal)
                    && table[i].Played > 0
                )
                {
                    report.AddNews(
                        new DressingRoomEntry(
                            Text.Format(K.NewsTablePlace, i + 1, table.Count, table[i].Points)
                        )
                    );
                }
            }

            int streak = Streak(season, user);
            if (Math.Abs(streak) >= StreakNews)
            {
                report.AddNews(
                    new DressingRoomEntry(
                        Text.Format(
                            streak > 0 ? K.NewsWinStreak : K.NewsLossStreak,
                            Math.Abs(streak)
                        )
                    )
                );
            }
        }

        /// <summary>Gets the club's run of regular-season results, newest back: positive for wins in a row,
        /// negative for losses in a row (an overtime loss is a loss), 0 before the first game.</summary>
        public static int Streak(SeasonState season, string clubId)
        {
            int run = 0;
            foreach (Fixture f in season.PlayedBy(clubId).OrderByDescending(f => f.Number))
            {
                if (!f.Result.TryGet(out FixtureResult r))
                {
                    continue;
                }

                bool home = string.Equals(f.Home, clubId, StringComparison.Ordinal);
                int own = home ? r.HomeGoals : r.AwayGoals,
                    other = home ? r.AwayGoals : r.HomeGoals;
                int step = own > other ? 1 : -1;
                if (run != 0 && Math.Sign(run) != step)
                {
                    break;
                }

                run += step;
            }

            return run;
        }

        /// <summary>The club's latest playoff series with a game played, and its round.</summary>
        private static Maybe<(int Round, PlayoffSeries Series)> Series(
            PlayoffState playoffs,
            string clubId
        )
        {
            for (int r = playoffs.Rounds.Count - 1; r >= 0; r--)
            {
                if (
                    playoffs
                        .Rounds[r]
                        .Series.FirstOrNone(s => s.Involves(clubId) && s.Games.Count > 0)
                        .TryGet(out PlayoffSeries series)
                )
                {
                    return (r, series);
                }
            }

            return Maybe.None<(int, PlayoffSeries)>();
        }
    }
}
