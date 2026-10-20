using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The goal review's own state for one match (W79): the goal held under review, the coach's challenges
    /// used, and when an attacker last got in each goalie's way.</summary>
    internal sealed class ReviewState
    {
        /// <summary>Gets the luck in an AI coach's challenge; its own stream of the match seed, so the other dice keep theirs.</summary>
        public required Random Dice { get; init; }

        /// <summary>Gets or sets the goal held under review, or None while no goal waits.</summary>
        public Maybe<GoalUnderReview> Goal { get; set; }

        /// <summary>Gets or sets where the review of the held goal is.</summary>
        public ReviewStage Stage { get; set; }

        /// <summary>Gets or sets the seconds left in <see cref="Stage"/>.</summary>
        public float Timer { get; set; }

        /// <summary>Gets or sets the seconds since the held goal went in.</summary>
        public float Held { get; set; }

        /// <summary>Gets or sets a value indicating whether an attacker ran into the goalie in or around his crease as the held goal went in: it is disallowed.</summary>
        public bool Contact { get; set; }

        /// <summary>Gets or sets a value indicating whether an attacker stood in the crease as the held goal went in: no call by itself, but a challenge for goalie interference holds.</summary>
        public bool InCrease { get; set; }

        /// <summary>Gets or sets a value indicating whether the scoring club entered the zone offside, missed by the linesman: a challenge for offside holds.</summary>
        public bool Offside { get; set; }

        /// <summary>Gets or sets what the challenge under way is for.</summary>
        public ReviewCall Challenge { get; set; }

        /// <summary>Gets whether each club (by <see cref="Side"/>) has used its coach's challenge.</summary>
        public bool[] Used { get; } = new bool[2];

        /// <summary>Gets or sets the club that owes a delay-of-game minor for a failed challenge, called the step after its goal counted.</summary>
        public Maybe<Side> Minor { get; set; }

        /// <summary>Gets or sets a value indicating whether the challenge that just ended failed, so the goal banner says the goal stands.</summary>
        public bool Failed { get; set; }

        /// <summary>Gets or sets a value indicating whether every human's start button has been seen up since the review began, so a held one does not skip it.</summary>
        public bool Armed { get; set; }

        /// <summary>Gets or sets a value indicating whether a test or script asked to skip the stage under way.</summary>
        public bool SkipAsked { get; set; }

        /// <summary>Gets or sets the pass and shoot buttons of the asked club's humans last step, so the prompt answers on a fresh press.</summary>
        public (bool Pass, bool Shoot) LastAnswer { get; set; }

        /// <summary>Gets the match time an attacker last touched the goalie in or around his crease, per end (index 0 the -x net, 1 the +x net).</summary>
        public float[] ContactAt { get; } = [float.NegativeInfinity, float.NegativeInfinity];

        /// <summary>Gets the match time an attacker last stood in the crease, per end (index 0 the -x net, 1 the +x net).</summary>
        public float[] CreaseAt { get; } = [float.NegativeInfinity, float.NegativeInfinity];
    }
}
