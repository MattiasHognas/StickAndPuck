using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Players.Switching;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Players.Checks
{
    /// <summary>
    /// Checking and switching with switching, skating, checking, puck handling and puck physics:
    /// checks knock players off the puck, the checking trait decides how often, and control switching
    /// follows each Settings mode. Formerly SAP_SCENE=checkstest.
    /// </summary>
    public sealed class ChecksTests : CheckedTest
    {
        private const float Right = MathF.PI / 2;

        /// <summary>
        /// The seed of the next run's random streams. The old scene counted it up from 1 across all its
        /// groups, so each test starts from the seed its group had there.
        /// </summary>
        private int seed = 1;

        /// <summary>Starts the seeds where this test's group started in the old scene.</summary>
        private void SeedFrom(int first)
        {
            seed = first;
        }

        /// <summary>An empty rink with default settings and this switching mode.</summary>
        private static World Make(ControlSwitching mode = ControlSwitching.Auto)
        {
            return new WorldBuilder()
                .Settings(s => s with { Switching = mode })
                .NoStarters()
                .NotBegun()
                .Build();
        }

        /// <summary>Runs the systems for some seconds with no commands set.</summary>
        private void Run(World world, float seconds)
        {
            _ = Run(world, seconds, (_, _) => { });
        }

        /// <summary>Runs the systems for some seconds; <paramref name="each"/> sets commands before each step.</summary>
        private List<MatchEvent> Run(World world, float seconds, Action<World, int> each)
        {
#pragma warning disable S2245 // a fixed seed per run so the checks repeat
            ISystem[] systems =
            [
                new SwitchingSystem(),
                new SkatingSystem(),
                new CheckingSystem(new Random(seed)),
                new PuckHandlingSystem(new Random(seed)),
                new PuckPhysics(),
            ];
#pragma warning restore S2245
            seed++;
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

        private static void Give(World w, Player p)
        {
            w.Puck.Carrier = p.Id;
            w.Puck.Position = PuckHandling.Blade(p);
            w.Puck.Velocity = Vector3.Zero;
        }

        /// <summary>A checker chases a carrier skating up ice and checks once in reach.</summary>
        private (List<MatchEvent> Events, Player Checker, Player Carrier) Chase(
            World w,
            int checking,
            int resisting,
            float checkerSpeed,
            float carrierSpeed
        )
        {
            Player carrier = Add(
                w,
                Side.Home,
                new Vector3(0, 0, 0),
                traits: new Traits { Checking = resisting }
            );
            Player checker = Add(
                w,
                Side.Away,
                new Vector3(-2.6f, 0, 0),
                traits: new Traits { Checking = checking }
            );
            carrier.Velocity = new Vector3(carrierSpeed, 0, 0);
            checker.Velocity = new Vector3(checkerSpeed, 0, 0);
            Give(w, carrier);
            bool pressed = false;
            List<MatchEvent> events = Run(
                w,
                2.5f,
                (x, i) =>
                {
                    carrier.Command = new PlayerCommand
                    {
                        Move = new Vector2(carrierSpeed > 0 ? 0.3f : 0, 0),
                    };
                    bool press =
                        !pressed && Vector3.Distance(checker.Position, carrier.Position) < 1.4f;
                    pressed |= press;
                    checker.Command = new PlayerCommand
                    {
                        Move = Vector2.UnitX,
                        Sprint = true,
                        Check = press,
                    };
                }
            );
            return (events, checker, carrier);
        }

        [Test]
        public void StrongCheckWinsPuck()
        {
            SeedFrom(1);
            int down = 0,
                won = 0,
                hits = 0;
            bool slid = true;
            for (int t = 0; t < 10; t++)
            {
                World w = Make(ControlSwitching.Auto);
                (List<MatchEvent> events, Player checker, Player carrier) = Chase(
                    w,
                    90,
                    20,
                    8f,
                    2f
                );
                hits +=
                    events
                        .Where(e => e is CheckLanded check && check.CheckerId == checker.Id)
                        .Take(2)
                        .Count() == 1
                        ? 1
                        : 0;
                if (
                    (w.Puck.Carrier != carrier.Id && w.Puck.LastTouch != carrier.Id)
                    || w.Puck.Carrier == checker.Id
                )
                {
                    won++;
                }

                if (events.Exists(e => e is CheckLanded { Strength: >= CheckingModel.HardHit }))
                {
                    down++;
                }

                slid &= carrier.Position.X > 1.5f;
            }
            Check(
                hits == 10,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a check from behind lands exactly once each time ({hits}/10)"
                )
            );
            Check(
                down >= 9,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a strong checker at speed hits hard ({down}/10 hard hits)"
                )
            );
            Check(
                won >= 8,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"and knocks a weak carrier off the puck ({won}/10)"
                )
            );
            Check(slid, "the hit carrier is shoved up ice");
        }

        [Test]
        public void WeakCheckBounces()
        {
            SeedFrom(11);
            int kept = 0;
            bool soft = true;
            for (int t = 0; t < 10; t++)
            {
                World w = Make(ControlSwitching.Auto);
                (List<MatchEvent> events, Player _, Player carrier) = Chase(w, 15, 90, 3f, 2f);
                if (w.Puck.Carrier == carrier.Id)
                {
                    kept++;
                }

                soft &= events.OfType<CheckLanded>().All(e => e.Strength < 0.75f);
            }
            Check(soft, "a weak, slow check on a strong carrier is a soft hit");
            Check(
                kept >= 6,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"which mostly bounces off: the carrier keeps the puck ({kept}/10)"
                )
            );
        }

        [Test]
        public void TraitMatters()
        {
            World w = Make();
            Player target = Add(w, Side.Home, Vector3.Zero);
            Player weak = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 20 });
            Player avg = Add(w, Side.Away, Vector3.Zero);
            Player strong = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 80 });
            Player tough = Add(w, Side.Home, Vector3.Zero, traits: new Traits { Checking = 80 });
            float s20 = CheckingModel.Strength(weak, target, 5, carrying: false),
                s50 = CheckingModel.Strength(avg, target, 5, carrying: false),
                s80 = CheckingModel.Strength(strong, target, 5, carrying: false);
            Check(
                s20 < s50 && s50 < s80,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a higher checking trait hits harder ({s20:0.00} < {s50:0.00} < {s80:0.00})"
                )
            );
            Check(
                MathF.Abs(CheckingModel.Strength(avg, target, 4, carrying: false) - 1f) < 0.01f,
                "an average check closing at 4 m/s has strength 1"
            );
            Check(
                CheckingModel.Strength(avg, tough, 5, carrying: false) < s50,
                "a higher checking trait takes hits better"
            );
            Check(
                CheckingModel.Strength(avg, target, 7, carrying: false) > s50,
                "more speed hits harder"
            );
            avg.Energy = 0;
            Check(
                CheckingModel.Strength(avg, target, 5, carrying: false) < s50,
                "a tired checker hits softer"
            );
        }

        [Test]
        public void FallenSlides()
        {
            SeedFrom(21);
            World w = Make();
            Player p = Add(w, Side.Home, Vector3.Zero);
            Player checker = Add(
                w,
                Side.Away,
                new Vector3(-1.2f, 0, 0),
                traits: new Traits { Checking = 100 }
            );
            checker.Velocity = new Vector3(8, 0, 0);
            bool wasDown = false,
                ignoredStick = true;
            float upAt = -1;
            _ = Run(
                w,
                3f,
                (x, i) =>
                {
                    p.Command = new PlayerCommand { Move = -Vector2.UnitY }; // stick toward the far boards
                    checker.Command = new PlayerCommand { Check = i == 0 };
                    if (p.Action == PlayerAction.Fallen)
                    {
                        wasDown = true;
                        ignoredStick &= p.Position.Z > -0.3f;
                    }
                    else if (wasDown && upAt < 0)
                    {
                        upAt = x.Time;
                    }
                }
            );
            Check(wasDown, "a full-strength check knocks an average player down");
            Check(
                ignoredStick,
                "a fallen player slides where the hit sent them, whatever their stick says"
            );
            Check(
                upAt is > 0.8f and < 2.2f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"and gets up after a moment ({upAt:0.00} s)"
                )
            );
            Check(p.Position.Z < -1f, "then skates on with their stick");
        }

        [Test]
        public void WhiffAndCarrier()
        {
            SeedFrom(22);
            World w = Make();
            Player lone = Add(w, Side.Away, new Vector3(-5, 0, 0));
            Player carrier = Add(w, Side.Home, new Vector3(5, 0, 0));
            Player near = Add(w, Side.Away, new Vector3(6.2f, 0, 0), -Right);
            Give(w, carrier);
            List<MatchEvent> events = Run(
                w,
                1f,
                (x, i) =>
                {
                    lone.Command = new PlayerCommand { Check = i == 0 };
                    carrier.Command = new PlayerCommand { Check = i == 0 };
                    near.Command = default;
                }
            );
            Check(
                !events.Exists(e => e is CheckLanded),
                "a check at nobody, or from the carrier, hits no one"
            );
            Check(
                lone.Action == PlayerAction.None && lone.Speed > 0.5f,
                "a whiffed check lunges forward and ends"
            );
        }

        [Test]
        public void PickupSwitches()
        {
            SeedFrom(23);
            foreach (
                ControlSwitching mode in new[]
                {
                    ControlSwitching.Auto,
                    ControlSwitching.PassesOnly,
                    ControlSwitching.Manual,
                }
            )
            {
                World w = Make(mode);
                Player human = Add(w, Side.Home, new Vector3(-10, 0, 0));
                Player mate = Add(w, Side.Home, new Vector3(5, 0, 0));
                human.Slot = 0;
                w.Puck.Position = PuckHandling.Blade(mate) + new Vector3(-0.3f, 0, 0);
                w.Puck.Velocity = new Vector3(0.5f, 0, 0);
                _ = Run(
                    w,
                    0.5f,
                    (x, i) =>
                    {
                        if (human.Slot == 0)
                        {
                            human.Command = new PlayerCommand { Move = Vector2.UnitX };
                        }
                    }
                );
                bool follows = mode != ControlSwitching.Manual;
                Check(
                    w.Puck.Carrier == mate.Id
                        && mate.Slot == 0 == follows
                        && human.Slot == -1 == follows,
                    $"{mode}: control {(follows ? "moves" : "stays")} when a teammate picks up the puck"
                );
                if (follows)
                {
                    Check(
                        human.Command.Move == Vector2.Zero,
                        $"{mode}: the old player's stick is cleared"
                    );
                }
            }
        }

        [Test]
        public void DefenceSwitches()
        {
            SeedFrom(26);
            foreach (
                ControlSwitching mode in new[]
                {
                    ControlSwitching.Auto,
                    ControlSwitching.PassesOnly,
                }
            )
            {
                World w = Make(mode);
                Player human = Add(w, Side.Home, new Vector3(-20, 0, 0));
                Player near = Add(w, Side.Home, new Vector3(6, 0, 3));
                Player far = Add(w, Side.Home, new Vector3(-5, 0, 0));
                Player them = Add(w, Side.Away, new Vector3(9, 0, 0), -Right);
                human.Slot = 0;
                w.Puck.Position = PuckHandling.Blade(them) + new Vector3(0.2f, 0, 0);
                Run(w, 0.5f);
                bool auto = mode == ControlSwitching.Auto;
                Check(
                    w.Puck.Carrier == them.Id && near.Slot == 0 == auto && far.Slot == -1,
                    $"{mode}: on defence control {(auto ? "goes to the skater nearest the puck" : "stays put")}"
                );
            }
        }

        [Test]
        public void DefenceSettles()
        {
            SeedFrom(28);
            World w = Make();
            // two teammates equally near a carrier circling between them: control must not flicker
            Player a = Add(w, Side.Home, new Vector3(-3, 0, 0));
            _ = Add(w, Side.Home, new Vector3(3, 0, 0));
            Player them = Add(w, Side.Away, new Vector3(0, 0, 4));
            a.Slot = 0;
            Give(w, them);
            int switches = 0,
                last = a.Id;
            _ = Run(
                w,
                4f,
                (x, i) =>
                {
                    float t = i * 0.08f;
                    them.Command = new PlayerCommand
                    {
                        Move = new Vector2(SimMath.Cos(t), SimMath.Sin(t)),
                    };
                    int now = x.Controlled(0).Select(p => p.Id).Or(-1);
                    if (now != last)
                    {
                        switches++;
                    }

                    last = now;
                }
            );
            Check(
                switches <= 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"defence switching does not flicker between two close teammates ({switches} switches in 4 s)"
                )
            );
        }

        [Test]
        public void ManualButton()
        {
            SeedFrom(29);
            World w = Make(ControlSwitching.Manual);
            Player human = Add(w, Side.Home, new Vector3(-20, 0, 0));
            Player near = Add(w, Side.Home, new Vector3(6, 0, 3));
            _ = Add(w, Side.Home, new Vector3(-5, 0, 0));
            Player them = Add(w, Side.Away, new Vector3(9, 0, 0), -Right);
            human.Slot = 0;
            Give(w, them);
            _ = Run(
                w,
                0.5f,
                (x, i) =>
                {
                    if (human.Slot == 0)
                    {
                        human.Command = new PlayerCommand { SwitchPlayer = i == 10 };
                    }
                }
            );
            Check(
                near.Slot == 0 && human.Slot == -1,
                "Manual: the switch button moves control to the teammate nearest the puck"
            );

            World w2 = Make(ControlSwitching.Auto);
            Player carrier = Add(w2, Side.Home, new Vector3(0, 0, 0));
            _ = Add(w2, Side.Home, new Vector3(1, 0, 2));
            carrier.Slot = 0;
            Give(w2, carrier);
            _ = Run(
                w2,
                0.3f,
                (x, i) => carrier.Command = new PlayerCommand { SwitchPlayer = i == 5 }
            );
            Check(carrier.Slot == 0, "the switch button does nothing while you carry the puck");
        }

        [Test]
        public void KnockedDownHuman()
        {
            SeedFrom(31);
            // on hard, where an AI check hits the human in full (see HumanEdge)
            World w = new WorldBuilder()
                .Settings(s =>
                    s with
                    {
                        Switching = ControlSwitching.Auto,
                        Difficulty = Difficulty.Hard,
                    }
                )
                .NoStarters()
                .NotBegun()
                .Build();
            Player human = Add(w, Side.Home, Vector3.Zero);
            Player mate = Add(w, Side.Home, new Vector3(-8, 0, 2));
            Player checker = Add(
                w,
                Side.Away,
                new Vector3(-1.2f, 0, 0),
                traits: new Traits { Checking = 100 }
            );
            checker.Velocity = new Vector3(8, 0, 0);
            human.Slot = 0;
            Give(w, human);
            _ = Run(
                w,
                0.6f,
                (x, i) =>
                    checker.Command = new PlayerCommand { Check = i == 0, Move = Vector2.UnitX }
            );
            Check(
                human.Action == PlayerAction.Fallen
                    && w.Controlled(0).Is(c => c != human && c.Side == Side.Home),
                "Auto: a knocked-down human's control goes to a teammate on their feet"
            );
            _ = mate;
        }

        /// <summary>
        /// A human (Slot 0) skating with an AI carrier at 5 m/s and pressing check every 8 steps
        /// for 3 s, from <paramref name="at"/> at <paramref name="velocity"/> with the stick held <paramref name="stick"/>.
        /// </summary>
        private (int Hits, int Downs, float First) Pester(
            Vector3 at,
            Vector3 velocity,
            Vector2 stick,
            Difficulty difficulty = Difficulty.Normal
        )
        {
            World w = new WorldBuilder()
                .Settings(s =>
                    s with
                    {
                        Switching = ControlSwitching.PassesOnly,
                        Difficulty = difficulty,
                    }
                )
                .NoStarters()
                .NotBegun()
                .Build();
            Player carrier = Add(w, Side.Away, Vector3.Zero);
            carrier.Velocity = new Vector3(5, 0, 0);
            Player human = Add(w, Side.Home, at, MathF.Atan2(velocity.X, velocity.Z));
            human.Velocity = velocity;
            human.Slot = 0;
            Give(w, carrier);
            int downs = 0;
            bool wasDown = false;
            List<MatchEvent> events = Run(
                w,
                3f,
                (x, i) =>
                {
                    carrier.Command = new PlayerCommand { Move = Vector2.UnitX };
                    human.Command = new PlayerCommand { Move = stick, Check = i > 0 && i % 8 == 0 };
                    bool down = carrier.Action == PlayerAction.Fallen;
                    if (down && !wasDown)
                    {
                        downs++;
                    }

                    wasDown = down;
                }
            );
            List<CheckLanded> hits =
            [
                .. events.OfType<CheckLanded>().Where(e => e.CheckerId == human.Id),
            ];
            return (hits.Count, downs, hits.Count > 0 ? hits[0].Strength : 0);
        }

        [Test]
        public void HumanCheckLands()
        {
            SeedFrom(40);
            foreach (
                (string name, Vector3 at, Vector3 velocity, Vector2 stick, bool alongside) in new[]
                {
                    (
                        "alongside",
                        new Vector3(0, 0, 1.2f),
                        new Vector3(5, 0, 0),
                        Vector2.UnitX,
                        true
                    ),
                    (
                        "from behind",
                        new Vector3(-3, 0, 0),
                        new Vector3(7, 0, 0),
                        Vector2.UnitX,
                        false
                    ),
                    (
                        "at an angle",
                        new Vector3(-2, 0, 2.5f),
                        new Vector3(6, 0, -3),
                        Vector2.Normalize(new Vector2(1, -0.6f)),
                        false
                    ),
                }
            )
            {
                int landed = 0,
                    downed = 0;
                float first = 0;
                for (int t = 0; t < 10; t++)
                {
                    (int hits, int downs, float strength) = Pester(at, velocity, stick);
                    if (hits > 0)
                    {
                        landed++;
                    }

                    if (downs > 0)
                    {
                        downed++;
                    }

                    first += strength / 10;
                }
                VerifyHumanCheckOutcome(name, alongside, landed, downed, first);
            }

            (int _, int _, float head) = Pester(
                new Vector3(6, 0, 0),
                new Vector3(-4, 0, 0),
                -Vector2.UnitX
            );
            Check(
                head >= CheckingModel.HardHit,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"head-on at speed is a hard hit ({head:0.00})"
                )
            );
        }

        private void VerifyHumanCheckOutcome(
            string name,
            bool alongside,
            int landed,
            int downed,
            float first
        )
        {
            Check(
                landed == 10,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name}: a human mashing check lands it every time ({landed}/10)"
                )
            );
            Check(
                downed >= 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name}: and knocks the carrier down in a fair share of tries ({downed}/10)"
                )
            );
            if (alongside)
            {
                Check(
                    first is > 0.75f and < 1f,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"alongside at equal speed a first hit lands at strength 0.75-1 ({first:0.00})"
                    )
                );
            }
        }

        [Test]
        public void CheckBehindWhiffs()
        {
            SeedFrom(51);
            World w = Make();
            Player human = Add(w, Side.Home, Vector3.Zero);
            Player behind = Add(w, Side.Away, new Vector3(-1.2f, 0, 0.3f));
            human.Slot = 0;
            List<MatchEvent> events = Run(
                w,
                0.7f,
                (x, i) =>
                {
                    human.Command = new PlayerCommand { Move = Vector2.UnitX, Check = i == 0 };
                    behind.Command = default;
                }
            );
            Check(
                !events.Exists(e => e is CheckLanded),
                "a check never lands on an opponent behind the checker"
            );
        }

        [Test]
        public void MashingBuffers()
        {
            SeedFrom(52);
            World w = Make();
            Player human = Add(w, Side.Home, new Vector3(-20, 0, 5)); // away from the loose puck: a carrier cannot check
            human.Slot = 0;
            int starts = 0;
            float last = float.MaxValue;
            int pressAt = (int)
                MathF.Round(
                    (CheckingModel.Recover - (CheckingModel.Buffer / 2)) / Game.Step,
                    MidpointRounding.ToEven
                );
            _ = Run(
                w,
                1f,
                (x, i) =>
                {
                    human.Command = new PlayerCommand { Check = i == 0 || i == pressAt };
                    float time =
                        human.Action == PlayerAction.Check ? human.ActionTime : float.MaxValue;
                    if (time < last)
                    {
                        starts++; // a check began, or began again straight after the last one
                    }

                    last = time;
                }
            );
            Check(
                starts == 2 && human.Action == PlayerAction.Check,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a press late in a check's recover starts the next check when it ends ({starts} checks)"
                )
            );
        }

        [Test]
        [Arguments(Difficulty.Easy)]
        [Arguments(Difficulty.Normal)]
        [Arguments(Difficulty.Hard)]
        public void ReachByDifficulty(Difficulty difficulty)
        {
            World w = new WorldBuilder()
                .Settings(s => s with { Difficulty = difficulty })
                .NoStarters()
                .NotBegun()
                .Build();
            Player human = Add(w, Side.Home, Vector3.Zero);
            Player ai = Add(w, Side.Home, new Vector3(0, 0, -30));
            Player them = Add(w, Side.Away, Vector3.Zero);
            human.Slot = 0;
            CheckReach reach = CheckingModel.Reach(human, difficulty);
            bool Hits(Player checker, float distance, float angle)
            {
                them.Position =
                    checker.Position
                    + (
                        new Vector3(SimMath.Sin(Right - angle), 0, SimMath.Cos(Right - angle))
                        * distance
                    );
                return CheckingModel.Target(w, checker) == them;
            }

            Check(
                Hits(human, reach.Contact - 0.1f, 0),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{difficulty}: a check just inside the reach ({reach.Contact - 0.1f:0.0} m) lands"
                )
            );
            Check(
                !Hits(human, reach.Contact + 0.1f, 0),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{difficulty}: just outside it ({reach.Contact + 0.1f:0.0} m) whiffs"
                )
            );
            Check(
                Hits(human, 1.2f, reach.Cone - 0.05f) && !Hits(human, 1.2f, reach.Cone + 0.05f),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{difficulty}: the cone ends at {reach.Cone * 180 / MathF.PI:0}°"
                )
            );
            Check(
                Hits(ai, CheckingModel.Contact - 0.1f, MathF.PI / 2)
                    && !Hits(ai, CheckingModel.Contact + 0.1f, 0),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{difficulty}: an AI check reaches its own {CheckingModel.Contact} m, alongside too"
                )
            );
        }

        [Test]
        public void AlongsideReachByDifficulty()
        {
            SeedFrom(53);
            foreach (
                (Difficulty difficulty, bool far, bool near) in new[]
                {
                    (Difficulty.Easy, true, true),
                    (Difficulty.Normal, false, true),
                    (Difficulty.Hard, false, false),
                }
            )
            {
                (int farHits, int _, float _) = Pester(
                    new Vector3(0, 0, 2f),
                    new Vector3(5, 0, 0),
                    Vector2.UnitX,
                    difficulty
                );
                (int nearHits, int _, float _) = Pester(
                    new Vector3(0, 0, 1.3f),
                    new Vector3(5, 0, 0),
                    Vector2.UnitX,
                    difficulty
                );
                Check(
                    (farHits > 0) == far,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{difficulty}: a human check at an opponent alongside 2 m away {(far ? "lands" : "whiffs")} ({farHits} hits)"
                    )
                );
                Check(
                    (nearHits > 0) == near,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{difficulty}: alongside 1.3 m away it {(near ? "lands" : "whiffs")} ({nearHits} hits)"
                    )
                );
            }

            (int squareHits, int _, float hard) = Pester(
                new Vector3(-1.5f, 0, 0),
                new Vector3(5, 0, 0),
                Vector2.UnitX,
                Difficulty.Hard
            );
            (int _, int _, float normal) = Pester(
                new Vector3(-1.5f, 0, 0),
                new Vector3(5, 0, 0),
                Vector2.UnitX
            );
            Check(
                squareHits > 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Hard: a square-on check from 1.5 m behind lands ({squareHits} hits)"
                )
            );
            Check(
                hard < normal - 0.05f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Hard: the same hit is softer than on Normal ({hard:0.00} vs {normal:0.00})"
                )
            );
        }
    }
}
