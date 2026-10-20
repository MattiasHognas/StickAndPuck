using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A goal was scored.</summary>
    /// <param name="Time">Match time in seconds.</param>
    /// <param name="Side">The scoring club.</param>
    /// <param name="ScorerId">The scorer: the last player to touch the puck (on an own goal, a player of
    /// the other club), or, when the last touch is no valid player, the nearest on-ice skater of the scoring
    /// club (any of its players as a last resort). Always a valid id: with no player of the scoring club at
    /// all, no goal is published.</param>
    /// <param name="Position">Where the puck crossed the line.</param>
    /// <param name="Speed">Puck speed in metres per second.</param>
    public sealed record GoalScored(
        float Time,
        Side Side,
        int ScorerId,
        Vector3 Position,
        float Speed
    ) : MatchEvent(Time)
    {
        /// <summary>Gets the seconds the goal was held under review before it was confirmed (0 without a review):
        /// the puck crossed the line at <c language="C#">Time - ReviewSeconds</c>.</summary>
        public float ReviewSeconds { get; init; }
    }
}
