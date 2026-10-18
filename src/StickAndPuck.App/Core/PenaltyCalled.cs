using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A penalty was called.</summary>
    public sealed record PenaltyCalled(
        float Time,
        Side Side,
        int PlayerId,
        Vector3 Position,
        float Seconds
    ) : MatchEvent(Time);
}
