using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>W7a: faceoffs, the clock, three periods, goals and restarts. Owns <see cref="World.Match"/> and publishes Faceoff, Goal, PeriodEnd and MatchEnd.</summary>
internal sealed class MatchRules : ISystem
{
    public void Update(World world, float dt) { }
}
