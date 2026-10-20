namespace StickAndPuck.App.Season.Challenges
{
    /// <summary>What a coach's challenge asks for over its three games (<see cref="SeasonChallenge"/>).</summary>
    public enum ChallengeKind
    {
        /// <summary>Win two of the three games.</summary>
        WinTwo,

        /// <summary>Keep a clean sheet in one of the three games.</summary>
        Shutout,

        /// <summary>Score <see cref="SeasonChallenge.Target"/> goals over the three games.</summary>
        Goals,

        /// <summary>Beat the rival met in one of the three games.</summary>
        BeatRival,
    }
}
