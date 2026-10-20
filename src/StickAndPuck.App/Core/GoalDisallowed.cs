using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A goal under review does not count: no score, no stats, play restarts with a faceoff outside the zone.</summary>
    /// <param name="Time">The match time of the call.</param>
    /// <param name="Side">The club whose goal it would have been.</param>
    /// <param name="ScorerId">The player who would have scored.</param>
    /// <param name="Position">Where the puck crossed the line.</param>
    /// <param name="Call">Why: goalie interference or offside on the entry.</param>
    /// <param name="Challenged">True when a coach's challenge took it off the board, false when the review itself did.</param>
    public sealed record GoalDisallowed(
        float Time,
        Side Side,
        int ScorerId,
        Vector3 Position,
        ReviewCall Call,
        bool Challenged
    ) : MatchEvent(Time);
}
