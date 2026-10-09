using StickAndPuck.Core;

namespace StickAndPuck.Hud;

/// <summary>W10: scorebug with crests and short names, clock, period and goal messages, drawn in DrawHud at 480x270 from <see cref="World.Match"/> and <see cref="World.Events"/>.</summary>
internal sealed class HudSystem : ISystem
{
    public bool RunsWhilePaused => true;

    public void Update(World world, float dt) { }
}
