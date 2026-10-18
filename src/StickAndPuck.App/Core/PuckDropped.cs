using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The puck was dropped.</summary>
    public sealed record PuckDropped(float Time, Vector3 Dot) : MatchEvent(Time);
}
