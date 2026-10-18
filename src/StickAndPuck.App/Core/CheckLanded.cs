using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A body check (or a punch in a fight) landed.</summary>
    public sealed record CheckLanded(
        float Time,
        Side Side,
        int CheckerId,
        Vector3 Position,
        float Strength
    ) : MatchEvent(Time);
}
