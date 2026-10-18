namespace StickAndPuck.App.Core
{
    /// <summary>A period ended.</summary>
    public sealed record PeriodEnded(float Time, int Period) : MatchEvent(Time);
}
