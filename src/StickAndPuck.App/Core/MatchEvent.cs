namespace StickAndPuck.App.Core
{
    /// <summary>One thing that happened in a match. The hierarchy is closed: every kind is a sealed record
    /// below, carrying only its own fields, so consumers switch over the types.</summary>
    public abstract record MatchEvent
    {
        /// <summary>Initializes a new instance of the <see cref="MatchEvent"/> class.</summary>
        private protected MatchEvent(float time)
        {
            Time = time;
        }

        /// <summary>Gets the match time in seconds (<see cref="World.Time"/>) when it happened.</summary>
        public float Time { get; init; }
    }
}
