using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>The puck hit the boards or glass.</summary>
    public sealed record PuckHitBoards(float Time, Vector3 Position, float Speed)
        : MatchEvent(Time);
}
