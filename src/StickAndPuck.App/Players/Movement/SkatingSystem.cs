using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>Skaters move from their commands, with top speed and acceleration from their own traits
/// (see <see cref="SkatingModel"/>). Also ages each player's body action, so clips know how far in they are.</summary>
internal sealed class SkatingSystem : ISystem
{
    /// <summary>The top speed of an average fresh player; the skate clip plays at its normal rate there.</summary>
    public const float MaxSpeed = SkatingModel.AverageTopSpeed;

    public void Update(World world, float dt)
    {
        foreach (var p in world.Players)
        {
            p.ActionTime += dt;
            if (!p.OnIce || p.IsGoalie) continue;
            SkatingModel.Step(p, dt);
        }
    }
}
