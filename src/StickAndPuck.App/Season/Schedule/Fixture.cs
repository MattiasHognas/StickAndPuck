namespace StickAndPuck.App.Season;

/// <summary>How a match was decided, which sets the SHL points.</summary>
public enum ResultKind
{
    /// <summary>Decided in regulation: 3 points to the winner.</summary>
    Regulation,

    /// <summary>Decided in 3 on 3 overtime: 2 points to the winner, 1 to the loser.</summary>
    Overtime,

    /// <summary>Decided in the shootout: 2 points to the winner, 1 to the loser.</summary>
    Shootout,
}

/// <summary>A played match's final score, shootout goal included, and how it was decided.</summary>
/// <param name="HomeGoals">Home goals on the final score.</param>
/// <param name="AwayGoals">Away goals on the final score.</param>
/// <param name="Kind">Regulation, overtime or shootout.</param>
public sealed record FixtureResult(int HomeGoals, int AwayGoals, ResultKind Kind)
{
    /// <summary>Gets the SHL points for the home club.</summary>
    public int HomePoints => Points(HomeGoals, AwayGoals);

    /// <summary>Gets the SHL points for the away club.</summary>
    public int AwayPoints => Points(AwayGoals, HomeGoals);

    int Points(int own, int other)
    {
        if (own == other) return 1;
        bool extra = Kind != ResultKind.Regulation;
        return own > other ? (extra ? 2 : 3) : (extra ? 1 : 0);
    }
}

/// <summary>One game of the season, and its result once played.</summary>
/// <param name="Number">The game's index in the whole schedule, from 0.</param>
/// <param name="Round">The round, from 0; every club plays once a round.</param>
/// <param name="Home">The home club's id.</param>
/// <param name="Away">The away club's id.</param>
/// <param name="Result">The result, or null while unplayed.</param>
public sealed record Fixture(int Number, int Round, string Home, string Away, FixtureResult? Result = null)
{
    /// <summary>Gets a value indicating whether the game has a result.</summary>
    public bool Played => Result != null;

    /// <summary>Gets a value indicating whether a club plays in this game.</summary>
    /// <param name="clubId">The club id.</param>
    /// <returns>True for the home or away club.</returns>
    public bool Involves(string clubId)
    {
        return string.Equals(Home, clubId, StringComparison.Ordinal) || string.Equals(Away, clubId, StringComparison.Ordinal);
    }
}
