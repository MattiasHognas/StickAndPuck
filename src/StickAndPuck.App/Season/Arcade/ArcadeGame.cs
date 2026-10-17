namespace StickAndPuck.App.Season;

/// <summary>One played arcade game, seen from the human's club.</summary>
/// <param name="Stage">The stage, from 0: how many clubs the human had beaten before this game.</param>
/// <param name="Opponent">The other club's id.</param>
/// <param name="Home">True when the human's club was the home club.</param>
/// <param name="Result">The final score, home goals first, and how it was decided.</param>
public sealed record ArcadeGame(int Stage, string Opponent, bool Home, FixtureResult Result)
{
    /// <summary>Gets the human's goals.</summary>
    public int GoalsFor => Home ? Result.HomeGoals : Result.AwayGoals;

    /// <summary>Gets the opponent's goals.</summary>
    public int GoalsAgainst => Home ? Result.AwayGoals : Result.HomeGoals;

    /// <summary>Gets a value indicating whether the human won; a tie is not a win, so it costs a life.</summary>
    public bool Won => GoalsFor > GoalsAgainst;
}
