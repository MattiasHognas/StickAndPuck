using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>The bench's state for one match.</summary>
    internal sealed class LinesBoard
    {
        public Maybe<Squad>[] Squads { get; } = new Maybe<Squad>[2];

        /// <summary>Gets each club's change on the fly, if one is under way.</summary>
        public Maybe<ShiftChange>[] Changing { get; } = new Maybe<ShiftChange>[2];

        /// <summary>Gets the groups that change at the next faceoff, indexed by side then group.</summary>
        public bool[,] Pending { get; } = new bool[2, 2];

        /// <summary>Gets the clubs that send their first line and pair out at the next faceoff (a new period).</summary>
        public bool[] FirstUnits { get; } = new bool[2];

        /// <summary>Gets the clubs whose backup goalie comes in at the next faceoff.</summary>
        public bool[] GoalieSwap { get; } = new bool[2];

        /// <summary>Gets each club's pulled goalie, or None while he is in the net.</summary>
        public Maybe<Player>[] PulledGoalie { get; } = new Maybe<Player>[2];

        /// <summary>Gets the id of the extra attacker who came on for each club's goalie, or -1.</summary>
        public int[] ExtraAttacker { get; } = [-1, -1];

        /// <summary>Gets whether each human slot held the pull goalie button last step.</summary>
        public bool[] PullHeld { get; } = new bool[MatchSetup.Slots];

        /// <summary>Gets the button presses not yet acted on, per side.</summary>
        public bool[] Requested { get; } = new bool[2];

        /// <summary>Gets the game seconds of recent goals against, per side.</summary>
        public List<float>[] GoalsAgainst { get; } =
        [
            [],
            [],
        ];

        public int[] LastScore { get; } = new int[2];

        /// <summary>Gets or sets the game seconds played: time in play, so the clock's time over all periods.</summary>
        public float GameTime { get; set; }

        public MatchPhase LastPhase { get; set; } = MatchPhase.Intro;

        /// <summary>Gets what each club last sent out and when, for the debug panel.</summary>
        public (string What, float At)[] Last { get; } =
        [("", float.NegativeInfinity), ("", float.NegativeInfinity)];
    }
}
