using StickAndPuck.App.Season.Playoffs;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>The NHL standings as the wild card view shows them (W118): per conference, the division
    /// leaders and the wild card race (<see cref="WildCardTable"/>). Picks the same clubs as the playoff
    /// field (<see cref="NhlSeeding"/>).</summary>
    public static class WildCardStandings
    {
        /// <summary>The conferences by name; empty when the season's clubs have no divisions.</summary>
        public static IReadOnlyList<WildCardTable> Of(
            SeasonState season,
            IReadOnlyList<StandingRow> table
        )
        {
            Dictionary<string, int> place = [with(StringComparer.Ordinal)];
            for (int i = 0; i < table.Count; i++)
            {
                place[table[i].ClubId] = i;
            }

            return
            [
                .. DivisionStandings
                    .Of(season, table)
                    .GroupBy(d => d.Conference, StringComparer.Ordinal)
                    .Select(conference => new WildCardTable(
                        conference.Key,
                        [
                            .. conference
                                .OrderBy(d => place[d.Rows[0].ClubId])
                                .Select(d =>
                                    d with
                                    {
                                        Rows = [.. d.Rows.Take(NhlSeeding.PerDivision)],
                                    }
                                ),
                        ],
                        [
                            .. conference
                                .SelectMany(d => d.Rows.Skip(NhlSeeding.PerDivision))
                                .OrderBy(r => place[r.ClubId]),
                        ]
                    )),
            ];
        }
    }
}
