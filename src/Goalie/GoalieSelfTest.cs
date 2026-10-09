#pragma warning disable S2245 // game dice with fixed seeds, so every run fires the same shots

using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;

namespace StickAndPuck.Goalie;

/// <summary>
/// SAP_SCENE=goalietest: runs the goalie checks without opening a window, prints one line per check
/// and exits with 0 when all pass, 1 otherwise. Covers the W5 "done when": stops most straight
/// shots, leaves some rebounds, and better reflexes save more.
/// </summary>
public sealed class GoalieSelfTest : IScene
{
    const int Rounds = 400;
    int failures;

    /// <summary>What a batch of shots came to.</summary>
    readonly record struct Tally(int Saves, int Rebounds, int Goals, bool StayedInCrease)
    {
        public float SavePct => Saves + Goals == 0 ? 0 : (float)Saves / (Saves + Goals);
    }

    /// <inheritdoc/>
    public string Name => "goalietest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs);
        World Make() => new(setup, settings);

        var straight = Batch(Make(), 50, StraightShot);
        Console.WriteLine($"      straight shots, reflexes 50: {straight}");
        Check(straight.SavePct >= 0.85f, $"stops most straight shots ({straight.SavePct:P0})");
        float reboundShare = (float)straight.Rebounds / Math.Max(1, straight.Saves);
        Check(reboundShare is > 0.15f and < 0.75f, $"leaves some rebounds ({reboundShare:P0} of saves)");
        Check(straight.StayedInCrease, "stays in the crease");

        var weak = Batch(Make(), 20, GoalieDemo.Shoot);
        var strong = Batch(Make(), 85, GoalieDemo.Shoot);
        Console.WriteLine($"      mixed shots, reflexes 20: {weak}");
        Console.WriteLine($"      mixed shots, reflexes 85: {strong}");
        Check(strong.SavePct > weak.SavePct + 0.05f, $"better reflexes save more ({weak.SavePct:P0} -> {strong.SavePct:P0})");
        Check(weak.Goals > 0 && strong.Goals > 0, "goals still go in against both");
        Check(weak.StayedInCrease && strong.StayedInCrease, "stays in the crease on mixed shots");

        HoldAndPlayOut(Make());
        Behind(Make());

        Console.WriteLine(failures == 0 ? "GOALIETEST: all checks passed" : $"GOALIETEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    static Player AddGoalie(World world, int reflexes)
    {
        var data = new PlayerData { Position = PlayingPosition.G, Traits = new Traits { GoalieReflexes = reflexes } };
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
            if (events[at].Kind == MatchEventKind.Goal) goals++;
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
    void HoldAndPlayOut(World world)
    {
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

    /// <summary>With the puck behind the net the goalie hugs the near post.</summary>
    void Behind(World world)
    {
        var g = AddGoalie(world, 50);
        using var goalie = new GoalieSystem();
        using var physics = new PuckPhysics();
        world.Puck.Position = new Vector3(RinkSpec.GoalLineX + 2, 0, -2);
        world.Puck.Velocity = Vector3.Zero;
        Run(world, goalie, physics, 1.5f);
        Check(g.Position.Z < -0.4f && g.Position.X > RinkSpec.GoalLineX - 0.5f, $"hugs the near post with the puck behind the net (z {g.Position.Z:F2})");
    }
}
