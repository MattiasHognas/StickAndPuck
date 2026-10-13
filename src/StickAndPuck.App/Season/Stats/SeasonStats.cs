using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>
/// W25: the season's player stats and records, saved inside season.json with the rest of the
/// season. The regular season and the playoffs each have a <see cref="StatTable"/>; every game
/// is credited once, by key, whether the human played it (from the event log, by
/// <see cref="StatsSystem"/>) or it was settled on paper (from the score, by <see cref="CatchUp"/>),
/// so a save from before this existed fills in when it is next opened. The leaders page reads
/// <see cref="Regular"/>, <see cref="Playoffs"/> and <see cref="Awards"/>.
/// </summary>
public sealed class SeasonStats
{
    /// <summary>Gets or sets the regular season's table.</summary>
    public StatTable Regular { get; set; } = new();

    /// <summary>Gets or sets the playoffs' table.</summary>
    public StatTable Playoffs { get; set; } = new();

    /// <summary>Gets or sets every player of the game so far, oldest first.</summary>
    public List<PlayerOfGameAward> Awards { get; set; } = [];

    /// <summary>Gets or sets the keys of the games credited: "g12" for season game 13, "p0.1.2" for playoff round 1 series 2 game 3.</summary>
    public HashSet<string> Credited { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The key of a regular-season game.</summary>
    /// <param name="number">The fixture number.</param>
    /// <returns>The key.</returns>
    public static string GameKey(int number) => $"g{number}";

    /// <summary>The key of a playoff game.</summary>
    /// <param name="round">The round, from 0.</param>
    /// <param name="series">The series in the round, from 0.</param>
    /// <param name="game">The game in the series, from 0.</param>
    /// <returns>The key.</returns>
    public static string PlayoffKey(int round, int series, int game) => $"p{round}.{series}.{game}";

    /// <summary>Gets the latest award, or None before any game.</summary>
    public Maybe<PlayerOfGameAward> LastAward => Awards.Count == 0 ? Maybe<PlayerOfGameAward>.None : Awards[^1];

    /// <summary>Gets the awards a club's players won.</summary>
    /// <param name="club">The club id.</param>
    /// <returns>Those awards, oldest first.</returns>
    public IEnumerable<PlayerOfGameAward> AwardsOf(string club)
    {
        return Awards.Where(a => string.Equals(a.Club, club, StringComparison.Ordinal));
    }

    /// <summary>Credits one game's sheet, unless that key was credited before.</summary>
    /// <param name="key">The game key.</param>
    /// <param name="sheet">The sheet.</param>
    /// <returns>True when it was credited now.</returns>
    public bool Credit(string key, MatchStatSheet sheet)
    {
        if (!Credited.Add(key)) return false;
        (key.StartsWith('p') ? Playoffs : Regular).Add(sheet);
        if (sheet.Award(key).TryGet(out var award)) Awards.Add(award);
        return true;
    }

    /// <summary>
    /// Credits every played game that has no lines yet from its score, the way the games the
    /// human did not play are settled. Call after a round is simulated, and after a save is read.
    /// </summary>
    /// <param name="season">The season this belongs to.</param>
    /// <param name="clubs">Every club, for rosters.</param>
    /// <returns>The games credited now.</returns>
    public int CatchUp(SeasonState season, IReadOnlyList<ClubData> clubs)
    {
        var byId = clubs.ToDictionary(c => c.Id, StringComparer.Ordinal);
        int count = 0;
        foreach (var g in season.Games)
        {
            if (!g.Result.TryGet(out var result) || Credited.Contains(GameKey(g.Number))) continue;
            if (!byId.TryGetValue(g.Home, out var home) || !byId.TryGetValue(g.Away, out var away)) continue;
            if (Credit(GameKey(g.Number), SyntheticStats.Sheet(home, away, result, season.GameSeed(g.Number)))) count++;
        }
        if (season.Playoffs.TryGet(out var playoffs))
        {
            for (int r = 0; r < playoffs.Rounds.Count; r++)
            {
                for (int s = 0; s < playoffs.Rounds[r].Series.Count; s++)
                {
                    var series = playoffs.Rounds[r].Series[s];
                    for (int i = 0; i < series.Games.Count; i++)
                    {
                        string key = PlayoffKey(r, s, i);
                        if (Credited.Contains(key)) continue;
                        var pg = series.Games[i];
                        if (!byId.TryGetValue(pg.Home, out var home) || !byId.TryGetValue(pg.Away, out var away)) continue;
                        if (Credit(key, SyntheticStats.Sheet(home, away, pg.Result, playoffs.GameSeed(r, s, i)))) count++;
                    }
                }
            }
        }
        return count;
    }
}
