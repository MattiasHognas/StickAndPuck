using StickAndPuck.App.Core;

namespace StickAndPuck.App.Referee
{
    /// <summary>A penalty the referee has signalled and will call at the next stoppage (the penalty rules decide calls).</summary>
    /// <param name="Infraction">What is being called: tripping, hooking or slashing.</param>
    /// <param name="PlayerId">The offending player.</param>
    /// <param name="Time">When the arm went up, in <see cref="World.Time"/>.</param>
    public readonly record struct RefereeCall(string Infraction, int PlayerId, float Time);
}
