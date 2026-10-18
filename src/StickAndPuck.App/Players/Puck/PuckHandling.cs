using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Movement;

namespace StickAndPuck.App.Players.Puck
{
    /// <summary>The numbers and aiming maths of puck handling, with no Raylib and no state, so AI
    /// and tests can ask what a pass or shot would do. Traits read here: puck control (reach, how
    /// hard a puck can be taken, how tight the puck sits on the blade), smart passes (picking and
    /// leading the receiver, pass accuracy), shot power and shot accuracy.</summary>
    public static partial class PuckHandling
    {
        /// <summary>Shoot held this long or longer makes a slap shot; a shorter tap is a wrist shot.</summary>
        public const float SlapAfter = 0.3f;

        /// <summary>A slap shot wound up this long is at full power.</summary>
        public const float FullCharge = 1.0f;

        /// <summary>Seconds from the start of the shoot clip to the puck leaving the blade (the sweep pose).</summary>
        public const float StrikeDelay = 6 / 24f;

        /// <summary>Seconds after a release before the same player can take the puck again.</summary>
        public const float Repick = 0.35f;

        /// <summary>A puck higher than this flies over the stick.</summary>
        public const float StickHeight = 0.25f;

        /// <summary>A puck this close to a skater's centre is at their skates.</summary>
        public const float SkateReach = 0.5f;

        /// <summary>Passes look for teammates within this angle of the stick, in radians.</summary>
        public const float PassCone = 70f * MathF.PI / 180f;

        /// <summary>An open teammate has no opponent this close to his blade or body, in metres.</summary>
        public const float OpenRoom = 2f;

        /// <summary>Nor an opponent this close to the pass lane, past the first metre off the passer's blade, in metres.</summary>
        public const float LaneRoom = 1.5f;

        private const float LongestPass = 40f;
        private const float NetAimCone = 80f * MathF.PI / 180f;
        private const float LongestShot = 26f;
        private const float MinFlight = 0.15f;
        private const float MaxLift = 7f;

        /// <summary>Gets how far from the blade a player can take a loose puck, in metres.</summary>
        public static float Reach(Traits t)
        {
            return 0.45f + (0.35f * Traits.Unit(t.PuckControl));
        }

        /// <summary>Gets the fastest puck, relative to the player, that the player can take cleanly.</summary>
        public static float CatchSpeed(Traits t)
        {
            return 14f + (12f * Traits.Unit(t.PuckControl));
        }

        /// <summary>Gets how quickly a carried puck settles back onto the blade, per second; higher is tighter.</summary>
        public static float Follow(Traits t)
        {
            return 25f + (35f * Traits.Unit(t.PuckControl));
        }

        /// <summary>Gets the speed of a pass over a distance: firm enough to arrive, soft enough to take.</summary>
        public static float PassSpeed(float distance)
        {
            return Math.Clamp(8f + (0.35f * distance), 8f, 17f);
        }

        /// <summary>Gets the end of the rink a club attacks: +1 for the +x net, -1 for the -x net.</summary>
        public static int AttackEnd(World world, Side side)
        {
            return world.Match.DefendsLeft == side ? 1 : -1;
        }

        /// <summary>Where a player's blade is: just ahead of the body, on the stick side.</summary>
        public static Vector3 Blade(Player p)
        {
            return Blade(p.Position, p.Heading);
        }

        /// <summary>Where a blade is for a body at a position and heading.</summary>
        public static Vector3 Blade(Vector3 position, float heading)
        {
            // Matrix4x4 rotation uses platform trigonometry and estimated SIMD multiply-adds.
            // Keep the simulation on SimMath and require the same fused rounding everywhere.
            float x = 0.55f * SkaterRenderer.Scale,
                z = 1.05f * SkaterRenderer.Scale;
            float sine = SimMath.Sin(heading),
                cosine = SimMath.Cos(heading);
            return new Vector3(
                position.X + MathF.FusedMultiplyAdd(z, sine, x * cosine),
                0,
                position.Z + MathF.FusedMultiplyAdd(z, cosine, -x * sine)
            );
        }

        /// <summary>Gets the way a player faces, on the ice (x, z).</summary>
        public static Vector2 Facing(Player p)
        {
            return new Vector2(SimMath.Sin(p.Heading), SimMath.Cos(p.Heading));
        }

        /// <summary>Gets the way a player means: the stick when pushed, else the way the body faces.</summary>
        public static Vector2 Intent(Player p)
        {
            return Intent(p, p.Command.Move);
        }

        /// <summary>Gets the way a player would mean with the stick held this way: the stick when pushed, else the way the body faces.</summary>
        public static Vector2 Intent(Player p, Vector2 stick)
        {
            return stick.Length() > 0.3f ? Vector2.Normalize(stick) : Facing(p);
        }

        /// <summary>Picks who a pass goes to: the teammate on the ice nearest the way the passer means, within
        /// <see cref="PassCone"/>, closer ones preferred. Goalies are not picked.</summary>
        public static Maybe<Player> PickReceiver(World world, Player passer)
        {
            return PickReceiver(world, passer, passer.Command.Move);
        }

        /// <summary>Picks who a pass would go to with the stick held this way, the same way as
        /// <see cref="PickReceiver(World, Player)"/> (same scoring, same ties) but without reading or
        /// touching the passer's command, so the AI can weigh a pass it has not made.</summary>
        public static Maybe<Player> PickReceiver(World world, Player passer, Vector2 stick)
        {
            Vector2 dir = Intent(passer, stick);
            Vector2 from = Flat(Blade(passer));
            Maybe<Player> best = Maybe.None<Player>();
            float bestScore = float.MaxValue;
            foreach (Player mate in world.OnIce(passer.Side))
            {
                if (mate.Id == passer.Id || mate.IsGoalie)
                {
                    continue;
                }

                Vector2 to = Flat(Blade(mate)) - from;
                float d = to.Length();
                if (d is < 2f or > LongestPass)
                {
                    continue;
                }

                float angle = MathF.Abs(Angle(dir, to));
                if (angle > PassCone)
                {
                    continue;
                }

                float score = (angle / PassCone) + (0.5f * d / LongestPass);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = mate;
                }
            }
            return best;
        }

        /// <summary>Who a human's pass goes to when nobody is in <see cref="PassCone"/>: the nearest open
        /// teammate 2 to 40 metres off, by blade. Open means no opponent (standing) has his blade or
        /// body within <see cref="OpenRoom"/> of the teammate's blade, nor within <see cref="LaneRoom"/>
        /// of the lane between the blades past its first metre (a forechecker on the passer's own stick
        /// does not close every lane). Goalies and fallen teammates are not picked; nearest wins, the
        /// first in player order on a tie. No randomness.</summary>
        public static Maybe<Player> PickOpen(World world, Player passer)
        {
            Vector2 from = Flat(Blade(passer));
            List<Player> foes =
            [
                .. world
                    .OnIce(passer.Side == Side.Home ? Side.Away : Side.Home)
                    .Where(o => o.Action != PlayerAction.Fallen),
            ];
            Maybe<Player> best = Maybe.None<Player>();
            float bestDistance = float.MaxValue;
            foreach (Player mate in world.OnIce(passer.Side))
            {
                if (mate.Id == passer.Id || mate.IsGoalie || mate.Action == PlayerAction.Fallen)
                {
                    continue;
                }

                Vector2 to = Flat(Blade(mate));
                float d = Vector2.Distance(from, to);
                if (d is < 2f or > LongestPass || d >= bestDistance || !Open(from, to, foes))
                {
                    continue;
                }

                best = mate;
                bestDistance = d;
            }
            return best;
        }

        private static bool Open(Vector2 from, Vector2 to, List<Player> foes)
        {
            Vector2 start = from + Vector2.Normalize(to - from);
            return !foes.Exists(o =>
                new[] { Flat(o.Position), Flat(Blade(o)) }.Any(at =>
                    Vector2.Distance(at, to) < OpenRoom || SegmentDistance(start, to, at) < LaneRoom
                )
            );
        }

        private static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len = ab.LengthSquared();
            float t = len > 1e-8f ? Math.Clamp(Vector2.Dot(p - a, ab) / len, 0f, 1f) : 0f;
            return Vector2.Distance(p, a + (ab * t));
        }

        /// <summary>Where to send a pass so it meets a moving receiver's blade. A smarter passer leads the
        /// receiver closer to where they will be; a poor one aims nearer where they are now.</summary>
        public static (Vector3 Target, float Speed) Lead(
            Vector3 from,
            Player receiver,
            int hockeyIq
        )
        {
            float lead = 0.4f + (0.6f * Traits.Unit(hockeyIq));
            Vector3 blade = Blade(receiver);
            Vector3 target = blade;
            float speed = PassSpeed(Vector3.Distance(from, blade));
            for (int i = 0; i < 3; i++)
            {
                float d = Vector3.Distance(from, target);
                speed = PassSpeed(d);
                target = blade + (receiver.Velocity * SlideTime(d, speed) * lead);
            }
            return (target with { Y = 0 }, speed);
        }

        /// <summary>Gets how long a puck sliding on the ice takes to cover a distance, slowing as the puck physics slows it.</summary>
        public static float SlideTime(float distance, float speed)
        {
            const float step = 1 / 60f;
            float t = 0;
            while (distance > 0 && speed > 0.1f && t < 5f)
            {
                distance -= speed * step;
                speed -=
                    (Physics.PuckTuning.IceFriction + (Physics.PuckTuning.Drag * speed)) * step;
                t += step;
            }
            return t;
        }

        /// <summary>The velocity of a pass toward a point, with the passer's error: a smart passer is accurate,
        /// a poor one sprays the angle and the weight.</summary>
        public static Vector3 PassVelocity(
            Vector3 from,
            Vector3 target,
            float speed,
            int hockeyIq,
            Random rng
        )
        {
            float miss = 1f - Traits.Unit(hockeyIq);
            Vector2 dir = Flat(target - from);
            dir = dir.LengthSquared() > 1e-6f ? Vector2.Normalize(dir) : Vector2.UnitX;
            dir = Rotate(dir, Gauss(rng) * Deg(0.3f + (2f * miss)));
            speed *= 1f + (Gauss(rng) * 0.08f * miss);
            return new Vector3(dir.X, 0, dir.Y) * speed;
        }

        /// <summary>Gets a value indicating whether a point is inside a net, past the goal line.</summary>
        public static bool InNet(Vector3 p)
        {
            float ax = MathF.Abs(p.X);
            return ax > RinkSpec.GoalLineX
                && ax < RinkSpec.GoalLineX + RinkSpec.NetDepth + 0.1f
                && MathF.Abs(p.Z) < (RinkSpec.NetWidth / 2) + 0.1f;
        }

        /// <summary>Keeps a carried puck inside the boards and their rounded corners.</summary>
        public static Vector3 InsideBoards(Vector3 p)
        {
            const float margin = 0.08f;
            float hx = RinkSpec.HalfLength - margin,
                hz = RinkSpec.HalfWidth - margin,
                r = RinkSpec.CornerRadius - margin;
            float x = Math.Clamp(p.X, -hx, hx),
                z = Math.Clamp(p.Z, -hz, hz);
            float cx = hx - r,
                cz = hz - r;
            if (MathF.Abs(x) > cx && MathF.Abs(z) > cz)
            {
                Vector2 corner = new(MathF.Sign(x) * cx, MathF.Sign(z) * cz);
                Vector2 off = new Vector2(x, z) - corner;
                if (off.Length() > r)
                {
                    off = Vector2.Normalize(off) * r;
                    x = corner.X + off.X;
                    z = corner.Y + off.Y;
                }
            }
            return new Vector3(x, p.Y, z);
        }

        /// <summary>A normally distributed number with mean 0 and spread 1.</summary>
        public static float Gauss(Random rng)
        {
            float u = 1f - rng.NextSingle(),
                v = rng.NextSingle();
            return MathF.Sqrt(-2f * SimMath.Log(u)) * SimMath.Cos(MathF.Tau * v);
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }

        private static float Deg(float degrees)
        {
            return degrees * MathF.PI / 180f;
        }

        private static float Angle(Vector2 from, Vector2 to)
        {
            return SimMath.Atan2((from.X * to.Y) - (from.Y * to.X), Vector2.Dot(from, to));
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = SimMath.Cos(radians),
                s = SimMath.Sin(radians);
            return new Vector2((v.X * c) - (v.Y * s), (v.X * s) + (v.Y * c));
        }
    }
}
