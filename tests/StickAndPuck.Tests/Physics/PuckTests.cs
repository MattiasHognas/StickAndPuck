using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Physics
{
    /// <summary>
    /// The puck physics: corner shots rebound plausibly, and a goal counts only when the whole puck
    /// is over the line. Formerly SAP_SCENE=pucktest.
    /// </summary>
    public sealed class PuckTests : CheckedTest
    {
        /// <summary>An empty rink: the default setup and settings, nobody on the ice, the match flow not begun.</summary>
        // one skater per club on the bench (home 0, away 1), clear of the puck, so a goal always has someone to credit
        private static World Make()
        {
            World w = new WorldBuilder().NoStarters().NotBegun().Build();
            PlayerData skater = WorldBuilder
                .Clubs[0]
                .Roster.First(p => p.Position != PlayingPosition.G);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                w.AddPlayer(side, skater, Vector3.Zero).OnIce = false;
            }

            return w;
        }

        /// <summary>Runs the physics system alone for some seconds and returns every event it published.</summary>
        private static List<MatchEvent> Run(World world, float seconds)
        {
            return Run(world, seconds, new PuckPhysics(), _ => { });
        }

        /// <summary>Runs the physics system alone for some seconds, calling back after each step; returns every event it published.</summary>
        private static List<MatchEvent> Run(World world, float seconds, Action<World> each)
        {
            return Run(world, seconds, new PuckPhysics(), each);
        }

        /// <summary>Runs this physics system alone for some seconds, calling back after each step; returns every event it published.</summary>
        private static List<MatchEvent> Run(
            World world,
            float seconds,
            PuckPhysics physics,
            Action<World> each
        )
        {
            List<MatchEvent> events = [];
            int steps = (int)MathF.Round(seconds / Game.Step, MidpointRounding.ToEven);
            for (int i = 0; i < steps; i++)
            {
                world.Events.Clear();
                physics.Update(world, Game.Step);
                events.AddRange(world.Events.Current);
                each(world);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static void Shoot(World world, Vector3 from, Vector3 velocity)
        {
            world.Puck.Position = from;
            world.Puck.Velocity = velocity;
            world.Puck.Carrier = -1;
            world.Puck.Live = true;
        }

        private static bool InsideRink(Vector3 p)
        {
            float cr = RinkSpec.CornerRadius,
                cx = RinkSpec.HalfLength - cr,
                cz = RinkSpec.HalfWidth - cr;
            float ax = MathF.Abs(p.X),
                az = MathF.Abs(p.Z);
            return ax <= RinkSpec.HalfLength + 1e-3f
                && az <= RinkSpec.HalfWidth + 1e-3f
                && (ax <= cx || az <= cz || new Vector2(ax - cx, az - cz).Length() <= cr + 1e-3f);
        }

        [Test]
        public void CornerRebound()
        {
            World w = Make();
            // a hard pass straight into the rounded corner comes back out, slower, still on the ice
            Vector3 dir = Vector3.Normalize(new Vector3(1, 0, 1));
            Shoot(w, new Vector3(18, 0, 0), dir * 20);
            List<MatchEvent> ev = Run(w, 1.2f);
            Vector3 v = w.Puck.Velocity;
            Check(ev.Exists(e => e is PuckHitBoards), "corner shot hits the boards");
            Check(
                Vector3.Dot(v, dir) < 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"corner shot comes back out of the corner (v = {v.X:0.0}, {v.Z:0.0})"
                )
            );
            Check(
                v.Length() is > 4 and < 16,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"corner rebound keeps some speed ({v.Length():0.0} of 20 m/s)"
                )
            );
            Check(InsideRink(w.Puck.Position), "corner shot stays inside the rink");
        }

        [Test]
        public void RimAroundCorner()
        {
            World w = Make();
            // a shot along the side boards rims round the corner and comes out along the end boards
            Shoot(w, new Vector3(10, 0, RinkSpec.HalfWidth - 0.3f), new Vector3(25, 0, 0.5f));
            _ = Run(w, 1.6f);
            Vector3 p = w.Puck.Position;
            Vector3 v = w.Puck.Velocity;
            Check(
                p.X > RinkSpec.HalfLength - RinkSpec.CornerRadius
                    && p.Z < RinkSpec.HalfWidth - RinkSpec.CornerRadius + 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"rim follows the corner round (at {p.X:0.0}, {p.Z:0.0})"
                )
            );
            Check(
                v.Z < -3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"rim leaves the corner along the end boards (vz = {v.Z:0.0})"
                )
            );
        }

        [Test]
        public void GoalCountsOnce()
        {
            World w = Make();
            w.Puck.LastTouch = 1;
            Shoot(w, new Vector3(18, 0, 0.2f), new Vector3(30, 1.5f, 0));
            List<MatchEvent> ev = Run(w, 2f);
            List<GoalScored> goals = [.. ev.OfType<GoalScored>()];
            Check(goals.Count == 1, $"a shot into the net is one goal ({goals.Count})");
            Check(
                goals.Count == 1 && goals[0].Side == w.Match.DefendsLeft && goals[0].ScorerId == 1,
                "the goal goes to the club attacking that net, credited to the last touch"
            );
            Check(
                PuckMotion.FullyOverGoalLine(w.Puck.Position, 1) && w.Puck.Velocity.Length() < 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the mesh keeps the puck in the net (speed {w.Puck.Velocity.Length():0.0})"
                )
            );
        }

        [Test]
        public void GoalLineEdge()
        {
            // a puck sliding to a stop on the line: only a puck wholly past the line's far edge counts
            float r = RinkSpec.PuckRadius,
                half = PuckMotion.GoalLineHalfWidth;
            foreach (
                (float u, bool goal) in new[]
                {
                    (0f, false),
                    (r, false),
                    (r + half - 0.003f, false),
                    (r + half + 0.003f, true),
                }
            )
            {
                World w = Make();
                Shoot(w, new Vector3(RinkSpec.GoalLineX + u, 0, 0.3f), Vector3.Zero);
                int n = Run(w, 0.1f).Count(e => e is GoalScored);
                Check(
                    n == (goal ? 1 : 0),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"puck at {u * 100:0.0} cm past the line centre: {(goal ? "goal" : "no goal")}"
                    )
                );
            }
            World dead = Make();
            Shoot(dead, new Vector3(RinkSpec.GoalLineX + 0.3f, 0, 0), Vector3.Zero);
            dead.Puck.Live = false;
            Check(
                Run(dead, 0.1f).TrueForAll(e => e is not GoalScored),
                "no goal while the puck is dead"
            );
        }

        [Test]
        public void GoalWithNoPlayers()
        {
            // nobody to credit: the puck is held as scored but no event names a player that does not exist
            World w = Make();
            World empty = new(w.Setup, w.Settings);
            Shoot(empty, new Vector3(RinkSpec.GoalLineX + 0.3f, 0, 0.3f), Vector3.Zero);
            List<MatchEvent> ev = Run(empty, 0.2f);
            Check(
                ev.TrueForAll(e => e is not GoalScored)
                    && PuckMotion.FullyOverGoalLine(empty.Puck.Position, 1),
                "a goal in a world with no players publishes no event and does not throw"
            );
        }

        [Test]
        public void PostRingsOut()
        {
            World w = Make();
            // aimed at the far post's outer half: it rings off and stays out
            Shoot(w, new Vector3(18, 0, (RinkSpec.NetWidth / 2) + 0.03f), new Vector3(30, 0, 0));
            List<MatchEvent> ev = Run(w, 1f);
            Check(ev.Exists(e => e is PuckHitPost), "a shot at the post rings off it");
            Check(ev.TrueForAll(e => e is not GoalScored), "the post shot is no goal");
            Check(
                w.Puck.Position.X < RinkSpec.GoalLineX
                    || MathF.Abs(w.Puck.Position.Z) > RinkSpec.NetWidth / 2,
                "the post shot ends up outside the net"
            );

            World bar = new(w.Setup, w.Settings);
            Shoot(bar, new Vector3(24, RinkSpec.NetHeight + 0.04f, 0), new Vector3(28, 0, 0));
            List<MatchEvent> bev = Run(bar, 0.6f);
            Check(bev.Exists(e => e is PuckHitPost), "a shot at the crossbar hits it");
        }

        [Test]
        public void BehindTheNet()
        {
            World w = Make();
            // from behind the net into its back: bounces off, never a goal
            Shoot(w, new Vector3(RinkSpec.HalfLength - 0.5f, 0, 0.1f), new Vector3(-15, 0, 0));
            List<MatchEvent> ev = Run(w, 1.5f);
            Check(
                ev.TrueForAll(e => e is not GoalScored),
                "a puck into the back of the net is no goal"
            );
            Check(
                w.Puck.Position.X > RinkSpec.GoalLineX + RinkSpec.NetDepth,
                "it stays behind the net"
            );

            World side = new(w.Setup, w.Settings);
            Shoot(side, new Vector3(RinkSpec.GoalLineX + 0.5f, 0, 4), new Vector3(0, 0, -15));
            List<MatchEvent> sev = Run(side, 1.5f);
            Check(
                sev.TrueForAll(e => e is not GoalScored) && side.Puck.Position.Z > 0,
                "a puck into the side of the net bounces off it"
            );
        }

        [Test]
        public void GlassAboveBoards()
        {
            World w = Make();
            // a lifted puck high off the side glass drops back onto the ice
            Shoot(w, new Vector3(0, 0, 4), new Vector3(0, 6, 22));
            bool glass = false;
            List<MatchEvent> ev = Run(
                w,
                3f,
                each: x =>
                    glass |=
                        x.Puck.Position.Z > RinkSpec.HalfWidth - 0.1f
                        && x.Puck.Position.Y > RinkSpec.BoardHeight
            );
            Check(
                glass && ev.Exists(e => e is PuckHitBoards),
                "a high shot hits the glass above the boards"
            );
            Check(
                w.Puck.Position.Y == 0 && InsideRink(w.Puck.Position),
                "and falls back onto the ice"
            );
        }

        [Test]
        public void BodyBlock()
        {
            World w = Make();
            PlayerData skater =
                WorldBuilder.Clubs[0].Roster.FirstOrDefault(p => p.Position != PlayingPosition.G)
                ?? new PlayerData { Position = PlayingPosition.D };
            Player block = w.AddPlayer(Side.Away, skater, new Vector3(5, 0, 0));
            Shoot(w, new Vector3(0, 0, 0), new Vector3(20, 0, 0));
            _ = Run(w, 0.5f);
            Check(
                w.Puck.Velocity.X < 0 && w.Puck.Velocity.Length() < 8,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a shot into a body drops off it ({w.Puck.Velocity.X:0.0} m/s)"
                )
            );
            Check(w.Puck.LastTouch == block.Id, "the blocker becomes the last touch");

            World over = Make();
            _ = over.AddPlayer(Side.Away, skater, new Vector3(5, 0, 0));
            Shoot(over, new Vector3(0, 1.2f, 0), new Vector3(20, 9, 0));
            _ = Run(over, 0.5f);
            Check(over.Puck.Position.X > 6, "a puck lifted over a body flies past it");
        }

        [Test]
        public void FrictionStops()
        {
            World w = Make();
            Shoot(w, new Vector3(-20, 0, -5), new Vector3(9, 0, 0));
            _ = Run(w, 6f);
            float v1 = w.Puck.Velocity.Length();
            _ = Run(w, 14f);
            Check(
                v1 is > 1 and < 7,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a 9 m/s slide still glides after 6 s ({v1:0.0} m/s)"
                )
            );
            Check(w.Puck.Velocity.Length() < 0.01f, "and comes to rest");
        }

        [Test]
        public void Fuzz()
        {
            // many random shots: the puck never leaves the rink, never gets into a net except through the mouth
#pragma warning disable S2245 // a fixed seed so every run fuzzes the same shots
            Random rng = new(7);
#pragma warning restore S2245
            int escapes = 0,
                sneaky = 0,
                goals = 0;
            for (int i = 0; i < 600; i++)
            {
                World w = Make();
                Vector3 from = new(
                    (float)((rng.NextDouble() * 2) - 1) * 28,
                    0,
                    (float)((rng.NextDouble() * 2) - 1) * 11.5f
                );
                if (
                    MathF.Abs(MathF.Abs(from.X) - RinkSpec.GoalLineX - 0.6f) < 1.2f
                    && MathF.Abs(from.Z) < 1.5f
                )
                {
                    continue;
                }

                if (!InsideRink(from))
                {
                    continue;
                }

                float a = (float)(rng.NextDouble() * MathF.Tau),
                    s = 5 + ((float)rng.NextDouble() * 40);
                Shoot(
                    w,
                    from,
                    new Vector3(SimMath.Cos(a) * s, (float)rng.NextDouble() * 6, SimMath.Sin(a) * s)
                );
                bool mouth = false;
                Vector3 last = from;
                List<MatchEvent> ev = Run(
                    w,
                    4f,
                    each: x =>
                    {
                        Vector3 p = x.Puck.Position;
                        for (int end = -1; end <= 1; end += 2)
                        {
                            if (
                                end * last.X <= RinkSpec.GoalLineX
                                && end * p.X > RinkSpec.GoalLineX
                                && MathF.Abs(p.Z) < RinkSpec.NetWidth / 2
                                && p.Y < RinkSpec.NetHeight
                            )
                            {
                                mouth = true;
                            }
                        }

                        if (!InsideRink(p) || p.Y < 0)
                        {
                            escapes++;
                        }

                        last = p;
                    }
                );
                int g = ev.Count(e => e is GoalScored);
                goals += g;
                if (g > 0 && !mouth)
                {
                    sneaky++;
                }
            }
            Check(
                escapes == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"600 random shots never leave the rink ({escapes} steps outside)"
                )
            );
            Check(
                sneaky == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"every goal came in through the mouth ({goals} goals, {sneaky} not)"
                )
            );
        }
    }
}
