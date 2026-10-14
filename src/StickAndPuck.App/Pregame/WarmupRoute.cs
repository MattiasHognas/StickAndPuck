using System.Numerics;
using StickAndPuck.App.Core;
using System.Collections.Immutable;

namespace StickAndPuck.App.Pregame;

/// <summary>One skater's warm-up loop: a point that travels round an oval in the squad's own end, which the
/// skater chases. Each squad gets laps of the zone, circles round its faceoff dots and crossovers
/// along the blue line, so the ice looks busy without anyone crossing centre.</summary>
/// <param name="Centre">The oval's centre on the ice (x, z).</param>
/// <param name="Radius">Its half sizes along x and z.</param>
/// <param name="Speed">How fast the chased point travels, in metres per second.</param>
/// <param name="Phase">Where on the oval the point begins, in radians.</param>
/// <param name="Clockwise">Which way round.</param>
public sealed record WarmupRoute(Vector2 Centre, Vector2 Radius, float Speed, float Phase, bool Clockwise)
{
    // the loops for the squad defending -x, in order of the skaters as they stand: two zone laps,
    // two circles round the faceoff dots and one set of crossovers inside the blue line
    static readonly ImmutableArray<WarmupRoute> Shapes =
    [
        new(new(-17f, 0), new(8f, 9f), 5.5f, 0f, true),
        new(new(-17f, 0), new(8f, 9f), 5.5f, MathF.PI, true),
        new(new(-20f, -7f), new(4.5f, 4.5f), 4.5f, 0f, false),
        new(new(-20f, 7f), new(4.5f, 4.5f), 4.5f, MathF.PI, true),
        new(new(-10f, 0), new(2.5f, 7f), 4f, MathF.PI / 2, false),
    ];

    /// <summary>Gets a route for a skater in the end a squad defends.</summary>
    public static WarmupRoute For(int index, int end)
    {
        var shape = Shapes[index % Shapes.Length];
        return end < 0 ? shape : shape with { Centre = shape.Centre with { X = -shape.Centre.X }, Clockwise = !shape.Clockwise };
    }

    /// <summary>Gets where the chased point is after some seconds.</summary>
    public Vector2 At(float seconds)
    {
        // angular speed from the oval's mean radius, so every loop is skated at about its Speed
        float mean = (Radius.X + Radius.Y) / 2f;
        float angle = Phase + (Clockwise ? -1f : 1f) * Speed / mean * seconds;
        return Centre + new Vector2(Radius.X * SimMath.Cos(angle), Radius.Y * SimMath.Sin(angle));
    }

    /// <summary>Gets a spot just inside the blue line of the squad's end, where its skaters gather once start is pressed.</summary>
    public static Vector2 Gather(int index, int end)
    {
        return new Vector2(end * (RinkSpec.BlueLineX + 3f), -6f + (3f * (index % 5)));
    }
}
