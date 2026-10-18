using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>What the referee might call for one check or one chase, and how likely it is.</summary>
    /// <param name="PlayerId">The offending skater.</param>
    /// <param name="Infraction">One of <see cref="Infraction"/>.</param>
    /// <param name="Chance">The chance the referee calls it, before <see cref="WorldSwitches.PenaltyStrictness"/>.</param>
    public readonly record struct Foul(int PlayerId, string Infraction, float Chance);
}
