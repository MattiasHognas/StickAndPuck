using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>A fight was decided: one fighter won it, the other lost it.</summary>
    /// <param name="Time">Match time in seconds.</param>
    /// <param name="WinnerId">The fighter who won.</param>
    /// <param name="LoserId">The fighter who lost.</param>
    /// <param name="Position">Where the fight was.</param>
    public sealed record FightEnded(float Time, int WinnerId, int LoserId, Vector3 Position)
        : MatchEvent(Time);
}
