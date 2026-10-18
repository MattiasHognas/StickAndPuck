namespace StickAndPuck.App.AI.Defence
{
    /// <summary>Where the puck carrier is, seen from the defending side.</summary>
    public enum DefenceZone
    {
        /// <summary>The carrier is in the defending side's own zone.</summary>
        Own,

        /// <summary>Between the blue lines.</summary>
        Neutral,

        /// <summary>The carrier is deep in his own zone: the defending side forechecks.</summary>
        Forecheck,
    }
}
