#pragma warning disable S2245 // game dice with fixed seeds, so every run fires the same shots

using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Physics;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The goalie checks (W5): stops most straight shots, leaves some rebounds, and better reflexes
/// save more. Formerly SAP_SCENE=goalietest.
/// </summary>
public sealed class GoalieTests : CheckedTest
{
    const int Rounds = 400;

    /// <summary>What a batch of shots came to.</summary>
    readonly record struct Tally(int Saves, int Rebounds, int Goals, bool StayedInCrease)
    {
        public float SavePct => Saves + Goals == 0 ? 0 : (float)Saves / (Saves + Goals);
    }

    /// <summary>An empty world: each check adds its own goalie.</summary>
    static World Make() => new WorldBuilder().NoStarters().NotBegun().Build();

    [Test]
    public void StraightShots()
    {
        var straight = Batch(Make(), 50, StraightShot);
        Console.WriteLine($"      straight shots, reflexes 50: {straight}");
        Check(straight.SavePct >= 0.85f, $"stops most straight shots ({straight.SavePct:P0})");
        float reboundShare = (float)straight.Rebounds / Math.Max(1, straight.Saves);
        Check(reboundShare is > 0.15f and < 0.75f, $"leaves some rebounds ({reboundShare:P0} of saves)");
        Check(straight.StayedInCrease, "stays in the crease");
    }

    [Test]
    public void MixedShots()
    {
        var weak = Batch(Make(), 20, GoalieDemo.Shoot);
        var strong = Batch(Make(), 85, GoalieDemo.Shoot);
        Console.WriteLine($"      mixed shots, reflexes 20: {weak}");
        Console.WriteLine($"      mixed shots, reflexes 85: {strong}");
        Check(strong.SavePct > weak.SavePct + 0.05f, $"better reflexes save more ({weak.SavePct:P0} -> {strong.SavePct:P0})");
        Check(weak.Goals > 0 && strong.Goals > 0, "goals still go in against both");
        Check(weak.StayedInCrease && strong.StayedInCrease, "stays in the crease on mixed shots");
    }

    /// <summary>Adds a goalie in the +x net; puck handling 0 keeps it in the crease (it never goes out for a loose puck).</summary>
    static Player AddGoalie(World world, int reflexes)
    {
        return AddGoalie(world, new Traits { GoalieReflexes = reflexes, GoaliePuckHandling = 0 });
    }

    static Player AddGoalie(World world, Traits traits, Hand catches = Hand.Left)
    {
        var data = new PlayerData { Position = PlayingPosition.G, Shoots = catches, Traits = traits };
        var g = world.AddPlayer(Side.Away, data, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0));   // away defends +x in period 1
        g.Heading = -MathF.PI / 2;
        return g;
    }

    /// <summary>A shot from the slot or the top of the circles, aimed within a body width of the middle of the net.</summary>
    static void StraightShot(World world, Random dice)
    {
        var from = new Vector3(RinkSpec.GoalLineX - 8 - dice.NextSingle() * 6, 0, (dice.NextSingle() * 2 - 1) * 2);
        var target = new Vector3(RinkSpec.GoalLineX, 0.05f + dice.NextSingle() * 0.85f, (dice.NextSingle() * 2 - 1) * 0.3f);
        GoalieDemo.Fire(world, from, target, 20 + dice.NextSingle() * 10);
    }

    /// <summary>Runs the goalie and the puck physics together, in game order, for some seconds.</summary>
    static List<MatchEvent> Run(World world, GoalieSystem goalie, PuckPhysics physics, float seconds, Action<World>? each = null)
    {
        var events = new List<MatchEvent>();
        int steps = (int)MathF.Round(seconds / Game.Step);
        for (int i = 0; i < steps; i++)
        {
            world.Events.Clear();
            goalie.Update(world, Game.Step);
            physics.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            each?.Invoke(world);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    /// <summary>Fires many shots at one goalie and counts what happened to each.</summary>
    static Tally Batch(World world, int reflexes, Action<World, Random> shot)
    {
        var g = AddGoalie(world, reflexes);
        using var goalie = new GoalieSystem { Dice = new Random(3) };
        using var physics = new PuckPhysics();
        var dice = new Random(7);
        int saves = 0, rebounds = 0, goals = 0;
        bool inCrease = true;
        for (int i = 0; i < Rounds; i++)
        {
            world.Puck.Carrier = -1;
            g.Position = GoalieModel.Spot(1, new Vector3(14, 0, 0));
            Run(world, goalie, physics, 0.6f);                          // settle back into the crease
            shot(world, dice);
            var events = Run(world, goalie, physics, 1.6f, w => inCrease &= InCrease(g));
            int at = events.FindIndex(e => e.Kind is MatchEventKind.Save or MatchEventKind.Goal);
            if (at < 0) continue;                                       // wide of the net
            if (events[at].Kind == MatchEventKind.Goal)
            {
                goals++;
            }
            else
            {
                saves++;
                if (world.Puck.Carrier != g.Id) rebounds++;
            }
        }
        return new Tally(saves, rebounds, goals, inCrease);
    }

    static bool InCrease(Player g)
    {
        float x = g.Position.X;
        return x > RinkSpec.GoalLineX - RinkSpec.CreaseRadius && x < RinkSpec.GoalLineX && MathF.Abs(g.Position.Z) < RinkSpec.NetWidth / 2 + 0.2f;
    }

    /// <summary>A soft shot is caught, held with a Save event, then played out to the corner when no whistle comes.</summary>
    [Test]
    public void HoldAndPlayOut()
    {
        var world = Make();
        var g = AddGoalie(world, 50);
        using var goalie = new GoalieSystem { Dice = new Random(1) };
        using var physics = new PuckPhysics();
        Run(world, goalie, physics, 0.5f);
        GoalieDemo.Fire(world, new Vector3(RinkSpec.GoalLineX - 6, 0, 0), new Vector3(RinkSpec.GoalLineX, 0.6f, 0), 9);
        var events = Run(world, goalie, physics, 1f);
        Check(world.Puck.Carrier == g.Id, "catches a soft shot at the chest");
        Check(events.Any(e => e.Kind == MatchEventKind.Save && e.Side == Side.Away && e.PlayerId == g.Id), "publishes a Save for the goalie's club");
        Run(world, goalie, physics, GoalieTuning.HoldSeconds + 0.5f);
        Check(world.Puck.Carrier < 0 && world.Puck.Velocity.Length() > 1f, "plays a held puck out when no whistle comes");
    }

    /// <summary>
    /// W34: 100 shots at the glove side and 100 at the blocker side, at hand height, against a goalie
    /// with a weak glove and against an even one. The weak glove lets in more glove-side shots than
    /// its blocker side and than the even goalie's glove side; the AI reads the glove as the weak side.
    /// </summary>
    [Test]
    public void WeakSide()
    {
        var weak = new Traits { GoalieGlove = 25, GoalieBlocker = 75, GoaliePuckHandling = 0 };
        var even = new Traits { GoalieGlove = 50, GoalieBlocker = 50, GoaliePuckHandling = 0 };
        var weakGlove = HandBatch(Make(), weak, +1);
        var weakBlocker = HandBatch(Make(), weak, -1);
        var evenGlove = HandBatch(Make(), even, +1);
        Console.WriteLine($"      weak glove (25/75), glove side: {weakGlove}");
        Console.WriteLine($"      weak glove (25/75), blocker side: {weakBlocker}");
        Console.WriteLine($"      even (50/50), glove side: {evenGlove}");
        Check(weakGlove.Goals > weakBlocker.Goals + 5, $"a weak glove lets in more glove-side goals over 100 shots ({weakGlove.Goals} vs {weakBlocker.Goals} blocker side)");
        Check(weakGlove.Goals > evenGlove.Goals, $"a weak glove lets in more glove-side goals than an even goalie ({weakGlove.Goals} vs {evenGlove.Goals})");

        var world = Make();
        var g = AddGoalie(world, weak);
        Check(GoalieModel.WeakSide(g, 1) == GoalieModel.GloveSide(g.Data, 1), "shooters read the weak glove as the weak side");
        var righty = AddGoalie(world, weak, Hand.Right);
        Check(GoalieModel.GloveSide(righty.Data, 1) == -GoalieModel.GloveSide(g.Data, 1) && GoalieModel.WeakSide(righty, -1) == GoalieModel.GloveSide(righty.Data, -1),
            "a right-catching goalie's glove and weak side are on the other side");
        Check(GoalieModel.WeakSide(AddGoalie(world, even), 1) == 0, "an even goalie has no weak side");
    }

    /// <summary>Shots at one side of the +x net at hand height, from the slot and circles.</summary>
    static Tally HandBatch(World world, Traits traits, int gloveOrBlocker)
    {
        var g = AddGoalie(world, traits);
        int side = gloveOrBlocker * GoalieModel.GloveSide(g.Data, 1);
        using var goalie = new GoalieSystem { Dice = new Random(3) };
        using var physics = new PuckPhysics();
        var dice = new Random(7);
        int saves = 0, goals = 0;
        for (int i = 0; i < 100; i++)
        {
            world.Puck.Carrier = -1;
            g.Position = GoalieModel.Spot(1, new Vector3(14, 0, 0));
            Run(world, goalie, physics, 0.6f);
            var from = new Vector3(RinkSpec.GoalLineX - 7 - dice.NextSingle() * 7, 0, (dice.NextSingle() * 2 - 1) * 3);
            var target = new Vector3(RinkSpec.GoalLineX, 0.55f + dice.NextSingle() * 0.6f, side * (0.35f + dice.NextSingle() * 0.45f));
            GoalieDemo.Fire(world, from, target, 20 + dice.NextSingle() * 12);
            var events = Run(world, goalie, physics, 1.6f);
            int at = events.FindIndex(e => e.Kind is MatchEventKind.Save or MatchEventKind.Goal);
            if (at < 0) continue;
            if (events[at].Kind == MatchEventKind.Goal) goals++;
            else saves++;
        }
        return new Tally(saves, 0, goals, true);
    }

    /// <summary>W34: poor rebound control holds fewer shots and leaves harder rebounds than good control.</summary>
    [Test]
    public void Rebounds()
    {
        var poor = ReboundBatch(Make(), 20);
        var good = ReboundBatch(Make(), 85);
        Console.WriteLine($"      rebound control 20: held {poor.Held:P0}, rebound speed {poor.Speed:F1} m/s, {poor.Slot:P0} into the slot");
        Console.WriteLine($"      rebound control 85: held {good.Held:P0}, rebound speed {good.Speed:F1} m/s, {good.Slot:P0} into the slot");
        Check(good.Held > poor.Held + 0.1f, $"good rebound control holds more shots ({poor.Held:P0} -> {good.Held:P0})");
        Check(poor.Speed > good.Speed * 1.3f, $"poor rebound control leaves harder rebounds ({poor.Speed:F1} vs {good.Speed:F1} m/s)");
        Check(poor.Slot > good.Slot, $"poor rebound control leaves more rebounds in the slot ({poor.Slot:P0} vs {good.Slot:P0})");
    }

    /// <summary>Straight shots at one goalie; what share it held, and how fast and where the rebounds went.</summary>
    static (float Held, float Speed, float Slot) ReboundBatch(World world, int control)
    {
        var g = AddGoalie(world, new Traits { GoalieReboundControl = control, GoaliePuckHandling = 0 });
        using var goalie = new GoalieSystem { Dice = new Random(3) };
        using var physics = new PuckPhysics();
        var dice = new Random(7);
        int saves = 0, held = 0, slot = 0;
        float speed = 0;
        for (int i = 0; i < 200; i++)
        {
            world.Puck.Carrier = -1;
            g.Position = GoalieModel.Spot(1, new Vector3(14, 0, 0));
            Run(world, goalie, physics, 0.6f);
            StraightShot(world, dice);
            bool saved = false;
            Run(world, goalie, physics, 1.2f, w =>
            {
                if (saved || !w.Events.Current.Any(e => e.Kind == MatchEventKind.Save)) return;
                saved = true;
                saves++;
                if (w.Puck.Carrier == g.Id) { held++; return; }
                speed += w.Puck.Velocity.Length();
                var v = Vector3.Normalize(w.Puck.Velocity with { Y = 0 });
                if (MathF.Abs(v.Z) < 0.5f) slot++;
            });
        }
        int rebounds = Math.Max(1, saves - held);
        return ((float)held / Math.Max(1, saves), speed / rebounds, (float)slot / rebounds);
    }

    /// <summary>
    /// W34: with a teammate open in front, a good puck handler plays a held puck to them and goes out
    /// to move a loose puck from behind the net on; a poor one rims a held puck to the corner and
    /// stays in the crease.
    /// </summary>
    [Test]
    public void PuckHandling()
    {
        var (goodPasses, goodPlays) = HandlingBatch(90);
        var (poorPasses, poorPlays) = HandlingBatch(10);
        Console.WriteLine($"      puck handling 90: {goodPasses}/20 held pucks passed to the open mate, {goodPlays}/20 loose pucks behind the net played");
        Console.WriteLine($"      puck handling 10: {poorPasses}/20 held pucks passed to the open mate, {poorPlays}/20 loose pucks behind the net played");
        Check(goodPasses >= 14 && poorPasses <= 6, $"a good puck handler passes a held puck out, a poor one rims it ({goodPasses} vs {poorPasses} of 20)");
        Check(goodPlays >= 14 && poorPlays <= 6, $"a good puck handler plays loose pucks behind the net, a poor one stays home ({goodPlays} vs {poorPlays} of 20)");
    }

    static (int Passes, int Plays) HandlingBatch(int handling)
    {
        int passes = 0, plays = 0;
        for (int i = 0; i < 20; i++)
        {
            var world = Make();
            var g = AddGoalie(world, new Traits { GoaliePuckHandling = handling });
            var mate = world.AddPlayer(Side.Away, new PlayerData { Position = PlayingPosition.D }, new Vector3(RinkSpec.GoalLineX - 9, 0, 4));
            using var goalie = new GoalieSystem { Dice = new Random(100 + i) };
            using var physics = new PuckPhysics();
            Run(world, goalie, physics, 0.3f);
            world.Puck.Live = true;
            world.Puck.Carrier = g.Id;
            var events = Run(world, goalie, physics, GoalieTuning.HoldSeconds + 0.6f);
            if (events.Any(e => e.Kind == MatchEventKind.Pass && e.PlayerId == g.Id)) passes++;

            world.Puck.Carrier = -1;
            g.Position = GoalieModel.Spot(1, new Vector3(14, 0, 0));
            Run(world, goalie, physics, 1f);
            world.Puck.Position = new Vector3(RinkSpec.GoalLineX + 2, 0, (i % 2 == 0 ? 1 : -1) * 1.5f);
            world.Puck.Velocity = Vector3.Zero;
            world.Puck.LastTouch = -1;
            events = Run(world, goalie, physics, 3f);
            if (events.Any(e => e.Kind == MatchEventKind.Pass && e.PlayerId == g.Id) || world.Puck.LastTouch == g.Id) plays++;
            _ = mate;
        }
        return (passes, plays);
    }

    /// <summary>With the puck behind the net the goalie hugs the near post.</summary>
    [Test]
    public void Behind()
    {
        var world = Make();
        var g = AddGoalie(world, 50);
        using var goalie = new GoalieSystem();
        using var physics = new PuckPhysics();
        world.Puck.Position = new Vector3(RinkSpec.GoalLineX + 2, 0, -2);
        world.Puck.Velocity = Vector3.Zero;
        Run(world, goalie, physics, 1.5f);
        Check(g.Position.Z < -0.4f && g.Position.X > RinkSpec.GoalLineX - 0.5f, $"hugs the near post with the puck behind the net (z {g.Position.Z:F2})");
    }
}
