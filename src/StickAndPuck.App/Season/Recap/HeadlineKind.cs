namespace StickAndPuck.App.Season.Recap
{
    /// <summary>What a recap headline is about (<see cref="RecapHeadline"/>). The first block is how the season
    /// ended, one of them always leads; the rest are picked from what happened, in this order.</summary>
    public enum HeadlineKind
    {
        /// <summary>Fired by the board.</summary>
        Fired,

        /// <summary>Lost the playout and went down.</summary>
        Relegated,

        /// <summary>Won the playout and stayed up.</summary>
        StayedUp,

        /// <summary>Won the final.</summary>
        Champions,

        /// <summary>Lost the final.</summary>
        RunnersUp,

        /// <summary>Lost in the semi-finals.</summary>
        Semifinal,

        /// <summary>Out of the playoffs before the semi-finals.</summary>
        PlayoffExit,

        /// <summary>Missed the playoffs.</summary>
        MissedPlayoffs,

        /// <summary>Topped the regular season table.</summary>
        RegularSeasonWinner,

        /// <summary>A long run of wins (<see cref="SeasonRecap.StreakLength"/> or more).</summary>
        WinStreak,

        /// <summary>A player scored three or more in a game.</summary>
        HatTrick,

        /// <summary>Won more games than lost against a rival.</summary>
        DerbyWins,

        /// <summary>Lost more games than won against a rival.</summary>
        DerbyLosses,

        /// <summary>A young player developed a lot (W132).</summary>
        Breakthrough,

        /// <summary>A trade at the deadline (W135).</summary>
        Trade,

        /// <summary>The coach's challenges met (W134).</summary>
        Challenges,

        /// <summary>A long run of losses.</summary>
        LosingStreak,
    }
}
