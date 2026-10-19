namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>Splits a season's table into its conferences and divisions (the NHL standings), keeping the
    /// table's order inside each, so the points system and tiebreaks of the whole table hold there too.</summary>
    public static class DivisionStandings
    {
        /// <summary>The divisions, conferences by name and divisions by name inside them; empty when the
        /// season's clubs have no divisions.</summary>
        public static IReadOnlyList<DivisionTable> Of(
            SeasonState season,
            IReadOnlyList<StandingRow> table
        )
        {
            return
            [
                .. table
                    .Where(r => season.Divisions.ContainsKey(r.ClubId))
                    .GroupBy(r =>
                        (
                            Conference: season.Conferences.TryGetValue(r.ClubId, out string c)
                                ? c
                                : "",
                            Division: season.Divisions[r.ClubId]
                        )
                    )
                    .OrderBy(g => g.Key.Conference, StringComparer.Ordinal)
                    .ThenBy(g => g.Key.Division, StringComparer.Ordinal)
                    .Select(g => new DivisionTable(g.Key.Conference, g.Key.Division, [.. g])),
            ];
        }
    }
}
