namespace StickAndPuck.App.Core
{
    /// <summary>A club called its time-out at a stoppage; play waits <see cref="Rules.Match.MatchTuning.TimeOutSeconds"/> before the faceoff.</summary>
    public sealed record TimeOutCalled(float Time, Side Side) : MatchEvent(Time);
}
