using System.Numerics;
using System.Runtime.InteropServices;

namespace StickAndPuck.App.Physics
{
    /// <summary>One impact: what was hit, where, how hard (speed into the surface) and, for a body, whose.</summary>
    /// <param name="Kind">The surface.</param>
    /// <param name="Position">The puck's position at the hit.</param>
    /// <param name="Speed">Speed into the surface, metres per second.</param>
    /// <param name="PlayerId">The player hit, or -1.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct PuckImpact(
        PuckHit Kind,
        Vector3 Position,
        float Speed,
        int PlayerId = -1
    );
}
