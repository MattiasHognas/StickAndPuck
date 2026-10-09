using StickAndPuck.Core;

namespace StickAndPuck.Camera;

/// <summary>
/// W9: the gameplay camera. Today it holds the straight broadcast camera; W9 pans it along the
/// rink with a lead, zooms with speed behind <see cref="Settings.CameraZoom"/>, and plays goal replays.
/// </summary>
internal sealed class CameraSystem : ISystem
{
    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        world.Camera = BroadcastCamera.Straight();
    }

    public void Update(World world, float dt) { }
}
