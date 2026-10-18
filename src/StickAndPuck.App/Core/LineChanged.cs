using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A line or goalie went on.</summary>
    public sealed record LineChanged(float Time, Side Side, int PlayerId, Vector3 Position)
        : MatchEvent(Time);
}
