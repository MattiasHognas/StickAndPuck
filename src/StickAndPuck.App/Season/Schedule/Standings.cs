using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>Works out the league table from the played games, under the season's rules:
    /// <see cref="RuleSet.TablePoints"/> sets the points a game gives and <see cref="RuleSet.Tiebreak"/>
    /// orders clubs level on points.</summary>
    public static class Standings
    {
        /// <summary>The table under SHL rules, best first.</summary>
        public static IReadOnlyList<StandingRow> Compute(
            IEnumerable<string> clubIds,
            IEnumerable<Fixture> games
        )
        {
            return Compute(clubIds, games, RuleSet.Shl);
        }

        /// <summary>The table, best first: points, then the rules' tiebreak (SHL: goal difference, goals
        /// scored, head-to-head points; NHL: regulation wins, all wins, head-to-head points, goal
        /// difference), then goal difference, goals scored, regulation wins and club id so the order
        /// never flickers. Head-to-head counts the points in the games between the clubs still level.</summary>
        public static IReadOnlyList<StandingRow> Compute(
            IEnumerable<string> clubIds,
            IEnumerable<Fixture> games,
            RuleSet rules
        )
        {
            List<(Fixture Game, FixtureResult Result)> played = [];
            foreach (Fixture g in games)
            {
                if (g.Result.TryGet(out FixtureResult r))
                {
                    played.Add((g, r));
                }
            }

            Dictionary<string, StandingRow> rows = clubIds.ToDictionary(
                id => id,
                id => new StandingRow(id, 0, 0, 0, 0, 0, 0, 0, 0),
                StringComparer.Ordinal
            );
            foreach ((Fixture g, FixtureResult r) in played)
            {
                if (rows.TryGetValue(g.Home, out StandingRow home))
                {
                    rows[g.Home] = Add(
                        home,
                        r.HomeGoals,
                        r.AwayGoals,
                        r.PointsFor(home: true, rules.TablePoints),
                        r.Kind
                    );
                }

                if (rows.TryGetValue(g.Away, out StandingRow away))
                {
                    rows[g.Away] = Add(
                        away,
                        r.AwayGoals,
                        r.HomeGoals,
                        r.PointsFor(home: false, rules.TablePoints),
                        r.Kind
                    );
                }
            }

            List<StandingRow> table = [];
            foreach (
                IGrouping<(int, int, int), StandingRow> level in rows
                    .Values.GroupBy(r => Level(r, rules.Tiebreak))
                    .OrderByDescending(g => g.Key)
            )
            {
                Dictionary<string, int> between = HeadToHead(
                    level.Select(r => r.ClubId),
                    played,
                    rules.TablePoints
                );
                table.AddRange(
                    level
                        .OrderByDescending(r => between.GetValueOrDefault(r.ClubId))
                        .ThenByDescending(r => r.GoalDifference)
                        .ThenByDescending(r => r.GoalsFor)
                        .ThenByDescending(r => r.Wins)
                        .ThenBy(r => r.ClubId, StringComparer.Ordinal)
                );
            }
            return table;
        }

        /// <summary>The points each club took in the games between the given clubs only.</summary>
        private static Dictionary<string, int> HeadToHead(
            IEnumerable<string> clubIds,
            IEnumerable<(Fixture Game, FixtureResult Result)> played,
            TablePoints system
        )
        {
            Dictionary<string, int> points = clubIds.ToDictionary(
                id => id,
                _ => 0,
                StringComparer.Ordinal
            );
            if (points.Count < 2)
            {
                return points;
            }

            foreach ((Fixture g, FixtureResult r) in played)
            {
                if (points.ContainsKey(g.Home) && points.ContainsKey(g.Away))
                {
                    points[g.Home] += r.PointsFor(home: true, system);
                    points[g.Away] += r.PointsFor(home: false, system);
                }
            }
            return points;
        }

        /// <summary>What must be equal before head-to-head is looked at: points, then the tiebreak's keys ahead of it.</summary>
        private static (int, int, int) Level(StandingRow r, TableTiebreak tiebreak)
        {
            return tiebreak switch
            {
                TableTiebreak.GoalDifference => (r.Points, r.GoalDifference, r.GoalsFor),
                TableTiebreak.RegulationWins => (r.Points, r.Wins, r.Wins + r.OvertimeWins),
                TableTiebreak.HeadToHead => (r.Points, 0, 0),
                _ => throw new NotSupportedException("no standings order for " + tiebreak),
            };
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
