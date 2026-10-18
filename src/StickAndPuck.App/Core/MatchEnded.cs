namespace StickAndPuck.App.Core
{
    /// <summary>The match ended: <see cref="MatchWon"/> or <see cref="MatchTied"/>.</summary>
    public abstract record MatchEnded(float Time) : MatchEvent(Time);
}
