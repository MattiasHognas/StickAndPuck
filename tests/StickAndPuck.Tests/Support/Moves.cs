namespace StickAndPuck.Tests.Support
{
    /// <summary>The AI's moves in one match: dekes (and how many kept the puck), one-timers armed and struck, passes.</summary>
    public sealed record Moves(
        int Dekes,
        int DekesKept,
        int Armed,
        int Struck,
        int OnGoal,
        int OneTimerGoals,
        int Stray,
        int Passes,
        int Completed
    );
}
