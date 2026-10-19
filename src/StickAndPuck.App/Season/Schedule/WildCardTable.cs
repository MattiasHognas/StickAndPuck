namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>One conference of the NHL wild card standings: each division's top three (they qualify by
    /// place), the division with the better leader first, and every other club of the conference in one
    /// race on the table's order, the first two of it holding the wild cards.</summary>
    /// <param name="Conference">The conference's name.</param>
    /// <param name="Leaders">Each division with its top three clubs only.</param>
    /// <param name="Race">The rest of the conference, best first: wild card one, wild card two, then the chasers.</param>
    public sealed record WildCardTable(
        string Conference,
        IReadOnlyList<DivisionTable> Leaders,
        IReadOnlyList<StandingRow> Race
    );
}
