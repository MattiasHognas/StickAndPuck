namespace StickAndPuck.Tests.Support
{
    /// <summary>What one all-AI match came to: shots on goal, goals, saves (and how many held), each goalie's line, and the AI's moves.</summary>
    public sealed record AllAiMatch(
        int Shots,
        int Goals,
        int Saves,
        int Caught,
        IReadOnlyList<Faced> Goalies,
        Moves Moves
    )
    {
        public override string ToString()
        {
            return $"goals {Goals}, shots on goal {Shots}, dekes {Moves.Dekes} ({Moves.DekesKept} kept), one-timers armed {Moves.Armed}, struck {Moves.Struck} ({Moves.OnGoal} on goal), scored {Moves.OneTimerGoals}, stray {Moves.Stray}";
        }
    }
}
