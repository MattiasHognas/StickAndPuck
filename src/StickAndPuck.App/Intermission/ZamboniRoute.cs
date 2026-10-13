using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.Intermission;

/// <summary>
/// The Zamboni's drive between periods: in through a gate in the far boards near the home end,
/// laps that spiral inward from the boards toward the centre, the way the ice gets resurfaced,
/// then back out through the same gate. The route keeps in front of both creases, so the goalies
/// can stay in their nets, and leaves the strip along the far boards free for the skaters waiting
/// at their bench doors. Positions are (x, z) on the ice, for the wheelbase centre.
/// </summary>
internal sealed class ZamboniRoute
{
    /// <summary>Half the length of each lap's straights, in metres.</summary>
    public const float HalfStraight = 12f;

    /// <summary>The first lap's distance from the long axis to the straights, in metres.</summary>
    public const float OuterRadius = 9.5f;

    /// <summary>The last lap's distance from the long axis to the straights, in metres.</summary>
    public const float InnerRadius = 4.5f;

    /// <summary>How many laps it drives, counting the last one that turns off for the gate.</summary>
    public const float Laps = 2f;

    /// <summary>Where the gate is along the rink.</summary>
    public const float GateX = -HalfStraight - 3f;

    /// <summary>Where the Zamboni waits behind the gate, out of sight of the ice.</summary>
    public const float OutsideZ = -RinkSpec.HalfWidth - 2.6f;

    /// <summary>Cruising speed in m/s: brisker than a real one, so the wait stays short.</summary>
    public const float Speed = 6.5f;

    /// <summary>Speeding up and slowing down, in m/s².</summary>
    const float Accel = 2.5f;

    /// <summary>The route is sampled every this many metres.</summary>
    const float Spacing = 0.25f;

    /// <summary>The heading looks this far ahead and behind along the route, which rounds the corners.</summary>
    const float HeadingReach = 1.4f;

    readonly Vector2[] points;
    readonly float[] lengths;

    public ZamboniRoute()
    {
        var list = new List<Vector2>();
        // in through the gate, then a quarter turn onto the far straight of the first lap
        for (float z = OutsideZ; z < -OuterRadius - 3f; z += Spacing) list.Add(new Vector2(GateX, z));
        for (int i = 0; i <= 12; i++)
        {
            float angle = MathF.PI - i * MathF.PI / 2f / 12f;
            list.Add(new Vector2(-HalfStraight + 3f * SimMath.Cos(angle), -OuterRadius - 3f + 3f * SimMath.Sin(angle)));
        }

        // the laps, the radius shrinking a little with every metre; the last one stops halfway
        // round the home-end turn, heading for the far boards, and drives straight out of the gate
        const float a = HalfStraight, r1 = InnerRadius;
        float lastTurn = (4 * a + 1.5f * MathF.PI * r1) / (4 * a + 2 * MathF.PI * r1);
        float laps = Laps - 1 + lastTurn;
        int steps = (int)(laps * 110f / Spacing);
        for (int i = 1; i <= steps; i++)
        {
            float u = laps * i / steps;
            float r = OuterRadius + (InnerRadius - OuterRadius) * u / laps;
            list.Add(Lap(u - MathF.Floor(u), r));
        }

        Line(list, list[^1], new Vector2(GateX, OutsideZ));

        points = [.. list];
        lengths = new float[points.Length];
        for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
    }

    /// <summary>Gets the route's length in metres.</summary>
    public float Length => lengths[^1];

    /// <summary>Gets how long the drive takes in seconds, from standing at the gate to standing outside it again.</summary>
    public float Duration
    {
        get
        {
            float ramp = Speed / Accel;
            return (Length - Speed * ramp) / Speed + 2 * ramp;
        }
    }

    /// <summary>Gets the distance driven after <paramref name="seconds"/>: speeding up, cruising, slowing to a stop at the end.</summary>
    /// <param name="seconds">Time since it set off.</param>
    /// <returns>Metres along the route.</returns>
    public float Distance(float seconds)
    {
        float ramp = Speed / Accel, rampDistance = Speed * ramp / 2, total = Duration;
        if (seconds <= 0) return 0;
        if (seconds >= total) return Length;
        if (seconds < ramp) return Accel * seconds * seconds / 2;
        float left = total - seconds;
        if (left < ramp) return Length - Accel * left * left / 2;
        return rampDistance + (seconds - ramp) * Speed;
    }

    /// <summary>Gets the point on the route at a distance along it.</summary>
    /// <param name="distance">Metres along the route.</param>
    /// <returns>The point (x, z).</returns>
    public Vector2 At(float distance)
    {
        distance = Math.Clamp(distance, 0, Length);
        int i = Array.BinarySearch(lengths, distance);
        if (i >= 0) return points[i];
        i = ~i;
        float t = (distance - lengths[i - 1]) / (lengths[i] - lengths[i - 1]);
        return Vector2.Lerp(points[i - 1], points[i], t);
    }

    /// <summary>Gets the heading at a distance along the route, in radians, 0 facing +z like a player.</summary>
    /// <param name="distance">Metres along the route.</param>
    /// <returns>The heading.</returns>
    public float HeadingAt(float distance)
    {
        var d = At(distance + HeadingReach) - At(distance - HeadingReach);
        return SimMath.Atan2(d.X, d.Y);
    }

    /// <summary>Gets whether the Zamboni is inside the boards at a distance along the route.</summary>
    /// <param name="distance">Metres along the route.</param>
    /// <returns>True on the ice.</returns>
    public bool OnIce(float distance)
    {
        return At(distance).Y > -RinkSpec.HalfWidth;
    }

    /// <summary>A point on one lap: the far straight toward +x, the turn at the away end, the near straight back, the turn at the home end.</summary>
    /// <param name="f">How far round the lap, 0 to 1.</param>
    /// <param name="r">The lap's radius.</param>
    static Vector2 Lap(float f, float r)
    {
        const float a = HalfStraight;
        float perimeter = 4 * a + 2 * MathF.PI * r;
        float s = f * perimeter;
        if (s < 2 * a) return new Vector2(-a + s, -r);
        s -= 2 * a;
        if (s < MathF.PI * r)
        {
            float phi = -MathF.PI / 2 + s / r;
            return new Vector2(a + r * SimMath.Cos(phi), r * SimMath.Sin(phi));
        }
        s -= MathF.PI * r;
        if (s < 2 * a) return new Vector2(a - s, r);
        s -= 2 * a;
        float psi = MathF.PI / 2 + s / r;
        return new Vector2(-a + r * SimMath.Cos(psi), r * SimMath.Sin(psi));
    }

    static void Line(List<Vector2> list, Vector2 from, Vector2 to)
    {
        int n = Math.Max(1, (int)(Vector2.Distance(from, to) / Spacing));
        for (int i = 1; i <= n; i++) list.Add(Vector2.Lerp(from, to, (float)i / n));
    }
}
