using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Checking and switching with switching, skating, checking, puck handling and puck physics (W4c):
/// checks knock players off the puck, the checking trait decides how often, and control switching
/// follows each Settings mode. Formerly SAP_SCENE=checkstest.
/// </summary>
public sealed class ChecksTests : CheckedTest
{
    const float Right = MathF.PI / 2;

    /// <summary>
    /// The seed of the next run's random streams. The old scene counted it up from 1 across all its
    /// groups, so each test starts from the seed its group had there.
    /// </summary>
    int seed = 1;

    /// <summary>Starts the seeds where this test's group started in the old scene.</summary>
    void SeedFrom(int first) => seed = first;

    /// <summary>An empty rink with default settings and this switching mode.</summary>
    static World Make(ControlSwitching mode = ControlSwitching.Auto)
    {
        return new WorldBuilder().Settings(s => s with { Switching = mode }).NoStarters().NotBegun().Build();
    }

    /// <summary>Runs the systems for some seconds with no commands set.</summary>
    List<MatchEvent> Run(World world, float seconds) => Run(world, seconds, (_, _) => { });

    /// <summary>Runs the systems for some seconds; <paramref name="each"/> sets commands before each step.</summary>
    List<MatchEvent> Run(World world, float seconds, Action<World, int> each)
    {
#pragma warning disable S2245 // a fixed seed per run so the checks repeat
        ISystem[] systems =
        [
            new SwitchingSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new PuckPhysics(),
        ];
#pragma warning restore S2245
        seed++;
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(world);
        var events = new List<MatchEvent>();
        int steps = (int)MathF.Round(seconds / Game.Step);
        for (int i = 0; i < steps; i++)
        {
            world.Events.Clear();
            each(world, i);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static Player Add(World w, Side side, Vector3 at, float heading = Right, Maybe<Traits> traits = default)
    {
        var p = w.AddPlayer(side, new PlayerData { Position = PlayingPosition.C, Traits = traits.Or(new Traits()) }, at);
        p.Heading = heading;
        return p;
    }

    static void Give(World w, Player p)
    {
        w.Puck.Carrier = p.Id;
        w.Puck.Position = PuckHandling.Blade(p);
        w.Puck.Velocity = Vector3.Zero;
    }

    /// <summary>A checker chases a carrier skating up ice and checks once in reach.</summary>
    (List<MatchEvent> Events, Player Checker, Player Carrier) Chase(World w, int checking, int resisting, float checkerSpeed, float carrierSpeed)
    {
        var carrier = Add(w, Side.Home, new Vector3(0, 0, 0), traits: new Traits { Checking = resisting });
        var checker = Add(w, Side.Away, new Vector3(-2.6f, 0, 0), traits: new Traits { Checking = checking });
        carrier.Velocity = new Vector3(carrierSpeed, 0, 0);
        checker.Velocity = new Vector3(checkerSpeed, 0, 0);
        Give(w, carrier);
        bool pressed = false;
        var events = Run(w, 2.5f, (x, i) =>
        {
            carrier.Command = new PlayerCommand { Move = new Vector2(carrierSpeed > 0 ? 0.3f : 0, 0) };
            bool press = !pressed && Vector3.Distance(checker.Position, carrier.Position) < 1.4f;
            pressed |= press;
            checker.Command = new PlayerCommand { Move = Vector2.UnitX, Sprint = true, Check = press };
        });
        return (events, checker, carrier);
    }

    [Test]
    public void StrongCheckWinsPuck()
    {
        SeedFrom(1);
        int down = 0, won = 0, hits = 0;
        bool slid = true;
        for (int t = 0; t < 10; t++)
        {
            var w = Make(ControlSwitching.Auto);
            var (events, checker, carrier) = Chase(w, 90, 20, 8f, 2f);
            hits += events.Count(e => e is CheckLanded check && check.CheckerId == checker.Id) == 1 ? 1 : 0;
            if (w.Puck.Carrier != carrier.Id && w.Puck.LastTouch != carrier.Id || w.Puck.Carrier == checker.Id) won++;
            if (events.Any(e => e is CheckLanded { Strength: >= CheckingModel.HardHit })) down++;
            slid &= carrier.Position.X > 1.5f;
        }
        Check(hits == 10, $"a check from behind lands exactly once each time ({hits}/10)");
        Check(down >= 9, $"a strong checker at speed hits hard ({down}/10 hard hits)");
        Check(won >= 8, $"and knocks a weak carrier off the puck ({won}/10)");
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
            var w = Make(ControlSwitching.Auto);
            var (events, _, carrier) = Chase(w, 15, 90, 3f, 2f);
            if (w.Puck.Carrier == carrier.Id) kept++;
            soft &= events.OfType<CheckLanded>().All(e => e.Strength < 0.75f);
        }
        Check(soft, "a weak, slow check on a strong carrier is a soft hit");
        Check(kept >= 6, $"which mostly bounces off: the carrier keeps the puck ({kept}/10)");
    }

    [Test]
    public void TraitMatters()
    {
        var w = Make();
        var target = Add(w, Side.Home, Vector3.Zero);
        var weak = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 20 });
        var avg = Add(w, Side.Away, Vector3.Zero);
        var strong = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 80 });
        var tough = Add(w, Side.Home, Vector3.Zero, traits: new Traits { Checking = 80 });
        float s20 = CheckingModel.Strength(weak, target, 5, false), s50 = CheckingModel.Strength(avg, target, 5, false), s80 = CheckingModel.Strength(strong, target, 5, false);
        Check(s20 < s50 && s50 < s80, $"a higher checking trait hits harder ({s20:0.00} < {s50:0.00} < {s80:0.00})");
        Check(MathF.Abs(CheckingModel.Strength(avg, target, 4, false) - 1f) < 0.01f, "an average check closing at 4 m/s has strength 1");
        Check(CheckingModel.Strength(avg, tough, 5, false) < s50, "a higher checking trait takes hits better");
        Check(CheckingModel.Strength(avg, target, 7, false) > s50, "more speed hits harder");
        avg.Energy = 0;
        Check(CheckingModel.Strength(avg, target, 5, false) < s50, "a tired checker hits softer");
    }

    [Test]
    public void FallenSlides()
    {
        SeedFrom(21);
        var w = Make();
        var p = Add(w, Side.Home, Vector3.Zero);
        var checker = Add(w, Side.Away, new Vector3(-1.2f, 0, 0), traits: new Traits { Checking = 100 });
        checker.Velocity = new Vector3(8, 0, 0);
        bool wasDown = false, ignoredStick = true;
        float upAt = -1;
        Run(w, 3f, (x, i) =>
        {
            p.Command = new PlayerCommand { Move = -Vector2.UnitY };   // stick toward the far boards
            checker.Command = new PlayerCommand { Check = i == 0 };
            if (p.Action == PlayerAction.Fallen) { wasDown = true; ignoredStick &= p.Position.Z > -0.3f; }
            else if (wasDown && upAt < 0)
            {
                upAt = x.Time;
            }
        });
        Check(wasDown, "a full-strength check knocks an average player down");
        Check(ignoredStick, "a fallen player slides where the hit sent them, whatever their stick says");
        Check(upAt > 0.8f && upAt < 2.2f, $"and gets up after a moment ({upAt:0.00} s)");
        Check(p.Position.Z < -1f, "then skates on with their stick");
    }

    [Test]
    public void WhiffAndCarrier()
    {
        SeedFrom(22);
        var w = Make();
        var lone = Add(w, Side.Away, new Vector3(-5, 0, 0));
        var carrier = Add(w, Side.Home, new Vector3(5, 0, 0));
        var near = Add(w, Side.Away, new Vector3(6.2f, 0, 0), -Right);
        Give(w, carrier);
        var events = Run(w, 1f, (x, i) =>
        {
            lone.Command = new PlayerCommand { Check = i == 0 };
            carrier.Command = new PlayerCommand { Check = i == 0 };
            near.Command = default;
        });
        Check(!events.Any(e => e is CheckLanded), "a check at nobody, or from the carrier, hits no one");
        Check(lone.Action == PlayerAction.None && lone.Speed > 0.5f, "a whiffed check lunges forward and ends");
    }

    [Test]
    public void PickupSwitches()
    {
        SeedFrom(23);
        foreach (var mode in new[] { ControlSwitching.Auto, ControlSwitching.PassesOnly, ControlSwitching.Manual })
        {
            var w = Make(mode);
            var human = Add(w, Side.Home, new Vector3(-10, 0, 0));
            var mate = Add(w, Side.Home, new Vector3(5, 0, 0));
            human.Slot = 0;
            w.Puck.Position = PuckHandling.Blade(mate) + new Vector3(-0.3f, 0, 0);
            w.Puck.Velocity = new Vector3(0.5f, 0, 0);
            Run(w, 0.5f, (x, i) => { if (human.Slot == 0) human.Command = new PlayerCommand { Move = Vector2.UnitX }; });
            bool follows = mode != ControlSwitching.Manual;
            Check(w.Puck.Carrier == mate.Id && (mate.Slot == 0) == follows && (human.Slot == -1) == follows,
                $"{mode}: control {(follows ? "moves" : "stays")} when a teammate picks up the puck");
            if (follows) Check(human.Command.Move == Vector2.Zero, $"{mode}: the old player's stick is cleared");
        }
    }

    [Test]
    public void DefenceSwitches()
    {
        SeedFrom(26);
        foreach (var mode in new[] { ControlSwitching.Auto, ControlSwitching.PassesOnly })
        {
            var w = Make(mode);
            var human = Add(w, Side.Home, new Vector3(-20, 0, 0));
            var near = Add(w, Side.Home, new Vector3(6, 0, 3));
            var far = Add(w, Side.Home, new Vector3(-5, 0, 0));
            var them = Add(w, Side.Away, new Vector3(9, 0, 0), -Right);
            human.Slot = 0;
            w.Puck.Position = PuckHandling.Blade(them) + new Vector3(0.2f, 0, 0);
            Run(w, 0.5f);
            bool auto = mode == ControlSwitching.Auto;
            Check(w.Puck.Carrier == them.Id && (near.Slot == 0) == auto && far.Slot == -1,
                $"{mode}: on defence control {(auto ? "goes to the skater nearest the puck" : "stays put")}");
        }
    }

    [Test]
    public void DefenceSettles()
    {
        SeedFrom(28);
        var w = Make();
        // two teammates equally near a carrier circling between them: control must not flicker
        var a = Add(w, Side.Home, new Vector3(-3, 0, 0));
        Add(w, Side.Home, new Vector3(3, 0, 0));
        var them = Add(w, Side.Away, new Vector3(0, 0, 4));
        a.Slot = 0;
        Give(w, them);
        int switches = 0, last = a.Id;
        Run(w, 4f, (x, i) =>
        {
            float t = i * 0.08f;
            them.Command = new PlayerCommand { Move = new Vector2(SimMath.Cos(t), SimMath.Sin(t)) };
            int now = x.Controlled(0).Select(p => p.Id).Or(-1);
            if (now != last) switches++;
            last = now;
        });
        Check(switches <= 3, $"defence switching does not flicker between two close teammates ({switches} switches in 4 s)");
    }

    [Test]
    public void ManualButton()
    {
        SeedFrom(29);
        var w = Make(ControlSwitching.Manual);
        var human = Add(w, Side.Home, new Vector3(-20, 0, 0));
        var near = Add(w, Side.Home, new Vector3(6, 0, 3));
        Add(w, Side.Home, new Vector3(-5, 0, 0));
        var them = Add(w, Side.Away, new Vector3(9, 0, 0), -Right);
        human.Slot = 0;
        Give(w, them);
        Run(w, 0.5f, (x, i) => { if (human.Slot == 0) human.Command = new PlayerCommand { SwitchPlayer = i == 10 }; });
        Check(near.Slot == 0 && human.Slot == -1, "Manual: the switch button moves control to the teammate nearest the puck");

        var w2 = Make(ControlSwitching.Auto);
        var carrier = Add(w2, Side.Home, new Vector3(0, 0, 0));
        Add(w2, Side.Home, new Vector3(1, 0, 2));
        carrier.Slot = 0;
        Give(w2, carrier);
        Run(w2, 0.3f, (x, i) => carrier.Command = new PlayerCommand { SwitchPlayer = i == 5 });
        Check(carrier.Slot == 0, "the switch button does nothing while you carry the puck");
    }

    [Test]
    public void KnockedDownHuman()
    {
        SeedFrom(31);
        var w = Make();
        var human = Add(w, Side.Home, Vector3.Zero);
        var mate = Add(w, Side.Home, new Vector3(-8, 0, 2));
        var checker = Add(w, Side.Away, new Vector3(-1.2f, 0, 0), traits: new Traits { Checking = 100 });
        checker.Velocity = new Vector3(8, 0, 0);
        human.Slot = 0;
        Give(w, human);
        Run(w, 0.6f, (x, i) => checker.Command = new PlayerCommand { Check = i == 0, Move = Vector2.UnitX });
        Check(human.Action == PlayerAction.Fallen && w.Controlled(0).Is(c => c != human && c.Side == Side.Home),
            "Auto: a knocked-down human's control goes to a teammate on their feet");
        _ = mate;
    }
    /// <summary>
    /// W99: a human (Slot 0) skating with an AI carrier at 5 m/s and pressing check every 8 steps
    /// for 3 s, from <paramref name="at"/> at <paramref name="velocity"/> with the stick held <paramref name="stick"/>.
    /// </summary>
    (int Hits, int Downs, float First) Pester(Vector3 at, Vector3 velocity, Vector2 stick, Difficulty difficulty = Difficulty.Normal)
    {
        var w = new WorldBuilder().Settings(s => s with { Switching = ControlSwitching.PassesOnly, Difficulty = difficulty }).NoStarters().NotBegun().Build();
        var carrier = Add(w, Side.Away, Vector3.Zero);
        carrier.Velocity = new Vector3(5, 0, 0);
        var human = Add(w, Side.Home, at, MathF.Atan2(velocity.X, velocity.Z));
        human.Velocity = velocity;
        human.Slot = 0;
        Give(w, carrier);
        int downs = 0;
        bool wasDown = false;
        var events = Run(w, 3f, (x, i) =>
        {
            carrier.Command = new PlayerCommand { Move = Vector2.UnitX };
            human.Command = new PlayerCommand { Move = stick, Check = i > 0 && i % 8 == 0 };
            bool down = carrier.Action == PlayerAction.Fallen;
            if (down && !wasDown) downs++;
            wasDown = down;
        });
        var hits = events.OfType<CheckLanded>().Where(e => e.CheckerId == human.Id).ToList();
        return (hits.Count, downs, hits.Count > 0 ? hits[0].Strength : 0);
    }

    [Test]
    public void HumanCheckLands()
    {
        SeedFrom(40);
        foreach (var (name, at, velocity, stick, alongside) in new[]
        {
            ("alongside", new Vector3(0, 0, 1.2f), new Vector3(5, 0, 0), Vector2.UnitX, true),
            ("from behind", new Vector3(-3, 0, 0), new Vector3(7, 0, 0), Vector2.UnitX, false),
            ("at an angle", new Vector3(-2, 0, 2.5f), new Vector3(6, 0, -3), Vector2.Normalize(new Vector2(1, -0.6f)), false),
        })
        {
            int landed = 0, downed = 0;
            float first = 0;
            for (int t = 0; t < 10; t++)
            {
                var (hits, downs, strength) = Pester(at, velocity, stick);
                if (hits > 0) landed++;
                if (downs > 0) downed++;
                first += strength / 10;
            }
            Check(landed == 10, $"{name}: a human mashing check lands it every time ({landed}/10)");
            Check(downed >= 3, $"{name}: and knocks the carrier down in a fair share of tries ({downed}/10)");
            if (alongside) Check(first is > 0.75f and < 1f, $"alongside at equal speed a first hit lands at strength 0.75-1 ({first:0.00})");
        }

        var (_, _, head) = Pester(new Vector3(6, 0, 0), new Vector3(-4, 0, 0), -Vector2.UnitX);
        Check(head >= CheckingModel.HardHit, $"head-on at speed is a hard hit ({head:0.00})");
    }

    [Test]
    public void CheckBehindWhiffs()
    {
        SeedFrom(51);
        var w = Make();
        var human = Add(w, Side.Home, Vector3.Zero);
        var behind = Add(w, Side.Away, new Vector3(-1.2f, 0, 0.3f));
        human.Slot = 0;
        var events = Run(w, 0.7f, (x, i) =>
        {
            human.Command = new PlayerCommand { Move = Vector2.UnitX, Check = i == 0 };
            behind.Command = default;
        });
        Check(!events.Any(e => e is CheckLanded), "a check never lands on an opponent behind the checker");
    }

    [Test]
    public void MashingBuffers()
    {
        SeedFrom(52);
        var w = Make();
        var human = Add(w, Side.Home, new Vector3(-20, 0, 5));   // away from the loose puck: a carrier cannot check
        human.Slot = 0;
        int starts = 0;
        float last = float.MaxValue;
        int pressAt = (int)MathF.Round((CheckingModel.Recover - (CheckingModel.Buffer / 2)) / Game.Step);
        Run(w, 1f, (x, i) =>
        {
            human.Command = new PlayerCommand { Check = i == 0 || i == pressAt };
            float time = human.Action == PlayerAction.Check ? human.ActionTime : float.MaxValue;
            if (time < last) starts++;   // a check began, or began again straight after the last one
            last = time;
        });
        Check(starts == 2 && human.Action == PlayerAction.Check, $"a press late in a check's recover starts the next check when it ends ({starts} checks)");
    }

    [Test]
    [Arguments(Difficulty.Easy)]
    [Arguments(Difficulty.Normal)]
    [Arguments(Difficulty.Hard)]
    public void ReachByDifficulty(Difficulty difficulty)
    {
        var w = new WorldBuilder().Settings(s => s with { Difficulty = difficulty }).NoStarters().NotBegun().Build();
        var human = Add(w, Side.Home, Vector3.Zero);
        var ai = Add(w, Side.Home, new Vector3(0, 0, -30));
        var them = Add(w, Side.Away, Vector3.Zero);
        human.Slot = 0;
        var reach = CheckingModel.Reach(human, difficulty);
        bool Hits(Player checker, float distance, float angle)
        {
            them.Position = checker.Position + (new Vector3(SimMath.Sin(Right - angle), 0, SimMath.Cos(Right - angle)) * distance);
            return CheckingModel.Target(w, checker) == them;
        }

        Check(Hits(human, reach.Contact - 0.1f, 0), $"{difficulty}: a check just inside the reach ({reach.Contact - 0.1f:0.0} m) lands");
        Check(!Hits(human, reach.Contact + 0.1f, 0), $"{difficulty}: just outside it ({reach.Contact + 0.1f:0.0} m) whiffs");
        Check(Hits(human, 1.2f, reach.Cone - 0.05f) && !Hits(human, 1.2f, reach.Cone + 0.05f), $"{difficulty}: the cone ends at {reach.Cone * 180 / MathF.PI:0}°");
        Check(Hits(ai, CheckingModel.Contact - 0.1f, MathF.PI / 2) && !Hits(ai, CheckingModel.Contact + 0.1f, 0),
            $"{difficulty}: an AI check reaches its own {CheckingModel.Contact} m, alongside too");
    }

    [Test]
    public void AlongsideReachByDifficulty()
    {
        SeedFrom(53);
        foreach (var (difficulty, far, near) in new[] { (Difficulty.Easy, true, true), (Difficulty.Normal, false, true), (Difficulty.Hard, false, false) })
        {
            var (farHits, _, _) = Pester(new Vector3(0, 0, 2f), new Vector3(5, 0, 0), Vector2.UnitX, difficulty);
            var (nearHits, _, _) = Pester(new Vector3(0, 0, 1.3f), new Vector3(5, 0, 0), Vector2.UnitX, difficulty);
            Check(farHits > 0 == far, $"{difficulty}: a human check at an opponent alongside 2 m away {(far ? "lands" : "whiffs")} ({farHits} hits)");
            Check(nearHits > 0 == near, $"{difficulty}: alongside 1.3 m away it {(near ? "lands" : "whiffs")} ({nearHits} hits)");
        }

        var (squareHits, _, hard) = Pester(new Vector3(-1.5f, 0, 0), new Vector3(5, 0, 0), Vector2.UnitX, Difficulty.Hard);
        var (_, _, normal) = Pester(new Vector3(-1.5f, 0, 0), new Vector3(5, 0, 0), Vector2.UnitX);
        Check(squareHits > 0, $"Hard: a square-on check from 1.5 m behind lands ({squareHits} hits)");
        Check(hard < normal - 0.05f, $"Hard: the same hit is softer than on Normal ({hard:0.00} vs {normal:0.00})");
    }
}
