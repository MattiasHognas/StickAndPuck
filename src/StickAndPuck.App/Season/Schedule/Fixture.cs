using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>One game of the season, and its result once played.</summary>
/// <param name="Number">The game's index in the whole schedule, from 0.</param>
/// <param name="Round">The round, from 0; every club plays once a round.</param>
/// <param name="Home">The home club's id.</param>
/// <param name="Away">The away club's id.</param>
/// <param name="Result">The result, or None while unplayed.</param>
public sealed record Fixture(int Number, int Round, string Home, string Away, Maybe<FixtureResult> Result = default)
{
    /// <summary>Gets a value indicating whether the game has a result.</summary>
    public bool Played => Result.HasValue;

    /// <summary>Gets a value indicating whether a club plays in this game.</summary>
    public bool Involves(string clubId)
    {
        return string.Equals(Home, clubId, StringComparison.Ordinal) || string.Equals(Away, clubId, StringComparison.Ordinal);
    }
}
