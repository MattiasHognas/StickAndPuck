using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>A change under way: who goes off, who comes on.</summary>
    internal sealed record ShiftChange(
        Side Side,
        Group Group,
        Unit Incoming,
        List<Swap> Swaps,
        float Started
    )
    {
        /// <summary>Gets or sets a value indicating whether the line change event went out.</summary>
        public bool Announced { get; set; }
    }
}
