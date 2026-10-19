using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The league suspends a player for his club's next <paramref name="Games"/> season games. Published
    /// again with more games when a second offence in the same match lengthens it.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The suspended player's club.</param>
    /// <param name="PlayerId">The suspended player's id.</param>
    /// <param name="Position">Where he was.</param>
    /// <param name="Games">The games he sits out, 1 to 3.</param>
    /// <param name="Cause">What he did; the latest offence when he has two.</param>
    public sealed record PlayerSuspended(
        float Time,
        Side Side,
        int PlayerId,
        Vector3 Position,
        int Games,
        SuspensionCause Cause
    ) : MatchEvent(Time);
}
