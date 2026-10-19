using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A player was hurt: he plays this match out and misses his club's next <paramref name="Games"/> season games.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The injured player's club.</param>
    /// <param name="PlayerId">The injured player's id.</param>
    /// <param name="Position">Where it happened.</param>
    /// <param name="Games">The games he misses, 1 to 6.</param>
    /// <param name="Cause">What hurt him.</param>
    public sealed record PlayerInjured(
        float Time,
        Side Side,
        int PlayerId,
        Vector3 Position,
        int Games,
        InjuryCause Cause
    ) : MatchEvent(Time);
}
