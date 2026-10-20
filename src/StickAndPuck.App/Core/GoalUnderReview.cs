namespace StickAndPuck.App.Core
{
    /// <summary>The puck went in while the goal review rule is on (<see cref="RuleSet.GoalReview"/>): play is held
    /// and the goal is not counted yet. The review ends with the goal published as <see cref="GoalScored"/>
    /// (confirmed) or with <see cref="GoalDisallowed"/>; nothing reacts to the goal before that.</summary>
    /// <param name="Time">The match time the puck crossed the line.</param>
    /// <param name="Goal">The goal as it would count: published once the review confirms it.</param>
    public sealed record GoalUnderReview(float Time, GoalScored Goal) : MatchEvent(Time);
}
