using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Camera;

/// <summary>
/// W9: the camera. The gameplay camera pans along the rink after the puck with a lead, at a fixed
/// height and tilt, in whole pixels, and zooms with speed when <see cref="Settings.CameraZoom"/> is on.
/// Goals cut to a low angled replay. Runs after every gameplay system, so it sees this step's
/// positions and events. F6 toggles the zoom while trying it out (a dev key); SAP_CAMERA_ZOOM=0 or 1
/// sets it at start, for headless checks.
/// </summary>
internal sealed class CameraSystem : ISystem
{
    readonly FollowCamera follow = new();
    readonly Replay replay = new();

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        string? zoom = Environment.GetEnvironmentVariable("SAP_CAMERA_ZOOM");
        if (zoom is "0" or "1") world.Settings = world.Settings with { CameraZoom = string.Equals(zoom, "1", StringComparison.Ordinal) };
        follow.Cut(world);
        world.Camera = follow.Camera;
    }

    public void Update(World world, float dt)
    {
        if (world.Match.Paused) return;   // the frame holds while a screen is up
        if (IsKeyPressed(KeyboardKey.F6)) world.Settings = world.Settings with { CameraZoom = !world.Settings.CameraZoom };

        if (replay.Update(world, dt)) follow.Cut(world);
        if (Replay.Playing)
        {
            world.Camera = replay.Camera;
            return;
        }

        follow.Update(world, dt);
        world.Camera = follow.Camera;
    }

    public void DrawHud(World world, RenderContext render)
    {
        replay.DrawHud();
    }
}
