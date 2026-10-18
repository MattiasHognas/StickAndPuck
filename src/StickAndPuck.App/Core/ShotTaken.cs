using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A shot was taken.</summary>
    public sealed record ShotTaken(
        float Time,
        Side Side,
        int ShooterId,
        Vector3 Position,
        float Speed
    ) : MatchEvent(Time);
}
