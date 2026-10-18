using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A centre moved before the drop and was waved out of the faceoff (<see cref="RuleSet.FaceoffViolations"/>).</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The offending club.</param>
    /// <param name="PlayerId">The skater waved out.</param>
    /// <param name="Position">The faceoff dot.</param>
    /// <param name="TakerId">The skater who takes the draw instead, or -1 after a second violation (a false start or a minor).</param>
    public sealed record FaceoffViolated(
        float Time,
        Side Side,
        int PlayerId,
        Vector3 Position,
        int TakerId
    ) : MatchEvent(Time);
}
