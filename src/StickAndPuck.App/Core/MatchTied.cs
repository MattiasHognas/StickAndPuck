namespace StickAndPuck.App.Core
{
    /// <summary>The match ended level.</summary>
    public sealed record MatchTied(float Time) : MatchEnded(Time);
}
