using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>One played playoff game. Playoff games always have a winner.</summary>
/// <param name="Home">The home club's id.</param>
/// <param name="Away">The away club's id.</param>
/// <param name="Result">The final score and how it was decided.</param>
public sealed record PlayoffGame(string Home, string Away, FixtureResult Result)
{
    /// <summary>Gets the winning club's id.</summary>
    [JsonIgnore]
    public string Winner => Result.HomeGoals > Result.AwayGoals ? Home : Away;
}

/// <summary>
/// One playoff series between a higher and a lower seed. In a best of seven the higher seed is at
/// home in games 1, 3, 5 and 7; in the best of three play-in the lower seed hosts game 1 and the
/// higher seed games 2 and 3, as in the SHL.
/// </summary>
public sealed class PlayoffSeries
{
    /// <summary>Gets or sets the higher seed's place in the final table, from 1.</summary>
    public int HighSeed { get; set; }

    /// <summary>Gets or sets the lower seed's place in the final table, from 1.</summary>
    public int LowSeed { get; set; }

    /// <summary>Gets or sets the higher seed's club id.</summary>
    public string High { get; set; } = "";

    /// <summary>Gets or sets the lower seed's club id.</summary>
    public string Low { get; set; } = "";

    /// <summary>Gets or sets the most games the series can take: 3 or 7.</summary>
    public int BestOf { get; set; }

    /// <summary>Gets or sets the games played so far.</summary>
    public List<PlayoffGame> Games { get; set; } = [];

    /// <summary>Gets the wins needed to take the series.</summary>
    [JsonIgnore]
    public int WinsNeeded => (BestOf / 2) + 1;

    /// <summary>Gets the higher seed's wins.</summary>
    [JsonIgnore]
    public int HighWins => Games.Count(g => Same(g.Winner, High));

    /// <summary>Gets the lower seed's wins.</summary>
    [JsonIgnore]
    public int LowWins => Games.Count - HighWins;

    /// <summary>Gets the winner's club id, or None while the series is open.</summary>
    [JsonIgnore]
    public Maybe<string> Winner => HighWins >= WinsNeeded ? High : LowWins >= WinsNeeded ? Low : Maybe<string>.None;

    /// <summary>Gets a value indicating whether one club has won the series.</summary>
    [JsonIgnore]
    public bool IsOver => Winner.HasValue;

    /// <summary>Gets the club at home in the next game.</summary>
    [JsonIgnore]
    public string NextHome => HighHosts(Games.Count) ? High : Low;

    /// <summary>Gets the club away in the next game.</summary>
    [JsonIgnore]
    public string NextAway => HighHosts(Games.Count) ? Low : High;

    /// <summary>Gets a value indicating whether a club plays in this series.</summary>
    /// <param name="clubId">The club id.</param>
    /// <returns>True for either club.</returns>
    public bool Involves(string clubId)
    {
        return Same(High, clubId) || Same(Low, clubId);
    }

    /// <summary>Gets the seed a club has in this series.</summary>
    /// <param name="clubId">The club id.</param>
    /// <returns>The seed, or 0 when the club is not in the series.</returns>
    public int SeedOf(string clubId)
    {
        return Same(High, clubId) ? HighSeed : Same(Low, clubId) ? LowSeed : 0;
    }

    /// <summary>Gets a value indicating whether the higher seed is at home in a game.</summary>
    /// <param name="game">The game, from 0.</param>
    /// <returns>True when the higher seed hosts it.</returns>
    public bool HighHosts(int game)
    {
        return BestOf == 3 ? game != 0 : game % 2 == 0;
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
}

/// <summary>One playoff round: its name, its series length and its series, best seed first.</summary>
public sealed class PlayoffRound
{
    /// <summary>Gets or sets the round's name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the series length: 3 or 7.</summary>
    public int BestOf { get; set; }

    /// <summary>Gets or sets the series, the top seed's first.</summary>
    public List<PlayoffSeries> Series { get; set; } = [];

    /// <summary>Gets a value indicating whether every series in the round has a winner.</summary>
    [JsonIgnore]
    public bool IsOver => Series.TrueForAll(s => s.IsOver);
}
