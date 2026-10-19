namespace StickAndPuck.App.Core
{
    /// <summary>How readily the coach reacts to the last game in the dressing room (<see cref="RuleSet.CoachTemper"/>).</summary>
    public enum CoachTemper
    {
        /// <summary>Only the big moments: a three-goal lead blown, a five-goal loss, 40 saves.</summary>
        Mild,

        /// <summary>Default: a two-goal lead blown, a four-goal loss, 35 saves.</summary>
        Normal,

        /// <summary>A short fuse: a three-goal loss, 30 saves and four penalties are enough.</summary>
        Strong,
    }
}
