namespace StickAndPuck.App.Core
{
    /// <summary>Why the league suspended a player.</summary>
    public enum SuspensionCause
    {
        /// <summary>His second fight in one game.</summary>
        SecondFight,

        /// <summary>A match penalty: a game misconduct, or a major and game misconduct (a fight under SHL rules, a dangerous hit).</summary>
        MatchPenalty,
    }
}
