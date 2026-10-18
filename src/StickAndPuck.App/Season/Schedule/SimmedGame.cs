namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>A game settled by <see cref="SeasonSession.SimCurrentGame"/>.</summary>
    /// <param name="Home">The home club's id.</param>
    /// <param name="Away">The away club's id.</param>
    /// <param name="Result">The result.</param>
    /// <param name="Playoff">True for a playoff game, false for a regular season game.</param>
    /// <param name="Stage">The round or series stage, for the screen ("ROUND 12", "FINAL GAME 3").</param>
    public sealed record SimmedGame(
        string Home,
        string Away,
        FixtureResult Result,
        bool Playoff,
        string Stage
    );
}
