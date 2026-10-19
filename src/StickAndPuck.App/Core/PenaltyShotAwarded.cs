using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The referee awards a penalty shot: a skater on a clean breakaway was fouled from behind
    /// (<see cref="RuleSet.PenaltyShot"/>). Play stops and he takes the shot alone from centre against the goalie.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The club awarded the shot.</param>
    /// <param name="ShooterId">The fouled skater, who takes the shot.</param>
    /// <param name="OffenderId">The skater who fouled him.</param>
    /// <param name="Position">Where the foul happened.</param>
    public sealed record PenaltyShotAwarded(
        float Time,
        Side Side,
        int ShooterId,
        int OffenderId,
        Vector3 Position
    ) : MatchEvent(Time);
}
