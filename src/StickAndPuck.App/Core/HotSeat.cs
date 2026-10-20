namespace StickAndPuck.App.Core
{
    /// <summary>Whether the board fires you when a season goes badly wrong (<see cref="RuleSet.HotSeat"/>): far below the
    /// board's season goal late in the regular season, or well below it at the end.</summary>
    public enum HotSeat
    {
        /// <summary>Never fired (Arcade): the board only grumbles in the dressing room.</summary>
        Off,

        /// <summary>Default (SHL): judged over the last quarter of the regular season.</summary>
        Lenient,

        /// <summary>The NHL: judged from halfway through the regular season.</summary>
        Strict,
    }
}
