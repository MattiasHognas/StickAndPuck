using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The puck handling checks with skating, handling and puck physics. Covers the W4b "done when":
/// one player carries, passes to a teammate, who shoots and scores; and that puck control, smart
/// passes, shot power and shot accuracy each make a difference. Formerly SAP_SCENE=handlingtest.
/// </summary>
/// <remarks>
/// Each run takes the next handling seed. The scene counted them from 1 through all its groups in
/// order, so each test starts its count where the scene had it when that group began.
/// </remarks>
public sealed class HandlingTests : CheckedTest
{
    const float Right = MathF.PI / 2;
    static World Make() => new(WorldBuilder.DefaultSetup, new Settings());

    /// <summary>The handling seeds a test's runs take in turn.</summary>
    /// <param name="first">The seed of the first run.</param>
    sealed class SeedCount(int first)
    {
        int next = first;

        public int Take() => next++;
    }

    /// <summary>Runs skating, handling and physics for some seconds; <paramref name="each"/> sets commands before each step.</summary>
    static List<MatchEvent> Run(SeedCount seeds, World world, float seconds, Action<World, int>? each = null, PuckHandlingSystem? handling = null)
    {
#pragma warning disable S2245 // a fixed seed per run so the checks repeat
        ISystem[] systems = [new SkatingSystem(), handling ?? new PuckHandlingSystem(new Random(seeds.Take())), new PuckPhysics()];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(world);
        var events = new List<MatchEvent>();
        int steps = (int)MathF.Round(seconds / Game.Step);
        for (int i = 0; i < steps; i++)
        {
            world.Events.Clear();
            each?.Invoke(world, i);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static Player Add(World w, Side side, Vector3 at, float heading = Right, Traits? traits = null)
    {
        var p = w.AddPlayer(side, new PlayerData { Position = PlayingPosition.C, Traits = traits ?? new Traits() }, at);
        p.Heading = heading;
        return p;
    }

    /// <summary>Adds a skater placed so that their blade is at a point.</summary>
    static Player AddBladeAt(World w, Side side, Vector3 blade, float heading = Right, Traits? traits = null)
    {
        return Add(w, side, blade - PuckHandling.Blade(Vector3.Zero, heading), heading, traits);
    }

    static void Give(World w, Player p)
    {
        w.Puck.Carrier = p.Id;
        w.Puck.Position = PuckHandling.Blade(p);
        w.Puck.Velocity = Vector3.Zero;
    }

    static void Press(Player p, int step, int at, bool pass = false, int shootFrom = -1, int shootTo = -1)
    {
        var c = p.Command;
        if (pass) c.Pass = step == at;
        if (shootFrom >= 0) c.Shoot = step >= shootFrom && step < shootTo;
        p.Command = c;
    }

    [Test]
    public void CarryScoreSequence()
    {
        var seeds = new SeedCount(1);
        var w = Make();
        // the done-when: a carries up ice, passes to b (control follows), b shoots and scores
        var a = Add(w, Side.Home, new Vector3(-15, 0, 2));
        var b = Add(w, Side.Home, new Vector3(10, 0, -4), traits: new Traits { ShotAccuracy = 90 });
        a.Slot = 0;
        Give(w, a);
        a.Command = new PlayerCommand { Move = Vector2.UnitX };
        int got = -1;
        var events = Run(seeds, w, 6f, (x, i) =>
        {
            if (i == 90) a.Command = new PlayerCommand { Move = Vector2.Normalize(new Vector2(1, -0.4f)), Pass = true };
            if (got < 0 && x.Puck.Carrier == b.Id) got = i;
            if (got >= 0 && b.Slot == 0) b.Command = new PlayerCommand { Move = Vector2.UnitX, Shoot = i >= got + 50 && i < got + 56 };
        });
        Check(events.Any(e => e.Kind == MatchEventKind.Pass && e.PlayerId == a.Id), "the carrier skates up ice and passes");
        Check(b.Slot == 0 && a.Slot == -1, "control follows the pass to the receiver");
        Check(events.Any(e => e.Kind == MatchEventKind.Shot && e.PlayerId == b.Id), "the receiver takes the pass and shoots");
        var goal = events.FirstOrDefault(e => e.Kind == MatchEventKind.Goal);
        Check(goal.Kind == MatchEventKind.Goal && goal.PlayerId == b.Id && goal.Side == Side.Home, "and scores, credited to the shooter");
    }

    [Test]
    public void CarryKeepsPuck()
    {
        var seeds = new SeedCount(2);
        var w = Make();
        var p = Add(w, Side.Home, new Vector3(0, 0, 0));
        Give(w, p);
        bool outside = false;
        float worst = 0;
        Run(seeds, w, 8f, (x, i) =>
        {
            // sprint in circles, then straight into the side boards
            float a = i * 0.05f;
            p.Command = new PlayerCommand { Move = i < 300 ? new Vector2(SimMath.Cos(a), SimMath.Sin(a)) : Vector2.UnitY, Sprint = true };
            var pk = x.Puck.Position;
            outside |= MathF.Abs(pk.Z) > RinkSpec.HalfWidth || MathF.Abs(pk.X) > RinkSpec.HalfLength;
            if (i > 10 && i < 300) worst = Math.Max(worst, Vector3.Distance(pk, PuckHandling.Blade(p)));
        });
        Check(w.Puck.Carrier == p.Id, "a skater sprinting in circles and into the boards keeps the puck");
        Check(worst < 0.5f, $"the carried puck stays on the blade (worst {worst:0.00} m)");
        Check(!outside, "the carried puck never leaves the rink");
    }

    [Test]
    public void PickUpLoose()
    {
        var seeds = new SeedCount(3);
        var w = Make();
        var p = AddBladeAt(w, Side.Home, new Vector3(0, 0, 0));
        w.Puck.Position = new Vector3(6, 0, 0);
        p.Command = new PlayerCommand { Move = Vector2.UnitX };
        Run(seeds, w, 2f);
        Check(w.Puck.Carrier == p.Id, "a skater skating onto a loose puck takes it");

        // the same hard puck: a soft-handed player cannot take it, a good one can
        foreach (var (control, speed, takes) in new[] { (10, 24f, false), (90, 24f, true), (10, 12f, true) })
        {
            var t = Make();
            var q = Add(t, Side.Home, new Vector3(0, 0, 0), traits: new Traits { PuckControl = control });
            var blade = PuckHandling.Blade(q);
            t.Puck.Position = blade - new Vector3(speed * 0.3f, 0, 0);
            t.Puck.Velocity = new Vector3(speed, 0, 0);
            Run(seeds, t, 0.5f);
            Check((t.Puck.Carrier == q.Id) == takes, $"puck control {control} {(takes ? "takes" : "cannot take")} a {speed:0} m/s puck");
        }
    }

    [Test]
    public void ControlFollowsPass()
    {
        var seeds = new SeedCount(7);
        foreach (var switching in new[] { ControlSwitching.Auto, ControlSwitching.Manual })
        {
            var w = Make();
            w.Settings = w.Settings with { Switching = switching };
            var a = Add(w, Side.Home, new Vector3(-10, 0, 0));
            var b = Add(w, Side.Home, new Vector3(5, 0, 1), -Right);
            Add(w, Side.Home, new Vector3(-10, 0, 9));   // off to the side: not the one the stick points at
            a.Slot = 0;
            Give(w, a);
            var events = Run(seeds, w, 3f, (_, i) => Press(a, i, 5, pass: true));
            bool follows = switching != ControlSwitching.Manual;
            Check(w.Puck.Carrier == b.Id, $"a pass reaches the teammate the stick points at ({switching})");
            Check(follows ? b.Slot == 0 && a.Slot < 0 : a.Slot == 0 && b.Slot < 0,
                $"with switching {switching} control {(follows ? "follows the pass" : "stays with the passer")}");
            Check(events.Count(e => e.Kind == MatchEventKind.Pass) == 1, "one pass event");
        }
    }

    [Test]
    public void Interception()
    {
        var seeds = new SeedCount(9);
        var w = Make();
        var a = Add(w, Side.Home, new Vector3(-10, 0, 0));
        Add(w, Side.Home, new Vector3(8, 0, 1), -Right);
        var mid = (PuckHandling.Blade(a) + PuckHandling.Blade(w.Players[1])) / 2;
        var thief = AddBladeAt(w, Side.Away, mid + new Vector3(0, 0, 0.3f), -Right);
        Give(w, a);
        Run(seeds, w, 3f, (_, i) => Press(a, i, 5, pass: true));
        Check(w.Puck.Carrier == thief.Id, "an opponent in the passing lane intercepts");
    }

    [Test]
    public void HockeyIqLead()
    {
        var seeds = new SeedCount(10);
        // a receiver skating across the ice: a smart passer leads them, a poor one passes behind
        int Completed(int smart)
        {
            int done = 0;
            for (int n = 0; n < 20; n++)
            {
                var w = Make();
                var a = Add(w, Side.Home, new Vector3(-10, 0, -4 + n * 0.3f), traits: new Traits { HockeyIq = smart });
                var b = Add(w, Side.Home, new Vector3(8, 0, -8), 0);
                b.Velocity = new Vector3(0, 0, SkatingModel.TopSpeed(b.Data.Traits) * 1.15f);   // already flat out
                b.Command = new PlayerCommand { Move = Vector2.UnitY, Sprint = true };
                Give(w, a);
                Run(seeds, w, 3f, (_, i) => Press(a, i, 0, pass: true));
                if (w.Puck.Carrier == b.Id) done++;
            }
            return done;
        }
        int smart = Completed(95), poor = Completed(5);
        Check(smart >= 16 && smart > poor, $"smart passes lead a moving receiver: {smart}/20 completed vs {poor}/20 for a poor passer");
    }

    [Test]
    public void ShotKinds()
    {
        var seeds = new SeedCount(50);
        float Shot(int power, int holdSteps)
        {
            var w = Make();
            var p = Add(w, Side.Home, new Vector3(10, 0, 0), traits: new Traits { ShotPower = power });
            Give(w, p);
            var ev = Run(seeds, w, 2f, (_, i) => Press(p, i, 0, shootFrom: 0, shootTo: holdSteps));
            return ev.FirstOrDefault(e => e.Kind == MatchEventKind.Shot).Strength;
        }
        float wrist = Shot(50, 6), slap = Shot(50, 60), weak = Shot(10, 6), strong = Shot(90, 6);
        Check(wrist > 15 && slap > wrist * 1.2f, $"a held shot is a harder slap shot ({slap:0} vs wrist {wrist:0} m/s)");
        Check(strong > weak + 3, $"shot power makes harder shots ({strong:0} vs {weak:0} m/s)");
    }

    [Test]
    public void AccuracyMatters()
    {
        var seeds = new SeedCount(54);
        int Goals(int accuracy)
        {
            int goals = 0;
            for (int n = 0; n < 30; n++)
            {
                var w = Make();
                var p = Add(w, Side.Home, new Vector3(12, 0, -3 + n * 0.2f), traits: new Traits { ShotAccuracy = accuracy });
                Give(w, p);
                p.Command = new PlayerCommand { Move = new Vector2(1, n % 2 == 0 ? 1 : -1) };   // pick a corner
                var ev = Run(seeds, w, 2f, (_, i) => Press(p, i, 0, shootFrom: 0, shootTo: 6));
                if (ev.Any(e => e.Kind == MatchEventKind.Goal)) goals++;
            }
            return goals;
        }
        int sharp = Goals(95), wild = Goals(5);
        Check(sharp >= 24 && sharp > wild + 4, $"shot accuracy puts shots on target into an empty net: {sharp}/30 vs {wild}/30");
    }

    [Test]
    public void NoPickupInNet()
    {
        var seeds = new SeedCount(114);
        var w = Make();
        var p = AddBladeAt(w, Side.Home, new Vector3(RinkSpec.GoalLineX - 0.1f, 0, 0));
        w.Puck.Position = new Vector3(RinkSpec.GoalLineX + 0.4f, 0, 0);
        Run(seeds, w, 1f);
        Check(w.Puck.Carrier < 0 && p.Id >= 0, "a puck inside the net is not taken");
    }
}
