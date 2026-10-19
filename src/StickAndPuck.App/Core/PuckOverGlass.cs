using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The puck flew out of the rink over the glass while in play (<see cref="RuleSet.PuckOverGlass"/>).
    /// Physics publishes it; the match rules stop play and decide whether it is a delay-of-game minor.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Position">Where the puck crossed the boards.</param>
    /// <param name="PlayerId">The last player to play the puck (shooter or deflector), or -1.</param>
    /// <param name="From">Where that player last played it: the shot's release point, or the deflection.</param>
    /// <param name="Direct">True when it went straight out off that player's shot or clear, touching nothing on the way (no deflection, no boards, posts or net).</param>
    public sealed record PuckOverGlass(
        float Time,
        Vector3 Position,
        int PlayerId,
        Vector3 From,
        bool Direct
    ) : MatchEvent(Time);
}
