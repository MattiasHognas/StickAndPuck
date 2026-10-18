using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The referee blew the whistle.</summary>
    public sealed record WhistleBlown(float Time, Vector3 Position) : MatchEvent(Time);
}
