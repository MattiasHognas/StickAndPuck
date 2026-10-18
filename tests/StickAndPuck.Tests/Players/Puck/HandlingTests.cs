using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Players.Puck
{
    /// <summary>
    /// The puck handling checks with skating, handling and puck physics. Covers that
    /// one player carries, passes to a teammate, who shoots and scores; and that puck control, smart
    /// passes, shot power and shot accuracy each make a difference. Formerly SAP_SCENE=handlingtest.
    /// </summary>
    /// <remarks>
    /// Each run takes the next handling seed. The scene counted them from 1 through all its groups in
    /// order, so each test starts its count where the scene had it when that group began.
    /// </remarks>
    public sealed class HandlingTests : CheckedTest
    {
        private const float Right = MathF.PI / 2;

        private static World Make()
        {
            return new(WorldBuilder.DefaultSetup, new Settings());
        }

        /// <summary>The handling seeds a test's runs take in turn.</summary>
        /// <param name="first">The seed of the first run.</param>
        private sealed class SeedCount(int first)
        {
            private int next = first;

            public int Take()
            {
                return next++;
            }
        }

        /// <summary>Runs skating, handling and physics for some seconds, with no commands set.</summary>
        private static void Run(SeedCount seeds, World world, float seconds)
        {
            _ = Run(seeds, world, seconds, (_, _) => { });
        }

        /// <summary>Runs skating, handling and physics for some seconds; <paramref name="each"/> sets commands before each step.</summary>
        private static List<MatchEvent> Run(
            SeedCount seeds,
            World world,
            float seconds,
            Action<World, int> each
        )
        {
            ISystem[] systems =
            [
                new SkatingSystem(),
                new PuckHandlingSystem(Seeds.Dice(seeds.Take())),
                new PuckPhysics(),
            ];
            foreach (ISystem s in systems.Where(s => s is not PuckPhysics))
            {
                s.Start(world);
            }

            List<MatchEvent> events = [];
            int steps = (int)MathF.Round(seconds / Game.Step, MidpointRounding.ToEven);
            for (int i = 0; i < steps; i++)
            {
                world.Events.Clear();
                each(world, i);
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static Player Add(
            World w,
            Side side,
            Vector3 at,
            float heading = Right,
            Maybe<Traits> traits = default
        )
        {
            Player p = w.AddPlayer(
                side,
                new PlayerData { Position = PlayingPosition.C, Traits = traits.Or(new Traits()) },
                at
            );
            p.Heading = heading;
            return p;
        }

        /// <summary>Adds a skater placed so that their blade is at a point.</summary>
        private static Player AddBladeAt(
            World w,
            Side side,
            Vector3 blade,
            float heading = Right,
            Maybe<Traits> traits = default
        )
        {
            return Add(w, side, blade - PuckHandling.Blade(Vector3.Zero, heading), heading, traits);
        }

        private static void Give(World w, Player p)
        {
            w.Puck.Carrier = p.Id;
            w.Puck.Position = PuckHandling.Blade(p);
            w.Puck.Velocity = Vector3.Zero;
        }

        private static void Press(
            Player p,
            int step,
            int at,
            bool pass = false,
            int shootFrom = -1,
            int shootTo = -1
        )
        {
            PlayerCommand c = p.Command;
            if (pass)
            {
                c.Pass = step == at;
            }

            if (shootFrom >= 0)
            {
                c.Shoot = step >= shootFrom && step < shootTo;
            }

            p.Command = c;
        }

        [Test]
        public void CarryScoreSequence()
        {
            SeedCount seeds = new(1);
            World w = Make();
            // the done-when: a carries up ice, passes to b (control follows), b shoots and scores
            Player a = Add(w, Side.Home, new Vector3(-15, 0, 2));
            Player b = Add(
                w,
                Side.Home,
                new Vector3(10, 0, -4),
                traits: new Traits { ShotAccuracy = 90 }
            );
            a.Slot = 0;
            Give(w, a);
            a.Command = new PlayerCommand { Move = Vector2.UnitX };
            int got = -1;
            List<MatchEvent> events = Run(
                seeds,
                w,
                6f,
                (x, i) =>
                {
                    if (i == 90)
                    {
                        a.Command = new PlayerCommand
                        {
                            Move = Vector2.Normalize(new Vector2(1, -0.4f)),
                            Pass = true,
                        };
                    }

                    if (got < 0 && x.Puck.Carrier == b.Id)
                    {
                        got = i;
                    }

                    if (got >= 0 && b.Slot == 0)
                    {
                        b.Command = new PlayerCommand
                        {
                            Move = Vector2.UnitX,
                            Shoot = i >= got + 50 && i < got + 56,
                        };
                    }
                }
            );
            Check(
                events.Exists(e => e is PassMade pass && pass.PasserId == a.Id),
                "the carrier skates up ice and passes"
            );
            Check(b.Slot == 0 && a.Slot == -1, "control follows the pass to the receiver");
            Check(
                events.Exists(e => e is ShotTaken shot && shot.ShooterId == b.Id),
                "the receiver takes the pass and shoots"
            );
            Maybe<GoalScored> goal = events.OfType<GoalScored>().FirstOrNone();
            Check(
                goal.Is(g => g.ScorerId == b.Id && g.Side == Side.Home),
                "and scores, credited to the shooter"
            );
        }

        [Test]
        public void CarryKeepsPuck()
        {
            SeedCount seeds = new(2);
            World w = Make();
            Player p = Add(w, Side.Home, new Vector3(0, 0, 0));
            Give(w, p);
            bool outside = false;
            float worst = 0;
            _ = Run(
                seeds,
                w,
                8f,
                (x, i) =>
                {
                    // sprint in circles, then straight into the side boards
                    float a = i * 0.05f;
                    p.Command = new PlayerCommand
                    {
                        Move =
                            i < 300 ? new Vector2(SimMath.Cos(a), SimMath.Sin(a)) : Vector2.UnitY,
                        Sprint = true,
                    };
                    Vector3 pk = x.Puck.Position;
                    outside |=
                        MathF.Abs(pk.Z) > RinkSpec.HalfWidth
                        || MathF.Abs(pk.X) > RinkSpec.HalfLength;
                    if (i is > 10 and < 300)
                    {
                        worst = Math.Max(worst, Vector3.Distance(pk, PuckHandling.Blade(p)));
                    }
                }
            );
            Check(
                w.Puck.Carrier == p.Id,
                "a skater sprinting in circles and into the boards keeps the puck"
            );
            Check(
                worst < 0.5f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the carried puck stays on the blade (worst {worst:0.00} m)"
                )
            );
            Check(!outside, "the carried puck never leaves the rink");
        }

        [Test]
        public void PickUpLoose()
        {
            SeedCount seeds = new(3);
            World w = Make();
            Player p = AddBladeAt(w, Side.Home, new Vector3(0, 0, 0));
            w.Puck.Position = new Vector3(6, 0, 0);
            p.Command = new PlayerCommand { Move = Vector2.UnitX };
            Run(seeds, w, 2f);
            Check(w.Puck.Carrier == p.Id, "a skater skating onto a loose puck takes it");

            // the same hard puck: a soft-handed player cannot take it, a good one can
            foreach (
                (int control, float speed, bool takes) in new[]
                {
                    (10, 24f, false),
                    (90, 24f, true),
                    (10, 12f, true),
                }
            )
            {
                World t = Make();
                Player q = Add(
                    t,
                    Side.Home,
                    new Vector3(0, 0, 0),
                    traits: new Traits { PuckControl = control }
                );
                Vector3 blade = PuckHandling.Blade(q);
                t.Puck.Position = blade - new Vector3(speed * 0.3f, 0, 0);
                t.Puck.Velocity = new Vector3(speed, 0, 0);
                Run(seeds, t, 0.5f);
                Check(
                    t.Puck.Carrier == q.Id == takes,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"puck control {control} {(takes ? "takes" : "cannot take")} a {speed:0} m/s puck"
                    )
                );
            }
        }

        [Test]
        public void ControlFollowsPass()
        {
            SeedCount seeds = new(7);
            foreach (
                ControlSwitching switching in new[]
                {
                    ControlSwitching.Auto,
                    ControlSwitching.Manual,
                }
            )
            {
                World w = Make();
                w.Settings = w.Settings with { Switching = switching };
                Player a = Add(w, Side.Home, new Vector3(-10, 0, 0));
                Player b = Add(w, Side.Home, new Vector3(5, 0, 1), -Right);
                _ = Add(w, Side.Home, new Vector3(-10, 0, 9)); // off to the side: not the one the stick points at
                a.Slot = 0;
                Give(w, a);
                List<MatchEvent> events = Run(seeds, w, 3f, (_, i) => Press(a, i, 5, pass: true));
                bool follows = switching != ControlSwitching.Manual;
                Check(
                    w.Puck.Carrier == b.Id,
                    $"a pass reaches the teammate the stick points at ({switching})"
                );
                Check(
                    follows ? b.Slot == 0 && a.Slot < 0 : a.Slot == 0 && b.Slot < 0,
                    $"with switching {switching} control {(follows ? "follows the pass" : "stays with the passer")}"
                );
                Check(events.Where(e => e is PassMade).Take(2).Count() == 1, "one pass event");
            }
        }

        [Test]
        public void Interception()
        {
            SeedCount seeds = new(9);
            World w = Make();
            Player a = Add(w, Side.Home, new Vector3(-10, 0, 0));
            _ = Add(w, Side.Home, new Vector3(8, 0, 1), -Right);
            Vector3 mid = (PuckHandling.Blade(a) + PuckHandling.Blade(w.Players[1])) / 2;
            Player thief = AddBladeAt(w, Side.Away, mid + new Vector3(0, 0, 0.3f), -Right);
            Give(w, a);
            _ = Run(seeds, w, 3f, (_, i) => Press(a, i, 5, pass: true));
            Check(w.Puck.Carrier == thief.Id, "an opponent in the passing lane intercepts");
        }

        [Test]
        public void HockeyIqLead()
        {
            SeedCount seeds = new(10);
            // a receiver skating across the ice: a smart passer leads them, a poor one passes behind
            int Completed(int smart)
            {
                int done = 0;
                for (int n = 0; n < 20; n++)
                {
                    World w = Make();
                    Player a = Add(
                        w,
                        Side.Home,
                        new Vector3(-10, 0, -4 + (n * 0.3f)),
                        traits: new Traits { HockeyIq = smart }
                    );
                    Player b = Add(w, Side.Home, new Vector3(8, 0, -8), 0);
                    b.Velocity = new Vector3(0, 0, SkatingModel.TopSpeed(b.Data.Traits) * 1.15f); // already flat out
                    b.Command = new PlayerCommand { Move = Vector2.UnitY, Sprint = true };
                    Give(w, a);
                    _ = Run(seeds, w, 3f, (_, i) => Press(a, i, 0, pass: true));
                    if (w.Puck.Carrier == b.Id)
                    {
                        done++;
                    }
                }
                return done;
            }
            int smart = Completed(95),
                poor = Completed(5);
            Check(
                smart >= 16 && smart > poor,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"smart passes lead a moving receiver: {smart}/20 completed vs {poor}/20 for a poor passer"
                )
            );
        }

        [Test]
        public void ShotKinds()
        {
            SeedCount seeds = new(50);
            float Shot(int power, int holdSteps)
            {
                World w = Make();
                Player p = Add(
                    w,
                    Side.Home,
                    new Vector3(10, 0, 0),
                    traits: new Traits { ShotPower = power }
                );
                Give(w, p);
                List<MatchEvent> ev = Run(
                    seeds,
                    w,
                    2f,
                    (_, i) => Press(p, i, 0, shootFrom: 0, shootTo: holdSteps)
                );
                return ev.OfType<ShotTaken>().FirstOrDefault()?.Speed ?? 0;
            }
            float wrist = Shot(50, 6),
                slap = Shot(50, 60),
                weak = Shot(10, 6),
                strong = Shot(90, 6);
            Check(
                wrist > 15 && slap > wrist * 1.2f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a held shot is a harder slap shot ({slap:0} vs wrist {wrist:0} m/s)"
                )
            );
            Check(
                strong > weak + 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"shot power makes harder shots ({strong:0} vs {weak:0} m/s)"
                )
            );
        }

        [Test]
        public void AccuracyMatters()
        {
            SeedCount seeds = new(54);
            int Goals(int accuracy)
            {
                int goals = 0;
                for (int n = 0; n < 30; n++)
                {
                    World w = Make();
                    Player p = Add(
                        w,
                        Side.Home,
                        new Vector3(12, 0, -3 + (n * 0.2f)),
                        traits: new Traits { ShotAccuracy = accuracy }
                    );
                    Give(w, p);
                    p.Command = new PlayerCommand { Move = new Vector2(1, n % 2 == 0 ? 1 : -1) }; // pick a corner
                    List<MatchEvent> ev = Run(
                        seeds,
                        w,
                        2f,
                        (_, i) => Press(p, i, 0, shootFrom: 0, shootTo: 6)
                    );
                    if (ev.Exists(e => e is GoalScored))
                    {
                        goals++;
                    }
                }
                return goals;
            }
            int sharp = Goals(95),
                wild = Goals(5);
            Check(
                sharp >= 24 && sharp > wild + 4,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"shot accuracy puts shots on target into an empty net: {sharp}/30 vs {wild}/30"
                )
            );
        }

        [Test]
        public void NoPickupInNet()
        {
            SeedCount seeds = new(114);
            World w = Make();
            Player p = AddBladeAt(w, Side.Home, new Vector3(RinkSpec.GoalLineX - 0.1f, 0, 0));
            w.Puck.Position = new Vector3(RinkSpec.GoalLineX + 0.4f, 0, 0);
            Run(seeds, w, 1f);
            Check(w.Puck.Carrier < 0 && p.Id >= 0, "a puck inside the net is not taken");
        }
    }
}
