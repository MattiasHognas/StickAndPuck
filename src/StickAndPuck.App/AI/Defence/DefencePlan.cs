using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Defence
{
    /// <summary>One defender's short memory.</summary>
    internal sealed class DefencePlan
    {
        public Duty Duty { get; set; }

        /// <summary>Gets or sets the attacker this defender marks.</summary>
        public int Man { get; set; } = -1;

        /// <summary>Gets or sets the carrier of the current approach.</summary>
        public int On { get; set; } = -1;

        /// <summary>Gets or sets when the approach is decided again.</summary>
        public float DecideAt { get; set; }

        /// <summary>Gets or sets whether this approach ends in a body check.</summary>
        public bool Engage { get; set; }

        public Maybe<Vector2> Read { get; set; }

        public long ReadTick { get; set; } = long.MinValue;

        public Vector2 Error { get; set; }

        public bool Watching { get; set; }

        public float ErrorUntil { get; set; } = float.NegativeInfinity;
    }
}
