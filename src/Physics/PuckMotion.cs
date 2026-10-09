using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Physics;

/// <summary>What the puck hit during a step.</summary>
public enum PuckHit
{
    /// <summary>The boards.</summary>
    Boards,

    /// <summary>The glass above the boards.</summary>
    Glass,

    /// <summary>A post or the crossbar.</summary>
    Post,

    /// <summary>The net mesh or frame.</summary>
    Net,

    /// <summary>A player's body.</summary>
    Body,

    /// <summary>The ice, landing from the air.</summary>
    Ice,
}

/// <summary>One impact: what was hit, where, how hard (speed into the surface) and, for a body, whose.</summary>
/// <param name="Kind">The surface.</param>
/// <param name="Position">The puck's position at the hit.</param>
/// <param name="Speed">Speed into the surface, metres per second.</param>
/// <param name="PlayerId">The player hit, or -1.</param>
public readonly record struct PuckImpact(PuckHit Kind, Vector3 Position, float Speed, int PlayerId = -1);

/// <summary>A body the puck can bounce off: a standing cylinder.</summary>
/// <param name="PlayerId">Whose body.</param>
/// <param name="Position">Its foot on the ice.</param>
/// <param name="Velocity">How it moves.</param>
public readonly record struct PuckBody(int PlayerId, Vector3 Position, Vector3 Velocity);

/// <summary>
/// Moves a loose puck: gravity, ice friction and drag, boards with rounded corners, glass, posts,
/// crossbar, net mesh and bodies. Pure maths on a position and a velocity, so it runs without a window
/// and the self-test can drive it. The puck's <see cref="Puck.Position"/> has y = 0 when it lies
/// on the ice; x and z are its centre.
/// </summary>
public static class PuckMotion
{
    /// <summary>Half the puck's thickness (25 mm puck).</summary>
    public const float HalfThickness = 0.0127f;

    /// <summary>Advances a loose puck by <paramref name="dt"/>, in sub-steps short enough that a fast shot cannot skip a post.</summary>
    /// <param name="position">The puck position, changed in place.</param>
    /// <param name="velocity">The puck velocity, changed in place.</param>
    /// <param name="dt">The step length in seconds.</param>
    /// <param name="bodies">Bodies on the ice.</param>
    /// <param name="impacts">Receives every impact.</param>
    public static void Step(ref Vector3 position, ref Vector3 velocity, float dt, ReadOnlySpan<PuckBody> bodies, List<PuckImpact> impacts)
    {
        float move = velocity.Length() * dt;
        int steps = Math.Clamp((int)MathF.Ceiling(move / PuckTuning.MaxSubstepMove), 1, 64);
        float h = dt / steps;
        for (int i = 0; i < steps; i++) SubStep(ref position, ref velocity, h, bodies, impacts);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            position = Vector3.Zero;
            velocity = Vector3.Zero;
        }
    }

    static void SubStep(ref Vector3 p, ref Vector3 v, float h, ReadOnlySpan<PuckBody> bodies, List<PuckImpact> impacts)
    {
        var prev = p;

        // ice: a puck on the ice slides with friction, a puck in the air falls
        bool onIce = p.Y <= 0 && v.Y <= 0;
        if (onIce)
        {
            p.Y = 0; v.Y = 0;
            var flat = new Vector2(v.X, v.Z);
            float speed = flat.Length();
            float slower = Math.Max(0, speed - PuckTuning.IceFriction * h);
            if (speed > 0) { v.X *= slower / speed; v.Z *= slower / speed; }
        }
        else
        {
            v.Y -= PuckTuning.Gravity * h;
        }
        v *= 1 - PuckTuning.Drag * h;
        p += v * h;

        if (p.Y < 0)
        {
            p.Y = 0;
            if (-v.Y > PuckTuning.LandSpeed)
            {
                impacts.Add(new PuckImpact(PuckHit.Ice, p, -v.Y));
                v.Y = -v.Y * PuckTuning.IceBounce;
                v.X *= 0.9f; v.Z *= 0.9f;
            }
            else v.Y = 0;
        }

        Boards(ref p, ref v, impacts);
        for (int end = -1; end <= 1; end += 2)
        {
            Net(end, prev, ref p, ref v, h, impacts);
            Posts(end, ref p, ref v, impacts);
        }
        foreach (var b in bodies) Body(b, ref p, ref v, impacts);
    }

    /// <summary>The rink outline: straight boards with rounded corners, glass above, netting above that.</summary>
    static void Boards(ref Vector3 p, ref Vector3 v, List<PuckImpact> impacts)
    {
        float r = RinkSpec.PuckRadius, cr = RinkSpec.CornerRadius;
        float cx = RinkSpec.HalfLength - cr, cz = RinkSpec.HalfWidth - cr;
        Vector2 n;          // outward normal of the wall the puck touches
        float depth;        // how far past the wall
        float ax = MathF.Abs(p.X), az = MathF.Abs(p.Z);
        if (ax > cx && az > cz)
        {
            var c = new Vector2(MathF.Sign(p.X) * cx, MathF.Sign(p.Z) * cz);
            var d = new Vector2(p.X, p.Z) - c;
            float dist = d.Length();
            depth = dist - (cr - r);
            if (depth <= 0) return;
            n = d / dist;
        }
        else
        {
            float dx = ax - (RinkSpec.HalfLength - r), dz = az - (RinkSpec.HalfWidth - r);
            if (dx <= 0 && dz <= 0) return;
            if (dx > dz) { depth = dx; n = new Vector2(MathF.Sign(p.X), 0); }
            else { depth = dz; n = new Vector2(0, MathF.Sign(p.Z)); }
        }
        p.X -= n.X * depth; p.Z -= n.Y * depth;
        bool glass = p.Y + HalfThickness > RinkSpec.BoardHeight;
        Reflect(ref v, n, glass ? PuckTuning.GlassBounce : PuckTuning.BoardBounce, PuckTuning.WallGrip,
            glass ? PuckHit.Glass : PuckHit.Boards, p, impacts);
    }

    /// <summary>
    /// The net behind one goal line: a box open at the mouth. A puck inside stays inside (the mesh
    /// swallows it); a puck outside bounces off the back, sides and top.
    /// </summary>
    static void Net(int end, Vector3 prev, ref Vector3 p, ref Vector3 v, float h, List<PuckImpact> impacts)
    {
        float r = RinkSpec.PuckRadius, w = RinkSpec.NetWidth / 2, d = RinkSpec.NetDepth, top = RinkSpec.NetHeight;
        // u: depth behind the goal line toward the back of the net
        float U(Vector3 q) => end * q.X - RinkSpec.GoalLineX;
        float u = U(p), pu = U(prev);
        bool wasIn = pu > 0 && pu < d && MathF.Abs(prev.Z) < w && prev.Y < top;
        if (wasIn)
        {
            float keep = Math.Max(0, 1 - PuckTuning.NetDrag * h);
            v.X *= keep; v.Z *= keep;
            if (u > d - r) { SetU(ref p, end, d - r); Mesh(ref v, new Vector2(end, 0), p, impacts); }
            if (MathF.Abs(p.Z) > w - r) { p.Z = MathF.Sign(p.Z) * (w - r); Mesh(ref v, new Vector2(0, MathF.Sign(p.Z)), p, impacts); }
            if (p.Y + 2 * HalfThickness > top && U(p) > 0)
            {
                p.Y = top - 2 * HalfThickness;
                if (v.Y > 0) { impacts.Add(new PuckImpact(PuckHit.Net, p, v.Y)); v.Y = -v.Y * PuckTuning.MeshBounce; }
            }
            return;
        }

        // from outside: the frame is the box grown by the puck's radius, minus the mouth
        bool overlaps = u > 0 && u < d + r && MathF.Abs(p.Z) < w + r && p.Y < top;
        if (!overlaps) return;
        if (pu >= d + r - 1e-4f)
        {
            SetU(ref p, end, d + r);
            Reflect(ref v, new Vector2(-end, 0), PuckTuning.NetOutsideBounce, PuckTuning.WallGrip, PuckHit.Net, p, impacts);
        }
        else if (MathF.Abs(prev.Z) >= w + r - 1e-4f || (pu > 0 && MathF.Abs(prev.Z) >= w))
        {
            float s = MathF.Sign(prev.Z);
            p.Z = s * (w + r);
            Reflect(ref v, new Vector2(0, -s), PuckTuning.NetOutsideBounce, PuckTuning.WallGrip, PuckHit.Net, p, impacts);
        }
        else if (prev.Y >= top)
        {
            p.Y = top;
            if (v.Y < 0) { impacts.Add(new PuckImpact(PuckHit.Net, p, -v.Y)); v.Y = -v.Y * PuckTuning.NetOutsideBounce; }
        }
        // otherwise it came in through the mouth: the posts and crossbar already had their say
    }

    static void SetU(ref Vector3 p, int end, float u)
    {
        p.X = end * (RinkSpec.GoalLineX + u);
    }

    static void Mesh(ref Vector3 v, Vector2 outward, Vector3 p, List<PuckImpact> impacts)
    {
        Reflect(ref v, outward, PuckTuning.MeshBounce, 0.6f, PuckHit.Net, p, impacts);
    }

    /// <summary>Two posts standing on the goal line and the crossbar between them.</summary>
    static void Posts(int end, ref Vector3 p, ref Vector3 v, List<PuckImpact> impacts)
    {
        float x0 = end * RinkSpec.GoalLineX, w = RinkSpec.NetWidth / 2, top = RinkSpec.NetHeight;
        float reach = RinkSpec.PuckRadius + PuckTuning.PostRadius;
        if (MathF.Abs(p.X - x0) > reach + 0.05f) return;

        if (p.Y < top)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var d = new Vector2(p.X - x0, p.Z - s * w);
                float dist = d.Length();
                if (dist >= reach || dist < 1e-6f) continue;
                var n = d / dist;
                p.X = x0 + n.X * reach; p.Z = s * w + n.Y * reach;
                Reflect(ref v, -n, PuckTuning.PostBounce, 0.05f, PuckHit.Post, p, impacts);
            }
        }

        // crossbar: a bar along z at the top of the posts; the puck is a ball at its middle height
        if (MathF.Abs(p.Z) < w)
        {
            var d = new Vector2(p.X - x0, p.Y + HalfThickness - top);
            float dist = d.Length();
            if (dist < reach && dist > 1e-6f)
            {
                var n = d / dist;
                p.X = x0 + n.X * reach; p.Y = Math.Max(0, top + n.Y * reach - HalfThickness);
                var v2 = new Vector2(v.X, v.Y);
                float into = Vector2.Dot(v2, n);
                if (into < 0)
                {
                    if (-into > PuckTuning.EventSpeed) impacts.Add(new PuckImpact(PuckHit.Post, p, -into));
                    v2 -= (1 + PuckTuning.PostBounce) * into * n;
                    v.X = v2.X; v.Y = v2.Y;
                }
            }
        }
    }

    /// <summary>A body: a standing cylinder that soaks up most of the hit and carries its own speed into the puck.</summary>
    static void Body(PuckBody b, ref Vector3 p, ref Vector3 v, List<PuckImpact> impacts)
    {
        if (p.Y > PuckTuning.BodyHeight) return;
        float reach = PuckTuning.BodyRadius + RinkSpec.PuckRadius;
        var d = new Vector2(p.X - b.Position.X, p.Z - b.Position.Z);
        float dist = d.Length();
        if (dist >= reach) return;
        var n = dist > 1e-6f ? d / dist : Vector2.UnitX;
        p.X = b.Position.X + n.X * reach; p.Z = b.Position.Z + n.Y * reach;
        var rel = new Vector2(v.X - b.Velocity.X, v.Z - b.Velocity.Z);
        float into = Vector2.Dot(rel, n);
        if (into >= 0) return;
        impacts.Add(new PuckImpact(PuckHit.Body, p, -into, b.PlayerId));
        rel -= (1 + PuckTuning.BodyBounce) * into * n;
        rel *= 0.8f;
        v.X = rel.X + b.Velocity.X; v.Z = rel.Y + b.Velocity.Z;
    }

    /// <summary>Bounces the horizontal velocity off a wall with the given outward normal.</summary>
    static void Reflect(ref Vector3 v, Vector2 outward, float bounce, float grip, PuckHit kind, Vector3 p, List<PuckImpact> impacts)
    {
        var v2 = new Vector2(v.X, v.Z);
        float into = Vector2.Dot(v2, outward);
        if (into <= 0) return;
        if (into > PuckTuning.EventSpeed) impacts.Add(new PuckImpact(kind, p, into));
        // Coulomb rubbing: the slide along the wall loses in proportion to how hard the puck hit it,
        // so a puck rimming round a corner keeps its pace while a hard square hit loses most of it
        var along = v2 - into * outward;
        float slide = along.Length();
        if (slide > 0) along *= Math.Max(0, slide - grip * (1 + bounce) * into) / slide;
        v2 = along - into * bounce * outward;
        v.X = v2.X; v.Z = v2.Y;
    }

    /// <summary>
    /// The goal-line test: true when the whole puck is past the whole goal line, between the posts
    /// and under the crossbar, at the given end.
    /// </summary>
    /// <param name="position">The puck position.</param>
    /// <param name="end">-1 for the net at -x, +1 for the net at +x.</param>
    /// <returns>Whether a goal would count, by position alone.</returns>
    public static bool FullyOverGoalLine(Vector3 position, int end)
    {
        float u = end * position.X - RinkSpec.GoalLineX;
        return u > RinkSpec.PuckRadius + GoalLineHalfWidth && u < RinkSpec.NetDepth
            && MathF.Abs(position.Z) < RinkSpec.NetWidth / 2 && position.Y < RinkSpec.NetHeight;
    }

    /// <summary>Half the width of the painted goal line (50 mm line); the puck must clear all of it.</summary>
    public const float GoalLineHalfWidth = 0.025f;
}
