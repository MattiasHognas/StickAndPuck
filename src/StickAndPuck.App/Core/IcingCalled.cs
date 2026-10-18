using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>Play stopped for icing.</summary>
    public sealed record IcingCalled(float Time, Side Offender, int PlayerId, Vector3 Position)
        : MatchEvent(Time);
}
