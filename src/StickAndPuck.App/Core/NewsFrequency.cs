namespace StickAndPuck.App.Core
{
    /// <summary>How often a season news event comes up (<see cref="RuleSet.NewsFrequency"/>).</summary>
    public enum NewsFrequency
    {
        /// <summary>About one game in eight.</summary>
        Rare,

        /// <summary>Default: about one game in five.</summary>
        Normal,

        /// <summary>About one game in three.</summary>
        Often,
    }
}
