using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>One skater's misread of his spot.</summary>
    internal sealed class SpotPlan
    {
        public Vector2 Error { get; set; }

        public float ErrorUntil { get; set; } = float.NegativeInfinity;

        /// <summary>Gets or sets the open ice this power play skater last read around his spot.</summary>
        public Maybe<Vector2> Open { get; set; }

        /// <summary>Gets or sets when he reads it again.</summary>
        public float OpenNext { get; set; }

        /// <summary>Gets or sets when a power play carrier next looks for a shot.</summary>
        public float ShotLook { get; set; }
    }
}
