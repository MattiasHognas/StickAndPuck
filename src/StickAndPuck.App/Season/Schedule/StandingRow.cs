namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>One club's line in the table.</summary>
    public sealed record StandingRow(
        string ClubId,
        int Played,
        int Wins,
        int OvertimeWins,
        int OvertimeLosses,
        int Losses,
        int GoalsFor,
        int GoalsAgainst,
        int Points
    )
    {
        /// <summary>Gets the goal difference.</summary>
        public int GoalDifference => GoalsFor - GoalsAgainst;
    }
}
