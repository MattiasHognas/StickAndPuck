using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A pass was made.</summary>
    public sealed record PassMade(
        float Time,
        Side Side,
        int PasserId,
        Vector3 Position,
        float Speed
    ) : MatchEvent(Time);
}
