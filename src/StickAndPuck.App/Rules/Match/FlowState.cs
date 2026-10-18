using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The flow's own state for one world.</summary>
    internal sealed class FlowState
    {
        public bool Active { get; set; }

        public bool Log { get; set; }

        /// <summary>Gets or sets the real seconds left in the current pause (intro, faceoff, goal, stoppage, intermission).</summary>
        public float Timer { get; set; }

        public Vector3 Dot { get; set; }

        public bool RefereeDrops { get; set; }

        /// <summary>Gets the luck in the draws; the match seed's stream, so runs repeat.</summary>
        public required Random Dice { get; init; }

        /// <summary>Gets the draw of the current faceoff.</summary>
        public DrawState Draw { get; } = new();

        /// <summary>Gets or sets how long a goalie has held the puck in play.</summary>
        public float Held { get; set; }

        /// <summary>Gets or sets the end-of-game celebration, from the horn of a decided game.</summary>
        public Maybe<Celebration.CelebrationState> Celebration { get; set; }
    }
}
