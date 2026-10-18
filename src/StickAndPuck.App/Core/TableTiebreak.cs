namespace StickAndPuck.App.Core
{
    /// <summary>How clubs level on points are ordered in the standings. Every order ends on goal difference,
    /// goals scored, regulation wins and club id, so the table never flickers.</summary>
    public enum TableTiebreak
    {
        /// <summary>Goal difference, then goals scored, then the points in the games between the tied clubs (SHL).</summary>
        GoalDifference,

        /// <summary>Regulation wins, then all wins, then the points in the games between the tied clubs, then goal difference (NHL).</summary>
        RegulationWins,

        /// <summary>The points in the games between the tied clubs first, then goal difference.</summary>
        HeadToHead,
    }
}
