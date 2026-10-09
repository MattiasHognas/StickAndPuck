using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.Camera;

/// <summary>
/// The cameras, copied from the Blender scene. Blender is Z-up with the far boards at +Y;
/// glTF and Raylib are Y-up, so a Blender point (x, y, z) is (x, z, -y) here.
/// </summary>
public static class BroadcastCamera
{
    /// <summary>
    /// Gameplay camera: faces the far boards square so board text and markings sit on the pixel
    /// grid. Height and tilt are the Blender broadcast camera's (z 17.5, looking 28.2 degrees
    /// down); the 50 mm lens on a 24 mm vertical sensor gives a 27 degree vertical field.
    /// </summary>
    public static Camera3D Straight()
    {
        var pos = new Vector3(0, 17.5f, 29.67f);
        float down = MathF.PI * 28.2f / 180f;
        var forward = new Vector3(0, -MathF.Sin(down), -MathF.Cos(down));
        return new Camera3D
        {
            Position = pos,
            Target = pos + forward * 10f,
            Up = Vector3.UnitY,
            FovY = 27f,
            Projection = CameraProjection.Perspective,
        };
    }
}
