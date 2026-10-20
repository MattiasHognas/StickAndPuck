namespace StickAndPuck.App.Rules.Offside
{
    /// <summary>Offside's own state for one world.</summary>
    internal sealed class OffsideState
    {
        /// <summary>Gets or sets the zone the puck is in (-1, 0, +1).</summary>
        public int Zone { get; set; }

        /// <summary>Gets or sets the zone with an offside pending, or 0.</summary>
        public int PendingEnd { get; set; }

        /// <summary>Gets or sets the last touch when the puck entered, so a new attacking touch can be told apart.</summary>
        public int EntryTouch { get; set; } = -1;

        /// <summary>Gets or sets a value indicating whether the pending offside is intentional: the puck passed or dumped in to an attacker hanging deep in the zone.</summary>
        public bool Intentional { get; set; }

        /// <summary>Gets or sets the zone the attacking club last entered offside with the linesman missing it (under
        /// <see cref="Core.RuleSet.GoalChallenge"/>), or 0: the entry a coach's challenge looks at while the puck stays in.</summary>
        public int MissedEnd { get; set; }
    }
}
