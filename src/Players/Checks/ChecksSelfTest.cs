using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;

namespace StickAndPuck.Players;

/// <summary>
/// SAP_SCENE=checkstest: runs the checking and switching checks with switching, skating,
/// checking, puck handling and puck physics and no window, prints one line per check and exits
/// with 0 when all pass, 1 otherwise. Covers the W4c "done when": checks knock players off the
/// puck, the checking trait decides how often, and control switching follows each Settings mode.
/// </summary>
public sealed class ChecksSelfTest : IScene
{
    const float Right = MathF.PI / 2;
    int failures;
    int seed = 1;

    /// <inheritdoc/>
    public string Name => "checkstest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs);
        World Make(ControlSwitching mode = ControlSwitching.Auto) => new(setup, settings with { Switching = mode });

        StrongCheckWinsPuck(Make);
        WeakCheckBounces(Make);
        TraitMatters(Make());
        FallenSlides(Make());
        WhiffAndCarrier(Make());
        PickupSwitches(Make);
        DefenceSwitches(Make);
        DefenceSettles(Make());
        ManualButton(Make);
        KnockedDownHuman(Make());

        Console.WriteLine(failures == 0 ? "CHECKSTEST: all checks passed" : $"CHECKSTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    /// <summary>Runs the systems for some seconds; <paramref name="each"/> sets commands before each step.</summary>
    List<MatchEvent> Run(World world, float seconds, Action<World, int>? each = null)
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

    void StrongCheckWinsPuck(Func<ControlSwitching, World> make)
    {
        int down = 0, won = 0, hits = 0;
        bool slid = true;
        for (int t = 0; t < 10; t++)
        {
            var w = make(ControlSwitching.Auto);
            var (events, checker, carrier) = Chase(w, 90, 20, 8f, 2f);
            hits += events.Count(e => e.Kind == MatchEventKind.Check && e.PlayerId == checker.Id) == 1 ? 1 : 0;
            if (w.Puck.Carrier != carrier.Id && w.Puck.LastTouch != carrier.Id || w.Puck.Carrier == checker.Id) won++;
            if (events.Any(e => e.Kind == MatchEventKind.Check && e.Strength >= CheckingModel.HardHit)) down++;
            slid &= carrier.Position.X > 1.5f;
        }
        Check(hits == 10, $"a check from behind lands exactly once each time ({hits}/10)");
        Check(down >= 9, $"a strong checker at speed hits hard ({down}/10 hard hits)");
        Check(won >= 8, $"and knocks a weak carrier off the puck ({won}/10)");
        Check(slid, "the hit carrier is shoved up ice");
    }

    void WeakCheckBounces(Func<ControlSwitching, World> make)
    {
        int kept = 0;
        for (int t = 0; t < 10; t++)
        {
            var w = make(ControlSwitching.Auto);
            var (_, _, carrier) = Chase(w, 15, 90, 3f, 2f);
            if (w.Puck.Carrier == carrier.Id) kept++;
        }
        Check(kept >= 9, $"a weak, slow check bounces off a strong carrier, who keeps the puck ({kept}/10)");
    }

    void TraitMatters(World w)
    {
        var target = Add(w, Side.Home, Vector3.Zero);
        var weak = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 20 });
        var avg = Add(w, Side.Away, Vector3.Zero);
        var strong = Add(w, Side.Away, Vector3.Zero, traits: new Traits { Checking = 80 });
        var tough = Add(w, Side.Home, Vector3.Zero, traits: new Traits { Checking = 80 });
        float s20 = CheckingModel.Strength(weak, target, 5, false), s50 = CheckingModel.Strength(avg, target, 5, false), s80 = CheckingModel.Strength(strong, target, 5, false);
        Check(s20 < s50 && s50 < s80, $"a higher checking trait hits harder ({s20:0.00} < {s50:0.00} < {s80:0.00})");
        Check(MathF.Abs(s50 - 1f) < 0.01f, "an average check at 5 m/s has strength 1");
        Check(CheckingModel.Strength(avg, tough, 5, false) < s50, "a higher checking trait takes hits better");
        Check(CheckingModel.Strength(avg, target, 7, false) > s50, "more speed hits harder");
        avg.Energy = 0;
        Check(CheckingModel.Strength(avg, target, 5, false) < s50, "a tired checker hits softer");
    }

    void FallenSlides(World w)
    {
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

    void WhiffAndCarrier(World w)
    {
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
        Check(!events.Any(e => e.Kind == MatchEventKind.Check), "a check at nobody, or from the carrier, hits no one");
        Check(lone.Action == PlayerAction.None && lone.Speed > 0.5f, "a whiffed check lunges forward and ends");
    }

    void PickupSwitches(Func<ControlSwitching, World> make)
    {
        foreach (var mode in new[] { ControlSwitching.Auto, ControlSwitching.PassesOnly, ControlSwitching.Manual })
        {
            var w = make(mode);
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

    void DefenceSwitches(Func<ControlSwitching, World> make)
    {
        foreach (var mode in new[] { ControlSwitching.Auto, ControlSwitching.PassesOnly })
        {
            var w = make(mode);
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

    void DefenceSettles(World w)
    {
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
            them.Command = new PlayerCommand { Move = new Vector2(MathF.Cos(t), MathF.Sin(t)) };
            int now = x.Controlled(0)?.Id ?? -1;
            if (now != last) switches++;
            last = now;
        });
        Check(switches <= 3, $"defence switching does not flicker between two close teammates ({switches} switches in 4 s)");
    }

    void ManualButton(Func<ControlSwitching, World> make)
    {
        var w = make(ControlSwitching.Manual);
        var human = Add(w, Side.Home, new Vector3(-20, 0, 0));
        var near = Add(w, Side.Home, new Vector3(6, 0, 3));
        Add(w, Side.Home, new Vector3(-5, 0, 0));
        var them = Add(w, Side.Away, new Vector3(9, 0, 0), -Right);
        human.Slot = 0;
        Give(w, them);
        Run(w, 0.5f, (x, i) => { if (human.Slot == 0) human.Command = new PlayerCommand { SwitchPlayer = i == 10 }; });
        Check(near.Slot == 0 && human.Slot == -1, "Manual: the switch button moves control to the teammate nearest the puck");

        var w2 = make(ControlSwitching.Auto);
        var carrier = Add(w2, Side.Home, new Vector3(0, 0, 0));
        Add(w2, Side.Home, new Vector3(1, 0, 2));
        carrier.Slot = 0;
        Give(w2, carrier);
        Run(w2, 0.3f, (x, i) => carrier.Command = new PlayerCommand { SwitchPlayer = i == 5 });
        Check(carrier.Slot == 0, "the switch button does nothing while you carry the puck");
    }

    void KnockedDownHuman(World w)
    {
        var human = Add(w, Side.Home, Vector3.Zero);
        var mate = Add(w, Side.Home, new Vector3(-8, 0, 2));
        var checker = Add(w, Side.Away, new Vector3(-1.2f, 0, 0), traits: new Traits { Checking = 100 });
        checker.Velocity = new Vector3(8, 0, 0);
        human.Slot = 0;
        Give(w, human);
        Run(w, 0.6f, (x, i) => checker.Command = new PlayerCommand { Check = i == 0, Move = Vector2.UnitX });
        Check(human.Action == PlayerAction.Fallen && w.Controlled(0) is { } c && c != human && c.Side == Side.Home,
            "Auto: a knocked-down human's control goes to a teammate on their feet");
        _ = mate;
    }
}
