using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>How a match ended.</summary>
    internal sealed record MatchResult(
        MatchSetup Setup,
        int HomeGoals,
        int AwayGoals,
        int HomeShots,
        int AwayShots,
        string Decided,
        IReadOnlyList<GoalLine> Goals,
        MatchPlan Plan
    );
}
