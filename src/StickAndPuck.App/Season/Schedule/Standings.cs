namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>Works out the SHL table from the played games.</summary>
    public static class Standings
    {
        /// <summary>The table, best first: points, then goal difference, then goals scored, then regulation
        /// wins, then club id so the order never flickers.</summary>
        public static IReadOnlyList<StandingRow> Compute(
            IEnumerable<string> clubIds,
            IEnumerable<Fixture> games
        )
        {
            Dictionary<string, StandingRow> rows = clubIds.ToDictionary(
                id => id,
                id => new StandingRow(id, 0, 0, 0, 0, 0, 0, 0, 0),
                StringComparer.Ordinal
            );
            foreach (Fixture g in games)
            {
                if (!g.Result.TryGet(out FixtureResult r))
                {
                    continue;
                }

                if (rows.TryGetValue(g.Home, out StandingRow home))
                {
                    rows[g.Home] = Add(home, r.HomeGoals, r.AwayGoals, r.HomePoints, r.Kind);
                }

                if (rows.TryGetValue(g.Away, out StandingRow away))
                {
                    rows[g.Away] = Add(away, r.AwayGoals, r.HomeGoals, r.AwayPoints, r.Kind);
                }
            }
            return
            [
                .. rows
                    .Values.OrderByDescending(r => r.Points)
                    .ThenByDescending(r => r.GoalDifference)
                    .ThenByDescending(r => r.GoalsFor)
                    .ThenByDescending(r => r.Wins)
                    .ThenBy(r => r.ClubId, StringComparer.Ordinal),
            ];
        }

        private static StandingRow Add(
            StandingRow row,
            int scored,
            int conceded,
            int points,
            ResultKind kind
        )
        {
            bool won = scored > conceded,
                lost = scored < conceded,
                extra = kind != ResultKind.Regulation;
            return row with
            {
                Played = row.Played + 1,
                Wins = row.Wins + (won && !extra ? 1 : 0),
                OvertimeWins = row.OvertimeWins + (won && extra ? 1 : 0),
                OvertimeLosses = row.OvertimeLosses + (lost && extra ? 1 : 0),
                Losses = row.Losses + (lost && !extra ? 1 : 0),
                GoalsFor = row.GoalsFor + scored,
                GoalsAgainst = row.GoalsAgainst + conceded,
                Points = row.Points + points,
            };
        }
    }
}
