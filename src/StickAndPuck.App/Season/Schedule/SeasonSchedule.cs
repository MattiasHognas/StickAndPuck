namespace StickAndPuck.App.Season;

/// <summary>
/// Builds the SHL double round robin twice over: every club meets every other club four times,
/// twice at home and twice away, one game per club per round. 14 clubs give 52 rounds of 7 games
/// and 52 games a club. An odd number of clubs gives each club a bye per cycle.
/// </summary>
public static class SeasonSchedule
{
    /// <summary>How many times each pair of clubs meets; half at each rink.</summary>
    public const int Meetings = 4;

    /// <summary>Builds the schedule.</summary>
    /// <param name="clubIds">The clubs' ids, at least two.</param>
    /// <param name="seed">Shuffles who meets whom when, so each new season has its own order.</param>
    /// <returns>Every game in round order, numbered from 0.</returns>
    public static IReadOnlyList<Fixture> Build(IReadOnlyList<string> clubIds, int seed)
    {
        if (clubIds.Count < 2) throw new ArgumentException("a season needs at least two clubs", nameof(clubIds));
#pragma warning disable S2245 // seeded schedule order, not security
        var dice = new Random(seed);
#pragma warning restore S2245
        var order = clubIds.OrderBy(_ => dice.Next()).ToList();
        if (order.Count % 2 == 1) order.Add("");            // the bye
        var cycle = Cycle(order);

        // the second half of each double round robin swaps every rink; the second double round robin
        // starts a few rounds in, so the season does not open with the same games twice
        int offset = cycle.Count / 2;
        var rounds = new List<List<(string Home, string Away)>>();
        for (int half = 0; half < Meetings; half++)
        {
            bool swap = half % 2 == 1;
            int shift = half >= 2 ? offset : 0;
            for (int r = 0; r < cycle.Count; r++)
                rounds.Add(cycle[(r + shift) % cycle.Count].Select(g => swap ? (g.Away, g.Home) : g).ToList());
        }

        var games = new List<Fixture>();
        for (int r = 0; r < rounds.Count; r++)
        {
            foreach (var (home, away) in rounds[r])
                if (home.Length > 0 && away.Length > 0) games.Add(new Fixture(games.Count, r, home, away));
        }

        return games;
    }

    /// <summary>The circle method: one club stays put, the rest rotate; home and away alternate so each club gets about half.</summary>
    static List<List<(string Home, string Away)>> Cycle(List<string> clubs)
    {
        int n = clubs.Count;
        var ring = clubs.ToList();
        var cycle = new List<List<(string, string)>>();
        for (int r = 0; r < n - 1; r++)
        {
            var round = new List<(string, string)>();
            for (int i = 0; i < n / 2; i++)
            {
                string a = ring[i], b = ring[n - 1 - i];
                // the fixed club alternates by round, the others by round and slot
                bool aHome = i == 0 ? r % 2 == 0 : (r + i) % 2 == 1;
                round.Add(aHome ? (a, b) : (b, a));
            }
            cycle.Add(round);
            // keep ring[0], rotate the rest one step
            string last = ring[n - 1];
            ring.RemoveAt(n - 1);
            ring.Insert(1, last);
        }
        return cycle;
    }
}
