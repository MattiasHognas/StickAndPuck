namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>What the coach reacts to after the human's last game (W127), sharpest first.</summary>
    public enum ReactionKind
    {
        /// <summary>One of ours scored three or more.</summary>
        HatTrick,

        /// <summary>We won after trailing by the comeback margin.</summary>
        Comeback,

        /// <summary>We lost after leading by the blown-lead margin.</summary>
        BlownLead,

        /// <summary>We won without a goal against.</summary>
        Shutout,

        /// <summary>One of ours won it in overtime or the shootout.</summary>
        Decider,

        /// <summary>We lost by the heavy-loss margin.</summary>
        HeavyLoss,

        /// <summary>Our goalie made the hot-goalie number of saves.</summary>
        HotGoalie,

        /// <summary>One of ours lost a fight.</summary>
        LostFight,

        /// <summary>We took the penalty-trouble number of penalties.</summary>
        PenaltyTrouble,

        /// <summary>One of ours won a fight.</summary>
        WonFight,
    }
}
