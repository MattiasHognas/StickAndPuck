namespace StickAndPuck.App.Season.Trades
{
    /// <summary>What a skater is best at, from his traits (<see cref="TradeDesk.Profile"/>): the twist of a trade
    /// offer is getting one kind of player for another.</summary>
    public enum TradeProfile
    {
        /// <summary>Shot accuracy and power.</summary>
        Sniper,

        /// <summary>Acceleration and top speed.</summary>
        Speedster,

        /// <summary>Checking.</summary>
        Grinder,

        /// <summary>Puck control and hockey IQ.</summary>
        Playmaker,
    }
}
