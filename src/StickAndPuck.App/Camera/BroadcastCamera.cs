using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.App.Camera;

/// <summary>The cameras, copied from the Blender scene. Blender is Z-up with the far boards at +Y;
/// glTF and Raylib are Y-up, so a Blender point (x, y, z) is (x, z, -y) here.</summary>
public static class BroadcastCamera
{
    /// <summary>The gameplay camera's height above the ice: the Blender broadcast camera's z 17.5.</summary>
    public const float Height = 17.5f;

    /// <summary>The gameplay camera's distance toward the viewer, its z when centred.</summary>
    public const float Back = 29.67f;

    /// <summary>The gameplay camera looks 28.2 degrees down.</summary>
    public const float TiltDegrees = 28.2f;

    /// <summary>The 50 mm lens on a 24 mm vertical sensor gives a 27 degree vertical field.</summary>
    public const float FovY = 27f;

    /// <summary>The gameplay camera's view direction: straight at the far boards, tilted down.</summary>
    public static Vector3 Forward
    {
        get
        {
            float down = MathF.PI * TiltDegrees / 180f;
            return new Vector3(0, -MathF.Sin(down), -MathF.Cos(down));
        }
    }

    /// <summary>The gameplay camera's screen-up direction: square to <see cref="Forward"/>, tipped toward the far boards.</summary>
    public static Vector3 Up
    {
        get
        {
            float down = MathF.PI * TiltDegrees / 180f;
            return new Vector3(0, MathF.Cos(down), -MathF.Sin(down));
        }
    }

    /// <summary>Gameplay camera: faces the far boards square so board text and markings sit on the pixel
    /// grid, at the Blender broadcast camera's height and tilt.</summary>
    public static Camera3D Straight()
    {
        return Straight(0, Back, FovY);
    }

    /// <summary>The same straight camera moved along the rink (x) or toward the viewer (z). Height and tilt
    /// never change, so the boards stay square to the frame wherever it pans.</summary>
    public static Camera3D Straight(float x, float z, float fovY)
    {
        var pos = new Vector3(x, Height, z);
        return new Camera3D
        {
            Position = pos,
            Target = pos + Forward * 10f,
            Up = Vector3.UnitY,
            FovY = fovY,
            Projection = CameraProjection.Perspective,
        };
    }

    /// <summary>The straight camera moved along the rink (x) and down the frame by a distance in metres: lower
    /// and back toward the viewer along the screen's down direction, with the same tilt. Every point keeps
    /// its distance along the view, so nothing grows or shrinks on screen; the picture only slides up,
    /// bringing the near boards into view while the far boards keep their size.</summary>
    public static Camera3D Lowered(float x, float drop, float fovY)
    {
        var pos = new Vector3(x, Height, Back) - (Up * drop);
        return new Camera3D
        {
            Position = pos,
            Target = pos + Forward * 10f,
            Up = Vector3.UnitY,
            FovY = fovY,
            Projection = CameraProjection.Perspective,
        };
    }

    /// <summary>Replay camera: the low "ice level" chase view, angled across the rink. Used only for short
    /// cuts such as goal replays, where board text need not land on pixel rows.</summary>
    public static Camera3D IceLevel(Vector3 position, Vector3 target)
    {
        return new Camera3D
        {
            Position = position,
            Target = target,
            Up = Vector3.UnitY,
            FovY = 34f,
            Projection = CameraProjection.Perspective,
        };
    }
}
