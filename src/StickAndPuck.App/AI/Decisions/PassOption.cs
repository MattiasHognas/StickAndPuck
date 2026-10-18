using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>One pass a carrier could make: to whom, where the puck is aimed, and what it is worth.</summary>
    /// <param name="Receiver">The teammate.</param>
    /// <param name="Target">Where the pass is aimed (x, z), leading the receiver.</param>
    /// <param name="Success">The passer's estimate that the pass arrives, 0 to 1.</param>
    /// <param name="Value">What the pass is worth: <see cref="Success"/> times the value of the receiver's spot.</param>
    public readonly record struct PassOption(
        Player Receiver,
        Vector2 Target,
        float Success,
        float Value
    );
}
