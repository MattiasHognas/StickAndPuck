using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>One set of numbers for the rink, in metres, Y-up, x along the rink and +z toward the camera.
/// They match the Blender arena (a Blender point (x, y, z) is (x, z, -y) here).</summary>
public static class RinkSpec
{
    /// <summary>Half the rink length, board to board along x (the ice in arena.glb ends at ±30).</summary>
    public const float HalfLength = 30f;

    /// <summary>Half the rink width: the boards' inner face sits at z = ±12.75.</summary>
    public const float HalfWidth = 12.75f;

    /// <summary>Radius of the rounded corners.</summary>
    public const float CornerRadius = 8.5f;

    /// <summary>Goal lines at x = ±26.7.</summary>
    public const float GoalLineX = 26.7f;

    /// <summary>Blue lines centred at x = ±7.62 (30 cm wide, as painted in arena.glb).</summary>
    public const float BlueLineX = 7.62f;

    /// <summary>Height of the boards above the ice.</summary>
    public const float BoardHeight = 1.07f;

    /// <summary>Net mouth width (between the posts).</summary>
    public const float NetWidth = 1.83f;

    /// <summary>Net mouth height (crossbar).</summary>
    public const float NetHeight = 1.22f;

    /// <summary>Net depth behind the goal line.</summary>
    public const float NetDepth = 1.12f;

    /// <summary>Goal crease radius.</summary>
    public const float CreaseRadius = 1.83f;

    /// <summary>Faceoff circle radius.</summary>
    public const float FaceoffRadius = 4.5f;

    /// <summary>Puck radius.</summary>
    public const float PuckRadius = 0.0381f;

    /// <summary>The centre faceoff dot.</summary>
    public static readonly Vector3 CentreDot = Vector3.Zero;

    /// <summary>The nine faceoff dots: centre, four neutral-zone dots and four end-zone dots.</summary>
    public static readonly IReadOnlyList<Vector3> FaceoffDots = Array.AsReadOnly(new[]
    {
        CentreDot,
        new Vector3(-6.1f, 0, -6.7f), new Vector3(-6.1f, 0, 6.7f), new Vector3(6.1f, 0, -6.7f), new Vector3(6.1f, 0, 6.7f),
        new Vector3(-20.7f, 0, -6.7f), new Vector3(-20.7f, 0, 6.7f), new Vector3(20.7f, 0, -6.7f), new Vector3(20.7f, 0, 6.7f),
    });

    /// <summary>The centre of a net mouth on the goal line, for the net at the given end (-1 left, +1 right).</summary>
    public static Vector3 NetMouth(int end)
    {
        return new Vector3(Math.Sign(end) * GoalLineX, 0, 0);
    }
}
