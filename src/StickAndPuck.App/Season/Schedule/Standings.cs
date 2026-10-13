namespace StickAndPuck.App.Season;

/// <summary>One club's line in the table.</summary>
public sealed record StandingRow(
    string ClubId, int Played, int Wins, int OvertimeWins, int OvertimeLosses, int Losses, int GoalsFor, int GoalsAgainst, int Points)
{
    /// <summary>Gets the goal difference.</summary>
    public int GoalDifference => GoalsFor - GoalsAgainst;
}

/// <summary>Works out the SHL table from the played games.</summary>
public static class Standings
{
    /// <summary>The table, best first: points, then goal difference, then goals scored, then regulation
    /// wins, then club id so the order never flickers.</summary>
    public static IReadOnlyList<StandingRow> Compute(IEnumerable<string> clubIds, IEnumerable<Fixture> games)
    {
        var rows = clubIds.ToDictionary(id => id, id => new StandingRow(id, 0, 0, 0, 0, 0, 0, 0, 0), StringComparer.Ordinal);
        foreach (var g in games)
        {
            if (!g.Result.TryGet(out var r)) continue;
            if (rows.TryGetValue(g.Home, out var home)) rows[g.Home] = Add(home, r.HomeGoals, r.AwayGoals, r.HomePoints, r.Kind);
            if (rows.TryGetValue(g.Away, out var away)) rows[g.Away] = Add(away, r.AwayGoals, r.HomeGoals, r.AwayPoints, r.Kind);
        }
        return rows.Values
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.GoalDifference)
            .ThenByDescending(r => r.GoalsFor)
            .ThenByDescending(r => r.Wins)
            .ThenBy(r => r.ClubId, StringComparer.Ordinal)
            .ToList();
    }

    static StandingRow Add(StandingRow row, int scored, int conceded, int points, ResultKind kind)
    {
        bool won = scored > conceded, lost = scored < conceded, extra = kind != ResultKind.Regulation;
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
