using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>One shootout attempt.</summary>
    public readonly record struct ShootoutShot(Side Side, int PlayerId, bool Scored);
}
