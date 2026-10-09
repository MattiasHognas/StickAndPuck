using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>W11: start screen, settings, team select, pause, results and standings. Owns <see cref="MatchState.Paused"/>.</summary>
internal sealed class ScreenSystem : ISystem
{
    public bool RunsWhilePaused => true;

    public void Update(World world, float dt) { }
}
