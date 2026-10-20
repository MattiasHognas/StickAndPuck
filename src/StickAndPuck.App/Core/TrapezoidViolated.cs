using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A goalie played the puck behind his own goal line outside the trapezoid (<see cref="RuleSet.Trapezoid"/>):
    /// a delay-of-game minor, served by a skater of his club, follows in the same step.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The goalie's club.</param>
    /// <param name="GoalieId">The goalie.</param>
    /// <param name="Position">Where he played the puck.</param>
    public sealed record TrapezoidViolated(float Time, Side Side, int GoalieId, Vector3 Position)
        : MatchEvent(Time);
}
