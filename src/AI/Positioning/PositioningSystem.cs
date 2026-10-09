using StickAndPuck.Core;

namespace StickAndPuck.AI;

/// <summary>W6a: where each AI player skates, by role, in attack and defence, from smart positioning. Writes <see cref="Player.Command"/> for players with no human slot; today it clears them.</summary>
internal sealed class PositioningSystem : ISystem
{
    public void Update(World world, float dt)
    {
        foreach (var p in world.Players)
            if (p.Slot < 0) p.Command = default;
    }
}
