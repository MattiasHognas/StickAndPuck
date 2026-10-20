namespace StickAndPuck.App.Core
{
    /// <summary>How many trade offers the human's club gets at the trade deadline (<see cref="RuleSet.TradeOffers"/>).</summary>
    public enum TradeFrequency
    {
        /// <summary>One offer.</summary>
        Rare,

        /// <summary>Default: one or two offers, as the season seed falls.</summary>
        Normal,

        /// <summary>Two offers.</summary>
        Often,
    }
}
