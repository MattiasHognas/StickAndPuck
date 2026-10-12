using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Physics;

/// <summary>
/// W62: the goal cage is solid for players too. Each net stands on the ice as a box from the posts on
/// the goal line to the back of the cage, the same footprint <see cref="PuckMotion"/> uses for the puck;
/// a player is a standing circle that slides along it and can never skate over or through it, from
/// behind, from the sides or in through the mouth. The nets never move.
/// A player pushing into the cage is steered along it, so AI skaters and goalies heading for a spot on
/// the far side go around it instead of stopping dead against the frame. A goalie has a slimmer circle
/// and may stand with its centre right on the goal line between the posts, as it does in its crease.
/// </summary>
public static class NetBody
{
    /// <summary>Keeps every player on the ice out of both cages. A carried puck moves with its carrier.</summary>
    /// <param name="world">The world.</param>
    /// <param name="dt">The step in seconds.</param>
    public static void Resolve(World world, float dt)
    {
        var pk = world.Puck;
        foreach (var p in world.Players)
        {
            if (!p.OnIce) continue;
            var before = p.Position;
            var position = before;
            var velocity = p.Velocity;
            var stick = p.IsGoalie ? Heading(velocity) : p.Command.Move;
            for (int end = -1; end <= 1; end += 2)
                Push(ref position, ref velocity, stick, end, p.IsGoalie, dt);
            if (position == before) continue;
            p.Position = position;
            p.Velocity = velocity;
            if (pk.Carrier == p.Id) pk.Position += position - before;
        }
    }

    /// <summary>Whether a player standing at a point overlaps the cage at one end.</summary>
    /// <param name="p">The player's position.</param>
    /// <param name="end">-1 for the net at -x, +1 for the net at +x.</param>
    /// <param name="goalie">True for a goalie, which may stand on the goal line between the posts.</param>
    /// <param name="slack">How far it may overlap, for tests.</param>
    /// <returns>True when it reaches into the frame.</returns>
    public static bool Overlaps(Vector3 p, int end, bool goalie = false, float slack = 0.02f)
    {
        var (u, z) = Local(p, end);
        float r = Radius(goalie) - slack;
        var closest = new Vector2(Math.Clamp(u, Front(goalie), Back), Math.Clamp(z, -Side, Side));
        return Vector2.DistanceSquared(closest, new Vector2(u, z)) < r * r;
    }

    static float Radius(bool goalie) => goalie ? PuckTuning.GoalieNetRadius : PuckTuning.SkaterNetRadius;

    /// <summary>The front of the frame: the posts, or for a goalie a little behind them, so its centre can reach the goal line.</summary>
    static float Front(bool goalie) => goalie ? PuckTuning.GoalieNetRadius - PuckTuning.PostRadius : -PuckTuning.PostRadius;

    static float Back => RinkSpec.NetDepth;

    static float Side => RinkSpec.NetWidth / 2 + PuckTuning.PostRadius;

    /// <summary>Depth behind the goal line and sideways offset at one end.</summary>
    static (float U, float Z) Local(Vector3 p, int end) => (PuckMotion.Depth(p, end), p.Z);

    /// <summary>A goalie moves by spots, not a stick: the way it is skating stands in for one.</summary>
    static Vector2 Heading(Vector3 v)
    {
        var flat = new Vector2(v.X, v.Z);
        float speed = flat.Length();
        return speed > PuckTuning.GoalieMoving ? flat / speed : Vector2.Zero;
    }

    static void Push(ref Vector3 p, ref Vector3 v, Vector2 move, int end, bool goalie, float dt)
    {
        var (u, z) = Local(p, end);
        float radius = Radius(goalie), front = Front(goalie);
        if (u < front - radius || u > Back + radius || MathF.Abs(z) > Side + radius) return;

        if (!Contact(u, z, front, radius, out var normal, out float depth)) return;
        u += normal.X * depth;
        z += normal.Y * depth;
        var slide = Around(u - (front + Back) / 2, z, normal, new Vector2(end * move.X, move.Y));
        if (goalie)
        {
            // a goalie's spot steering sets its velocity afresh every step, so move it along the frame here
            u += slide.X * PuckTuning.AroundSpeed * dt;
            z += slide.Y * PuckTuning.AroundSpeed * dt;
        }

        p.X = end * (RinkSpec.GoalLineX + u);
        p.Z = z;

        var world = new Vector2(end * normal.X, normal.Y);
        float into = v.X * world.X + v.Z * world.Y;
        if (into < 0)
        {
            v.X -= into * world.X;
            v.Z -= into * world.Y;
        }

        // a skater keeps its speed from step to step, so give it the slide as speed along the frame
        var way = new Vector2(end * slide.X, slide.Y);
        float along = v.X * way.X + v.Z * way.Y;
        if (!goalie && slide != Vector2.Zero && along < PuckTuning.AroundSpeed)
        {
            v.X += (PuckTuning.AroundSpeed - along) * way.X;
            v.Z += (PuckTuning.AroundSpeed - along) * way.Y;
        }
    }

    /// <summary>Where a circle touches the frame: the outward normal and how far it reaches in.</summary>
    static bool Contact(float u, float z, float front, float radius, out Vector2 normal, out float depth)
    {
        var c = new Vector2(u, z);
        var closest = new Vector2(Math.Clamp(u, front, Back), Math.Clamp(z, -Side, Side));
        if (closest != c)
        {
            var d = c - closest;
            float dist = d.Length();
            normal = dist > 1e-6f ? d / dist : Vector2.UnitX;
            depth = radius - dist;
            return dist < radius;
        }

        // the centre is inside the frame: leave through the nearest face
        float toFront = u - front, toBack = Back - u, toSide = Side - MathF.Abs(z);
        if (toFront <= toBack && toFront <= toSide) { normal = new Vector2(-1, 0); depth = toFront + radius; }
        else if (toBack <= toSide) { normal = new Vector2(1, 0); depth = toBack + radius; }
        else { normal = new Vector2(0, z < 0 ? -1 : 1); depth = toSide + radius; }
        return true;
    }

    /// <summary>
    /// A player pushing into the frame slides off it to get around, the way its stick points along the
    /// frame, else toward the nearer edge, like a skater turning off the net instead of stopping dead.
    /// </summary>
    /// <param name="u">Depth from the middle of the frame.</param>
    /// <param name="z">Sideways offset.</param>
    /// <param name="normal">The frame's outward normal at the contact, in depth and sideways.</param>
    /// <param name="stick">The way the player wants to go, in depth and sideways, up to length 1.</param>
    /// <returns>The unit direction to slide in, in depth and sideways, or zero.</returns>
    static Vector2 Around(float u, float z, Vector2 normal, Vector2 stick)
    {
        if (-Vector2.Dot(stick, normal) <= PuckTuning.PushingIntent) return Vector2.Zero;
        var tangent = new Vector2(-normal.Y, normal.X);
        float along = Vector2.Dot(stick, tangent);
        float edge = Vector2.Dot(new Vector2(u, z), tangent);
        // on a flat face the nearer edge wins unless the stick clearly says otherwise; round a corner the
        // stick's lean decides, since the nearer edge flips there
        bool corner = MathF.Abs(normal.X) > 0.2f && MathF.Abs(normal.Y) > 0.2f;
        float intent = corner ? 0.05f : PuckTuning.SlideIntent;
        float way = MathF.Abs(along) > intent ? MathF.Sign(along) : edge < 0 ? -1 : 1;
        return tangent * way;
    }
}
