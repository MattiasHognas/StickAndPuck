using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;

namespace StickAndPuck.Camera;

/// <summary>
/// W58: keeps a moving camera on the low-res pixel grid. A camera that slides by fractions of a
/// pixel makes every edge crawl a little each frame (the "swimming" look); one that moves in whole
/// pixels at the thing it looks at keeps that thing, and everything near its depth, still between
/// steps. The gameplay camera does this along x on its own (<see cref="FollowCamera"/>); this is the
/// same idea for a camera that looks across the rink at an angle.
/// </summary>
public static class PixelGrid
{
    /// <summary>The least slope used for the step along the view, so a camera looking almost level still steps by a bounded amount.</summary>
    const float MinSin = 0.05f;

    /// <summary>The size of one pixel of the frame, in metres, at a distance from a camera.</summary>
    /// <param name="distance">The distance along the view direction.</param>
    /// <param name="fovY">The vertical field of view in degrees.</param>
    /// <returns>The pixel size.</returns>
    public static float PixelAt(float distance, float fovY)
    {
        return 2 * distance * MathF.Tan(fovY * MathF.PI / 360f) / Game.Height;
    }

    /// <summary>
    /// Moves the camera to the nearest whole pixel at its target and moves the target by the same
    /// amount, so the view direction never changes. The steps are measured on the ice, not along the
    /// view: across the view by one pixel, along it (level with the ice) by what lifts the target's
    /// depth one pixel up the frame, and straight up by one pixel's height. A camera following a puck
    /// that slides on the ice then never steps up and down, and everything at the puck's depth moves
    /// by whole pixels.
    /// </summary>
    /// <param name="camera">The camera to snap.</param>
    /// <param name="divisor">The render scale (W59): 2 or 3 snaps to the pixels of the 240x135 or 160x90 frame.</param>
    /// <returns>The snapped camera.</returns>
    public static Camera3D Snap(Camera3D camera, int divisor = 1)
    {
        var view = camera.Target - camera.Position;
        float distance = view.Length();
        var level = new Vector3(view.X, 0, view.Z);
        float run = level.Length();
        if (distance < 1e-4f || run < 1e-4f) return camera;

        var ahead = level / run;
        var right = new Vector3(-ahead.Z, 0, ahead.X);
        float sinDown = Math.Max(MathF.Abs(view.Y) / distance, MinSin);
        float cosDown = run / distance;
        float pixel = PixelAt(distance, camera.FovY) * divisor;

        var pos = camera.Position;
        var snapped = (right * Round(Vector3.Dot(pos, right), pixel))
            + (ahead * Round(Vector3.Dot(pos, ahead), pixel / sinDown))
            + (Vector3.UnitY * Round(pos.Y, pixel / cosDown));
        var move = snapped - pos;
        camera.Position += move;
        camera.Target += move;
        return camera;
    }

    /// <summary>
    /// W59: the render scale that keeps a kit texel about one pixel at the camera's target, and the
    /// camera snapped to the pixels of that scale. The target must sit at the players' depth.
    /// </summary>
    /// <param name="camera">The close camera.</param>
    /// <param name="current">The scale used now, so an easing camera does not flicker between two.</param>
    /// <returns>The snapped camera and its scale.</returns>
    public static (Camera3D Camera, int Divisor) SnapScaled(Camera3D camera, int current)
    {
        float distance = Vector3.Distance(camera.Position, camera.Target);
        int divisor = Render.RenderScale.ForPixel(PixelAt(distance, camera.FovY), current);
        return (Snap(camera, divisor), divisor);
    }

    static float Round(float v, float step)
    {
        return MathF.Round(v / step) * step;
    }
}
