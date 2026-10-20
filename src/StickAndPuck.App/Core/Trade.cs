namespace StickAndPuck.App.Core
{
    /// <summary>A player-for-player trade made in a season (W135), as the season save and a replay store it: the
    /// club data files stay the base and each trade is a delta on them (<see cref="TradeDeals.Apply"/>). The
    /// players are found by their jersey numbers in the rosters as they stood when the trade was made
    /// (the data files plus the season's earlier trades); each takes over the other's roster slot, so the
    /// lines keep their shape, and wears the new number the trade gave him.</summary>
    /// <param name="Club">One club's id.</param>
    /// <param name="Number">The number of the player <paramref name="Club"/> gives away.</param>
    /// <param name="NewNumber">The number he wears at <paramref name="OtherClub"/>.</param>
    /// <param name="OtherClub">The other club's id.</param>
    /// <param name="OtherNumber">The number of the player <paramref name="OtherClub"/> gives away.</param>
    /// <param name="OtherNewNumber">The number he wears at <paramref name="Club"/>.</param>
    public sealed record Trade(
        string Club,
        int Number,
        int NewNumber,
        string OtherClub,
        int OtherNumber,
        int OtherNewNumber
    );
}
