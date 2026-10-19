using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>One skater off for one on.</summary>
    internal sealed record Swap(Player Out, Player In)
    {
        public bool Done { get; set; }

        /// <summary>Gets or sets a value indicating whether the skater coming on stepped out before the one going off reached the door (a rushed change): both are on the ice until he does.</summary>
        public bool Early { get; set; }

        /// <summary>Gets the spot (x, z) on the ice where the bench door is.</summary>
        public Vector2 Door => Squad.Door(Out.Side);
    }
}
