using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>W4c: moves each human slot between players (sets <see cref="Player.Slot"/>) as <see cref="Settings.Switching"/> says.</summary>
internal sealed class SwitchingSystem : ISystem
{
    public void Update(World world, float dt) { }
}
