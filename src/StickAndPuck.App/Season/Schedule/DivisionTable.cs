namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>One division's part of the league table, in table order.</summary>
    /// <param name="Conference">The conference the division plays in.</param>
    /// <param name="Division">The division's name.</param>
    /// <param name="Rows">The division's clubs, best first.</param>
    public sealed record DivisionTable(
        string Conference,
        string Division,
        IReadOnlyList<StandingRow> Rows
    );
}
