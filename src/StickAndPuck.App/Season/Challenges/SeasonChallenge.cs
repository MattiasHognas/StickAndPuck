namespace StickAndPuck.App.Season.Challenges
{
    /// <summary>A coach's three-game challenge for the human's club (W134), saved with the season
    /// (<see cref="Schedule.SeasonState.Challenges"/>): the open one and the ones already settled.</summary>
    public sealed class SeasonChallenge
    {
        /// <summary>Gets or sets what the challenge asks for.</summary>
        public ChallengeKind Kind { get; set; }

        /// <summary>Gets or sets the schedule numbers of its three games, the human's next three when it was set.</summary>
        public IList<int> Games { get; set; } = [];

        /// <summary>Gets or sets the wins, goals or shutouts it takes.</summary>
        public int Target { get; set; }

        /// <summary>Gets or sets the rival's club id for <see cref="ChallengeKind.BeatRival"/>, else "".</summary>
        public string Rival { get; set; } = "";

        /// <summary>Gets or sets the human's regular-season games played when it was set.</summary>
        public int Issued { get; set; }

        /// <summary>Gets or sets where it stands.</summary>
        public ChallengeOutcome Outcome { get; set; }

        /// <summary>Gets or sets the human's regular-season games played when it was settled; 0 while open.</summary>
        public int Settled { get; set; }

        /// <summary>Gets or sets the morale points (at normal strength) it gave when settled: the reward, or the penalty below 0.</summary>
        public int Morale { get; set; }
    }
}
