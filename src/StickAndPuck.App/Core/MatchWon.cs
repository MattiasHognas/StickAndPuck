namespace StickAndPuck.App.Core
{
    /// <summary>The match ended with a winner.</summary>
    public sealed record MatchWon(float Time, Side Winner) : MatchEnded(Time);
}
