using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Physics
{
    /// <summary>Moves a loose puck: gravity, ice friction and drag, boards with rounded corners, glass, posts,
    /// crossbar, net mesh and bodies. Pure maths on a position and a velocity, so it runs without a window
    /// and the self-test can drive it. The puck's <see cref="Puck.Position"/> has y = 0 when it lies
    /// on the ice; x and z are its centre.</summary>
    public static class PuckMotion
    {
        /// <summary>Half the puck's thickness (25 mm puck).</summary>
        public const float HalfThickness = 0.0127f;

        /// <summary>Advances a loose puck by <paramref name="dt"/>, in sub-steps short enough that a fast shot cannot skip a post.</summary>
        /// <param name="position">The puck position, changed in place.</param>
        /// <param name="velocity">The puck velocity, changed in place.</param>
        /// <param name="inNet">
        /// The end (-1, +1) whose net holds the puck because it came in through the mouth, or 0. Changed in
        /// place: set when the puck crosses the goal line between the posts and under the crossbar, cleared
        /// when it leaves again. While 0 the whole cage is solid, so a puck that is found inside without having
        /// come through the mouth is pushed back out through the nearest wall.
        /// </param>
        /// <param name="dt">The step length in seconds.</param>
        /// <param name="bodies">Bodies on the ice.</param>
        /// <param name="impacts">Receives every impact.</param>
        public static void Step(
            ref Vector3 position,
            ref Vector3 velocity,
            ref int inNet,
            float dt,
            ReadOnlySpan<PuckBody> bodies,
            ICollection<PuckImpact> impacts
        )
        {
            _ = Step(
                ref position,
                ref velocity,
                ref inNet,
                dt,
                bodies,
                impacts,
                float.PositiveInfinity
            );
        }

        /// <summary>Advances a loose puck as above, with a top on the wall: a puck that reaches the boards wholly
        /// above <paramref name="wallTop"/> flies out of the rink instead of bouncing back (the puck over the
        /// glass). <see cref="float.PositiveInfinity"/> is netting above the glass that catches everything.</summary>
        /// <returns>True when the puck left the rink this step; it stops at the wall it crossed.</returns>
        public static bool Step(
            ref Vector3 position,
            ref Vector3 velocity,
            ref int inNet,
            float dt,
            ReadOnlySpan<PuckBody> bodies,
            ICollection<PuckImpact> impacts,
            float wallTop
        )
        {
            float move = velocity.Length() * dt;
            int steps = Math.Clamp((int)MathF.Ceiling(move / PuckTuning.MaxSubstepMove), 1, 64);
            float h = dt / steps;
            bool left = false;
            for (int i = 0; i < steps && !left; i++)
            {
                left = SubStep(ref position, ref velocity, ref inNet, h, bodies, impacts, wallTop);
            }

            if (
                !float.IsFinite(position.X)
                || !float.IsFinite(position.Y)
                || !float.IsFinite(position.Z)
            )
            {
                position = Vector3.Zero;
                velocity = Vector3.Zero;
                inNet = 0;
            }

            return left;
        }

        private static bool SubStep(
            ref Vector3 p,
            ref Vector3 v,
            ref int inNet,
            float h,
            ReadOnlySpan<PuckBody> bodies,
            ICollection<PuckImpact> impacts,
            float wallTop
        )
        {
            Vector3 prev = p;

            // ice: a puck on the ice slides with friction, a puck in the air falls
            bool onIce = p.Y <= 0 && v.Y <= 0;
            if (onIce)
            {
                p.Y = 0;
                v.Y = 0;
                Vector2 flat = new(v.X, v.Z);
                float speed = flat.Length();
                float slower = Math.Max(0, speed - (PuckTuning.IceFriction * h));
                if (speed > 0)
                {
                    v.X *= slower / speed;
                    v.Z *= slower / speed;
                }
            }
            else
            {
                v.Y -= PuckTuning.Gravity * h;
            }
            v *= 1 - (PuckTuning.Drag * h);
            p += v * h;

            if (p.Y < 0)
            {
                p.Y = 0;
                if (-v.Y > PuckTuning.LandSpeed)
                {
                    impacts.Add(new PuckImpact(PuckHit.Ice, p, -v.Y));
                    v.Y = -v.Y * PuckTuning.IceBounce;
                    v.X *= 0.9f;
                    v.Z *= 0.9f;
                }
                else
                {
                    v.Y = 0;
                }
            }

            if (Boards(ref p, ref v, impacts, wallTop))
            {
                return true;
            }

            for (int end = -1; end <= 1; end += 2)
            {
                Net(end, prev, ref p, ref v, ref inNet, h, impacts);
                Posts(end, ref p, ref v, impacts);
            }
            foreach (PuckBody b in bodies)
            {
                Body(b, ref p, ref v, impacts);
            }

            return false;
        }

        /// <summary>The rink outline: straight boards with rounded corners, glass above, and above the glass
        /// either netting (an endless wall) or open air: a puck wholly above <paramref name="wallTop"/> at the
        /// wall is out of the rink, and true comes back with the puck left where it crossed.</summary>
        private static bool Boards(
            ref Vector3 p,
            ref Vector3 v,
            ICollection<PuckImpact> impacts,
            float wallTop
        )
        {
            float r = RinkSpec.PuckRadius,
                cr = RinkSpec.CornerRadius;
            float cx = RinkSpec.HalfLength - cr,
                cz = RinkSpec.HalfWidth - cr;
            Vector2 n; // outward normal of the wall the puck touches
            float depth; // how far past the wall
            float ax = MathF.Abs(p.X),
                az = MathF.Abs(p.Z);
            if (ax > cx && az > cz)
            {
                Vector2 c = new(MathF.Sign(p.X) * cx, MathF.Sign(p.Z) * cz);
                Vector2 d = new Vector2(p.X, p.Z) - c;
                float dist = d.Length();
                depth = dist - (cr - r);
                if (depth <= 0)
                {
                    return false;
                }

                n = d / dist;
            }
            else
            {
                float dx = ax - (RinkSpec.HalfLength - r),
                    dz = az - (RinkSpec.HalfWidth - r);
                if (dx <= 0 && dz <= 0)
                {
                    return false;
                }

                if (dx > dz)
                {
                    depth = dx;
                    n = new Vector2(MathF.Sign(p.X), 0);
                }
                else
                {
                    depth = dz;
                    n = new Vector2(0, MathF.Sign(p.Z));
                }
            }
            if (p.Y - HalfThickness > wallTop)
            {
                return true;
            }

            p.X -= n.X * depth;
            p.Z -= n.Y * depth;
            bool glass = p.Y + HalfThickness > RinkSpec.BoardHeight;
            Reflect(
                ref v,
                n,
                glass ? PuckTuning.GlassBounce : PuckTuning.BoardBounce,
                PuckTuning.WallGrip,
                glass ? PuckHit.Glass : PuckHit.Boards,
                p,
                impacts
            );
            return false;
        }

        /// <summary>The net behind one goal line: a cage open only at the mouth. The puck is inside only when it came
        /// in through the mouth (<paramref name="inNet"/> says so); then the mesh swallows it and it bounces
        /// softly off the back, sides and top from within. Any other puck that overlaps the cage, whether it
        /// flew in from behind, from a side or from above, or was carried through the mesh on a stick and let
        /// go there, is pushed back out through the nearest wall and bounces off it.</summary>
        private static void Net(
            int end,
            Vector3 prev,
            ref Vector3 p,
            ref Vector3 v,
            ref int inNet,
            float h,
            ICollection<PuckImpact> impacts
        )
        {
            float r = RinkSpec.PuckRadius,
                w = RinkSpec.NetWidth / 2,
                d = RinkSpec.NetDepth,
                top = RinkSpec.NetHeight;
            float u = Depth(p, end),
                pu = Depth(prev, end);
            if (inNet == end)
            {
                Inside(end, ref p, ref v, ref inNet, h, impacts);
                return;
            }
            if (inNet != 0)
            {
                return;
            }

            // from outside: the cage is the box grown by the puck's radius, minus the mouth
            bool overlaps = u > 0 && u < d + r && MathF.Abs(p.Z) < w + r && p.Y < top;
            if (!overlaps)
            {
                return;
            }

            if (pu <= 1e-4f && MathF.Abs(prev.Z) < w + r && prev.Y < top)
            {
                // it crossed the goal line at the mouth: between the posts and under the crossbar it is in,
                // otherwise the posts and crossbar, which run after this, ring it out
                if (ThroughMouth(p, end))
                {
                    inNet = end;
                }

                return;
            }

            // it came through a wall: push it back out through the nearest one
            float back = d + r - u,
                side = w + r - MathF.Abs(p.Z),
                over = top - p.Y;
            if (back <= side && back <= over)
            {
                SetU(ref p, end, d + r);
                Reflect(
                    ref v,
                    new Vector2(-end, 0),
                    PuckTuning.NetOutsideBounce,
                    PuckTuning.WallGrip,
                    PuckHit.Net,
                    p,
                    impacts
                );
            }
            else if (side <= over)
            {
                float s = (prev.Z != 0) ? MathF.Sign(prev.Z) : ((p.Z != 0) ? MathF.Sign(p.Z) : 1);
                p.Z = s * (w + r);
                Reflect(
                    ref v,
                    new Vector2(0, -s),
                    PuckTuning.NetOutsideBounce,
                    PuckTuning.WallGrip,
                    PuckHit.Net,
                    p,
                    impacts
                );
            }
            else
            {
                p.Y = top;
                if (v.Y < 0)
                {
                    if (-v.Y > PuckTuning.EventSpeed)
                    {
                        impacts.Add(new PuckImpact(PuckHit.Net, p, -v.Y));
                    }

                    v.Y = -v.Y * PuckTuning.NetOutsideBounce;
                }
            }
        }

        /// <summary>A puck that came in through the mouth: the mesh drags it, and it bounces softly off the back, sides and top from within.</summary>
        private static void Inside(
            int end,
            ref Vector3 p,
            ref Vector3 v,
            ref int inNet,
            float h,
            ICollection<PuckImpact> impacts
        )
        {
            float r = RinkSpec.PuckRadius,
                w = RinkSpec.NetWidth / 2,
                d = RinkSpec.NetDepth,
                top = RinkSpec.NetHeight;
            float u = Depth(p, end);
            // back out through the mouth, or lifted clear over the top (the crossbar can do that)
            if (u <= 0 || p.Y >= top)
            {
                inNet = 0;
                return;
            }
            float keep = Math.Max(0, 1 - (PuckTuning.NetDrag * h));
            v.X *= keep;
            v.Z *= keep;
            if (u > d - r)
            {
                SetU(ref p, end, d - r);
                Mesh(ref v, new Vector2(end, 0), p, impacts);
            }
            if (MathF.Abs(p.Z) > w - r)
            {
                p.Z = MathF.Sign(p.Z) * (w - r);
                Mesh(ref v, new Vector2(0, MathF.Sign(p.Z)), p, impacts);
            }
            if (p.Y + (2 * HalfThickness) > top)
            {
                p.Y = top - (2 * HalfThickness);
                if (v.Y > 0)
                {
                    impacts.Add(new PuckImpact(PuckHit.Net, p, v.Y));
                    v.Y = -v.Y * PuckTuning.MeshBounce;
                }
            }
        }

        /// <summary>How far a point is behind the goal line at one end, toward the back of the net (negative in front of it).</summary>
        public static float Depth(Vector3 p, int end)
        {
            return (end * p.X) - RinkSpec.GoalLineX;
        }

        /// <summary>Whether a puck centre lies in the net's opening: past the goal line, between the posts and wholly under the crossbar.</summary>
        private static bool ThroughMouth(Vector3 p, int end)
        {
            return Depth(p, end) > 0
                && MathF.Abs(p.Z) < RinkSpec.NetWidth / 2
                && p.Y + (2 * HalfThickness) < RinkSpec.NetHeight;
        }

        /// <summary>Whether a puck that was at <paramref name="from"/> and is now at <paramref name="to"/> went into the
        /// net at <paramref name="end"/> through its mouth: from in front of the goal line to inside the cage,
        /// between the posts and under the crossbar. For pucks moved by something other than physics, such as
        /// a carrier's stick, where the sub-steps cannot watch the crossing.</summary>
        public static bool EnteredThroughMouth(Vector3 from, Vector3 to, int end)
        {
            float r = RinkSpec.PuckRadius,
                w = RinkSpec.NetWidth / 2;
            return Depth(from, end) <= 1e-4f
                && MathF.Abs(from.Z) < w + r
                && from.Y < RinkSpec.NetHeight
                && InsideNet(to, end);
        }

        /// <summary>Whether a puck centre is inside the cage at one end: past the goal line, in front of the back, between the sides, under the top.</summary>
        public static bool InsideNet(Vector3 p, int end)
        {
            float u = Depth(p, end);
            return u > 0
                && u < RinkSpec.NetDepth
                && MathF.Abs(p.Z) < RinkSpec.NetWidth / 2
                && p.Y < RinkSpec.NetHeight;
        }

        private static void SetU(ref Vector3 p, int end, float u)
        {
            p.X = end * (RinkSpec.GoalLineX + u);
        }

        private static void Mesh(
            ref Vector3 v,
            Vector2 outward,
            Vector3 p,
            ICollection<PuckImpact> impacts
        )
        {
            Reflect(ref v, outward, PuckTuning.MeshBounce, 0.6f, PuckHit.Net, p, impacts);
        }

        /// <summary>Two posts standing on the goal line and the crossbar between them.</summary>
        private static void Posts(
            int end,
            ref Vector3 p,
            ref Vector3 v,
            ICollection<PuckImpact> impacts
        )
        {
            float x0 = end * RinkSpec.GoalLineX,
                w = RinkSpec.NetWidth / 2,
                top = RinkSpec.NetHeight;
            float reach = RinkSpec.PuckRadius + PuckTuning.PostRadius;
            if (MathF.Abs(p.X - x0) > reach + 0.05f)
            {
                return;
            }

            if (p.Y < top)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 d = new(p.X - x0, p.Z - (s * w));
                    float dist = d.Length();
                    if (dist >= reach || dist < 1e-6f)
                    {
                        continue;
                    }

                    Vector2 n = d / dist;
                    p.X = x0 + (n.X * reach);
                    p.Z = (s * w) + (n.Y * reach);
                    Reflect(ref v, -n, PuckTuning.PostBounce, 0.05f, PuckHit.Post, p, impacts);
                }
            }

            // crossbar: a bar along z at the top of the posts; the puck is a ball at its middle height
            if (MathF.Abs(p.Z) < w)
            {
                Vector2 d = new(p.X - x0, p.Y + HalfThickness - top);
                float dist = d.Length();
                if (dist < reach && dist > 1e-6f)
                {
                    Vector2 n = d / dist;
                    p.X = x0 + (n.X * reach);
                    p.Y = Math.Max(0, top + (n.Y * reach) - HalfThickness);
                    Vector2 v2 = new(v.X, v.Y);
                    float into = Vector2.Dot(v2, n);
                    if (into < 0)
                    {
                        if (-into > PuckTuning.EventSpeed)
                        {
                            impacts.Add(new PuckImpact(PuckHit.Post, p, -into));
                        }

                        v2 -= (1 + PuckTuning.PostBounce) * into * n;
                        v.X = v2.X;
                        v.Y = v2.Y;
                    }
                }
            }
        }

        /// <summary>A body: a standing cylinder that soaks up most of the hit and carries its own speed into the puck.</summary>
        private static void Body(
            PuckBody b,
            ref Vector3 p,
            ref Vector3 v,
            ICollection<PuckImpact> impacts
        )
        {
            if (p.Y > PuckTuning.BodyHeight)
            {
                return;
            }

            float reach = PuckTuning.BodyRadius + RinkSpec.PuckRadius;
            Vector2 d = new(p.X - b.Position.X, p.Z - b.Position.Z);
            float dist = d.Length();
            if (dist >= reach)
            {
                return;
            }

            Vector2 n = dist > 1e-6f ? d / dist : Vector2.UnitX;
            p.X = b.Position.X + (n.X * reach);
            p.Z = b.Position.Z + (n.Y * reach);
            Vector2 rel = new(v.X - b.Velocity.X, v.Z - b.Velocity.Z);
            float into = Vector2.Dot(rel, n);
            if (into >= 0)
            {
                return;
            }

            impacts.Add(new PuckImpact(PuckHit.Body, p, -into, b.PlayerId));
            rel -= (1 + PuckTuning.BodyBounce) * into * n;
            rel *= 0.8f;
            v.X = rel.X + b.Velocity.X;
            v.Z = rel.Y + b.Velocity.Z;
        }

        /// <summary>Bounces the horizontal velocity off a wall with the given outward normal.</summary>
        private static void Reflect(
            ref Vector3 v,
            Vector2 outward,
            float bounce,
            float grip,
            PuckHit kind,
            Vector3 p,
            ICollection<PuckImpact> impacts
        )
        {
            Vector2 v2 = new(v.X, v.Z);
            float into = Vector2.Dot(v2, outward);
            if (into <= 0)
            {
                return;
            }

            if (into > PuckTuning.EventSpeed)
            {
                impacts.Add(new PuckImpact(kind, p, into));
            }
            // Coulomb rubbing: the slide along the wall loses in proportion to how hard the puck hit it,
            // so a puck rimming round a corner keeps its pace while a hard square hit loses most of it
            Vector2 along = v2 - (into * outward);
            float slide = along.Length();
            if (slide > 0)
            {
                along *= Math.Max(0, slide - (grip * (1 + bounce) * into)) / slide;
            }

            v2 = along - (into * bounce * outward);
            v.X = v2.X;
            v.Z = v2.Y;
        }

        /// <summary>The goal-line test: true when the whole puck is past the whole goal line, between the posts
        /// and under the crossbar, at the given end.</summary>
        public static bool FullyOverGoalLine(Vector3 position, int end)
        {
            return InsideNet(position, end)
                && Depth(position, end) > RinkSpec.PuckRadius + GoalLineHalfWidth;
        }

        /// <summary>Half the width of the painted goal line (50 mm line); the puck must clear all of it.</summary>
        public const float GoalLineHalfWidth = 0.025f;
    }
}
