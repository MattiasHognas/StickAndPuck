namespace StickAndPuck.App.Core
{
    /// <summary>How often the coach sets a three-game challenge in a season (<see cref="RuleSet.ChallengeFrequency"/>).</summary>
    public enum ChallengeFrequency
    {
        /// <summary>Twelve games between challenges.</summary>
        Rare,

        /// <summary>Default: eight games between challenges.</summary>
        Normal,

        /// <summary>Five games between challenges.</summary>
        Often,
    }
}
