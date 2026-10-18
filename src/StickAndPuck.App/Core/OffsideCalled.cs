using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>Play stopped for offside.</summary>
    public sealed record OffsideCalled(float Time, Side Attacker, Vector3 Position)
        : MatchEvent(Time);
}
