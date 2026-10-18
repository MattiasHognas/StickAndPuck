using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The puck hit a post or crossbar.</summary>
    public sealed record PuckHitPost(float Time, Vector3 Position, float Speed) : MatchEvent(Time);
}
