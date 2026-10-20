namespace StickAndPuck.App.Season.Challenges
{
    /// <summary>Where a coach's challenge stands.</summary>
    public enum ChallengeOutcome
    {
        /// <summary>Still being played for.</summary>
        Open,

        /// <summary>Met: the club's morale went up.</summary>
        Met,

        /// <summary>Missed: the club's morale dipped a little.</summary>
        Missed,
    }
}
