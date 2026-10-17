using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>A penalty the referee has signalled and not yet called: play goes on until the offending club touches the puck.</summary>
/// <param name="Side">The offending club.</param>
/// <param name="PlayerId">The offending skater.</param>
/// <param name="Infraction">What was seen.</param>
/// <param name="Time">When the arm went up, in <see cref="World.Time"/>.</param>
/// <param name="Severity">What the call costs; a minor unless the referee saw worse.</param>
public readonly record struct DelayedPenalty(Side Side, int PlayerId, string Infraction, float Time, Severity Severity = Severity.Minor);
