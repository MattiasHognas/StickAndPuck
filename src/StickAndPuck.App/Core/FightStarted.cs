using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A fight started.</summary>
    public sealed record FightStarted(float Time, Side Side, int PlayerId, Vector3 Position)
        : MatchEvent(Time);
}
