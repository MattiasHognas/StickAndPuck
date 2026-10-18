using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A goalie stopped a shot.</summary>
    public sealed record ShotSaved(
        float Time,
        Side Side,
        int GoalieId,
        Vector3 Position,
        float Speed
    ) : MatchEvent(Time);
}
