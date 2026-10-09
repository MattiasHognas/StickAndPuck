using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;

namespace StickAndPuck.Goalie;

/// <summary>Where a shot meets a goalie: the offset across the crease from the goalie's centre, the height, and when.</summary>
/// <param name="Offset">Distance along z from the goalie's centre to the puck.</param>
/// <param name="Height">Height of the puck above the ice.</param>
/// <param name="Time">Seconds until the puck gets there.</param>
public readonly record struct ShotLine(float Offset, float Height, float Time);

/// <summary>
/// The goalie's maths, with no Raylib and no randomness, so tests and the AI can use it: where
/// to stand, how fast a goalie reacts and moves from goalie reflexes, where a shot will cross,
/// and whether a stance covers a point.
/// </summary>
public static class GoalieModel
{
    /// <summary>The end of the rink a goalie defends: -1 for the net at -x, +1 for the net at +x.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="goalie">The goalie.</param>
    /// <returns>-1 or +1.</returns>
    public static int End(World world, Player goalie)
    {
        return goalie.Side == world.Match.DefendsLeft ? -1 : 1;
    }

    /// <summary>Blends a value from a goalie with reflexes 0 to one with reflexes 100.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <param name="slow">The value at reflexes 0.</param>
    /// <param name="fast">The value at reflexes 100.</param>
    /// <returns>The value for this goalie.</returns>
    public static float ByReflexes(Traits traits, float slow, float fast)
    {
        return slow + (fast - slow) * Traits.Unit(traits.GoalieReflexes);
    }

    /// <summary>Seconds from a shot being released to the goalie moving toward it.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <returns>The reaction time.</returns>
    public static float ReactionTime(Traits traits)
    {
        return ByReflexes(traits, GoalieTuning.ReactSlow, GoalieTuning.ReactFast);
    }

    /// <summary>Speed of the goalie's push toward a shot.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <returns>Metres per second.</returns>
    public static float SaveSpeed(Traits traits)
    {
        return ByReflexes(traits, GoalieTuning.SaveSpeedSlow, GoalieTuning.SaveSpeedFast);
    }

    /// <summary>
    /// Where a goalie stands for a puck: on the line from the middle of the net to the puck, further
    /// out the further away the puck is, and tight to the near post once the puck is behind the line.
    /// </summary>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="puck">The puck position.</param>
    /// <returns>The spot on the ice.</returns>
    public static Vector3 Spot(int end, Vector3 puck)
    {
        var net = RinkSpec.NetMouth(end);
        float outX = (net.X - puck.X) * end;                       // metres in front of the goal line
        if (outX < 0.3f)
        {
            float side = MathF.Abs(puck.Z) < 0.01f ? 0 : MathF.Sign(puck.Z);
            return new Vector3(net.X - end * 0.3f, 0, side * (RinkSpec.NetWidth / 2 - GoalieTuning.PostHug));
        }
        var toward = new Vector2(-end * outX, puck.Z - net.Z);
        float distance = toward.Length();
        float depth = GoalieTuning.DepthNear + (GoalieTuning.DepthFar - GoalieTuning.DepthNear) * Math.Clamp((distance - 2f) / 7f, 0f, 1f);
        var step = Vector2.Normalize(toward) * depth;
        return new Vector3(net.X + step.X, 0, net.Z + step.Y);
    }

    /// <summary>Whether a loose puck is headed at the net, near enough to the posts to need a save.</summary>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="position">The puck position.</param>
    /// <param name="velocity">The puck velocity.</param>
    /// <returns>True for a shot on this net.</returns>
    public static bool IsShot(int end, Vector3 position, Vector3 velocity)
    {
        if (velocity.X * end < GoalieTuning.ShotSpeed) return false;
        if (position.X * end >= RinkSpec.GoalLineX) return false;
        var at = Cross(position, velocity, end * RinkSpec.GoalLineX);
        return MathF.Abs(at.Offset) < RinkSpec.NetWidth / 2 + GoalieTuning.ShotMargin && at.Height < RinkSpec.NetHeight + GoalieTuning.ShotMargin;
    }

    /// <summary>Where a puck in flight crosses the plane x = <paramref name="planeX"/>, with gravity on a puck off the ice.</summary>
    /// <param name="position">The puck position.</param>
    /// <param name="velocity">The puck velocity; its x must head toward the plane.</param>
    /// <param name="planeX">The plane's x.</param>
    /// <returns>The crossing, with Offset as the puck's z there.</returns>
    public static ShotLine Cross(Vector3 position, Vector3 velocity, float planeX)
    {
        float t = MathF.Abs(velocity.X) < 1e-4f ? float.PositiveInfinity : (planeX - position.X) / velocity.X;
        if (t < 0 || float.IsInfinity(t)) return new ShotLine(float.PositiveInfinity, 0, float.PositiveInfinity);
        float y = position.Y > PuckMotion.HalfThickness || velocity.Y > 0
            ? MathF.Max(0, position.Y + velocity.Y * t - 0.5f * PuckTuning.Gravity * t * t)
            : 0;
        return new ShotLine(position.Z + velocity.Z * t, y, t);
    }

    /// <summary>
    /// How well a stance covers a point on the goalie's plane: 0 means dead centre, 1 the very edge
    /// of the body, pads or hands, and anything above 1 is open net past the goalie.
    /// </summary>
    /// <param name="offset">Distance along z from the goalie's centre, signed.</param>
    /// <param name="height">Height of the puck.</param>
    /// <param name="butterfly">Whether the goalie is down in the butterfly.</param>
    /// <returns>The share of the reach the puck is out.</returns>
    public static float Coverage(float offset, float height, bool butterfly)
    {
        float reach;
        if (height > GoalieTuning.ShoulderHeight + (butterfly ? -0.45f : 0.2f)) reach = GoalieTuning.HandReach * 0.6f;
        else if (height < GoalieTuning.LowShot) reach = butterfly ? GoalieTuning.ButterflyHalfWidth : GoalieTuning.BodyHalfWidth + 0.1f;
        else reach = GoalieTuning.BodyHalfWidth + GoalieTuning.HandReach;
        return MathF.Abs(offset) / reach;
    }
}
