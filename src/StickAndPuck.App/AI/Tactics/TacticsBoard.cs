using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>Club tactics' state for one match.</summary>
    internal sealed class TacticsBoard
    {
        public TacticsTally[] Tally { get; } = [new(), new()];

        /// <summary>Gets each skater's task this step.</summary>
        public Dictionary<int, TacticTask> Tasks { get; } = [];

        /// <summary>Gets each skater's spot as he reads it, trailing the tactic's spot.</summary>
        public Dictionary<int, (Vector2 At, long Tick)> Reads { get; } = [];

        /// <summary>Gets or sets the carrier and run with the puck whose zone entry was weighed, so a run is weighed once.</summary>
        public (int Player, float GotAt) Weighed { get; set; } = (-1, 0f);

        /// <summary>Gets or sets the dump the carrier is setting up, or None.</summary>
        public Maybe<DumpPlan> Dump { get; set; }

        /// <summary>Gets or sets the side whose dump is being chased, and until when.</summary>
        public Maybe<ChaseWatch> Chasing { get; set; }

        /// <summary>Gets or sets the side that last carried the puck, or None before anyone has.</summary>
        public SideOrNone Owner { get; set; }

        /// <summary>Gets or sets when that side got the puck.</summary>
        public float OwnedSince { get; set; }

        /// <summary>Gets where the other side's carrier was last step, along each side's attack ("a"), to spot breakouts leaving the zone.</summary>
        public float[] LastCarrierA { get; } = [float.NegativeInfinity, float.NegativeInfinity];

        /// <summary>Gets where the puck was last step, along each side's attack ("a"), to spot entries.</summary>
        public float[] LastA { get; } = [0f, 0f];
    }
}
