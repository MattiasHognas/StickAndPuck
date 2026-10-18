namespace StickAndPuck.App.Core
{
    /// <summary>What a season, playoff or arcade run plays next: another match, or nothing because the run is over.</summary>
    public abstract record NextMatch
    {
        private NextMatch() { }

        /// <summary>Gets the run-over value.</summary>
        public static NextMatch Over { get; } = new RunOver();

        /// <summary>Another match to play.</summary>
        public sealed record Play(MatchSetup Setup) : NextMatch;

        /// <summary>The run has no match left.</summary>
        public sealed record RunOver : NextMatch;
    }
}
