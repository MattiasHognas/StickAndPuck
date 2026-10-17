using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>One played playoff game. Playoff games always have a winner.</summary>
public sealed record PlayoffGame(string Home, string Away, FixtureResult Result)
{
    /// <summary>Gets the winning club's id.</summary>
    [JsonIgnore]
    public string Winner => Result.HomeGoals > Result.AwayGoals ? Home : Away;
}

/// <summary>One playoff series between a higher and a lower seed. In a best of seven the higher seed is at
/// home in games 1, 3, 5 and 7; in the best of three play-in the lower seed hosts game 1 and the
/// higher seed games 2 and 3, as in the SHL.</summary>
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
    public Maybe<string> Winner
    {
        get
        {
            if (HighWins >= WinsNeeded)
            {
                return High;
            }

            if (LowWins >= WinsNeeded)
            {
                return Low;
            }

            return Maybe<string>.None;
        }
    }

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
    public bool Involves(string clubId)
    {
        return Same(High, clubId) || Same(Low, clubId);
    }

    /// <summary>Gets the seed a club has in this series.</summary>
    public int SeedOf(string clubId)
    {
        if (Same(High, clubId))
        {
            return HighSeed;
        }

        if (Same(Low, clubId))
        {
            return LowSeed;
        }

        return 0;
    }

    /// <summary>Gets a value indicating whether the higher seed is at home in a game.</summary>
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
