using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>One skater off for one on.</summary>
    internal sealed record Swap(Player Out, Player In)
    {
        public bool Done { get; set; }

        /// <summary>Gets the spot (x, z) on the ice where the bench door is.</summary>
        public Vector2 Door => Squad.Door(Out.Side);
    }
}
