namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>How one roll of a read or an execution came out.</summary>
    public enum ReadOutcome
    {
        /// <summary>The skater plays to their hockey IQ.</summary>
        Clean,

        /// <summary>The skater fumbles: this once they read and play like a much weaker player.</summary>
        Fumble,

        /// <summary>The skater gets lucky: this once they read and play like a star.</summary>
        Lucky,
    }
}
