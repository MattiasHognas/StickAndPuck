namespace StickAndPuck.App.Core
{
    /// <summary>A club's coach challenges the goal it just conceded (<see cref="RuleSet.GoalChallenge"/>), once a game:
    /// the goal is looked at again for <see cref="Call"/>. A challenge that holds disallows it
    /// (<see cref="GoalDisallowed"/>); one that fails lets it stand and costs a delay-of-game minor.</summary>
    /// <param name="Time">The match time.</param>
    /// <param name="Side">The challenging club.</param>
    /// <param name="Call">What it challenges for.</param>
    public sealed record CoachChallenged(float Time, Side Side, ReviewCall Call) : MatchEvent(Time);
}
