namespace StickAndPuck.App.Season.Trades
{
    /// <summary>Where a trade offer stands.</summary>
    public enum TradeAnswer
    {
        /// <summary>Not answered yet; it lapses when the next game is played.</summary>
        Open,

        /// <summary>Accepted: the players changed clubs.</summary>
        Accepted,

        /// <summary>Declined, or lapsed at the next game.</summary>
        Declined,
    }
}
