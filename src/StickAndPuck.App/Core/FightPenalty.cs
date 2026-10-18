namespace StickAndPuck.App.Core
{
    /// <summary>What a fight costs.</summary>
    public enum FightPenalty
    {
        /// <summary>Both fighters get a 5 minute major and a game misconduct (SHL).</summary>
        MajorAndEjection,

        /// <summary>The loser serves 2 minutes, the winner nothing (the game's original rule, Arcade).</summary>
        LoserMinor,

        /// <summary>Both fighters get a 5 minute major and stay in the game (NHL).</summary>
        Major,
    }
}
