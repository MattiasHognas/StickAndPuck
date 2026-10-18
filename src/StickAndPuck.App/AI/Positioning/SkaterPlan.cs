using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>One skater's plan: role, job, ideal spot, the smoothed spot it is skating to, and any request.</summary>
    internal sealed class SkaterPlan
    {
        public Role Role { get; set; }

        public Job Job { get; set; }

        /// <summary>Gets or sets the role's ideal spot this step.</summary>
        public Vector2 Ideal { get; set; }

        /// <summary>Gets or sets where the skater is heading this step.</summary>
        public Vector2 Target { get; set; }

        /// <summary>Gets or sets the shape spot as this skater reads it: it trails the ideal spot, slower for poor positioning.</summary>
        public Maybe<Vector2> Read { get; set; }

        /// <summary>Gets or sets this skater's misjudgement of its spot; bigger for poor positioning.</summary>
        public Vector2 Error { get; set; }

        /// <summary>Gets or sets when the misjudgement is next re-rolled.</summary>
        public float ErrorUntil { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets the skater's cycle station this step, or None.</summary>
        public Maybe<Station> Station { get; set; }

        /// <summary>Gets or sets the spot this skater last read as open for a pass, or None.</summary>
        public Maybe<Vector2> Open { get; set; }

        /// <summary>Gets or sets when the skater next reads where to get open.</summary>
        public float OpenNext { get; set; }

        public Maybe<Vector2> Request { get; set; }

        public bool RequestSprint { get; set; }

        public long RequestTick { get; set; } = long.MinValue;
    }
}
