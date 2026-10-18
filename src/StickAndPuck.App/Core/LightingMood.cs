namespace StickAndPuck.App.Core
{
    /// <summary>The arena lighting mood a match starts with (its time of day).</summary>
    public enum LightingMood
    {
        /// <summary>Default: a random mood each match.</summary>
        Random,

        /// <summary>Early afternoon: cool daylight, bright ice.</summary>
        Early,

        /// <summary>Late afternoon: the dusk look.</summary>
        Afternoon,

        /// <summary>Evening: floodlit, dark stands.</summary>
        Late,
    }
}
