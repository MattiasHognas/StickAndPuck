using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A skater blocked an opponent's fresh shot with the stick, the skates or the body.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The blocker's club.</param>
    /// <param name="BlockerId">The blocker's player id.</param>
    /// <param name="Position">Where the puck met him.</param>
    /// <param name="Speed">The puck's speed against him, in metres per second.</param>
    /// <param name="Slap">True when the blocked shot was a slap shot (or a one-timer).</param>
    public sealed record ShotBlocked(
        float Time,
        Side Side,
        int BlockerId,
        Vector3 Position,
        float Speed,
        bool Slap
    ) : MatchEvent(Time);
}
