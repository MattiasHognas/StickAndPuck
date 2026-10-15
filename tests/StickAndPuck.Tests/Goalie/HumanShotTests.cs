#pragma warning disable S2245 // game dice with fixed seeds, so every run fires the same shots

using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Shots the way a player takes them, against a club-average goalie (reflexes 66): the stick
/// pointed at the middle of the net, a tapped or briefly held wrist shot from the slot or the
/// circles, or a wound-up slap shot from the circles, through the same shot code a pad uses.
/// About one in ten goes in, and the goalie holds well under half of what it stops.
/// </summary>
public sealed class HumanShotTests : CheckedTest
{
    const int Rounds = 300;

    /// <summary>What a batch of shots came to.</summary>
    readonly record struct Tally(int Saves, int Caught, int Goals, int Wide)
    {
        public float Shooting => (float)Goals / Math.Max(1, Saves + Goals);

        public float CatchShare => (float)Caught / Math.Max(1, Saves);

        public override string ToString() => $"{Goals} goals, {Saves} saves ({Caught} held), {Wide} wide: {Shooting:P1} go in, {CatchShare:P0} of saves held";
    }

    /// <summary>A club-average goalie on normal.</summary>
    static readonly Traits Average = new() { GoalieReflexes = 66, HockeyIq = 64, GoalieGlove = 64, GoalieBlocker = 64, GoalieReboundControl = 64, GoaliePuckHandling = 0 };

    /// <summary>Rögle's starter as hard plays him: reflexes 64, positioning 62, glove 65, blocker 50, all raised.</summary>
    static readonly Traits HardRogle = DifficultyScale.Scale(new Traits { GoalieReflexes = 64, HockeyIq = 62, GoalieGlove = 65, GoalieBlocker = 50, GoalieReboundControl = 58, GoaliePuckHandling = 0 }, Difficulty.Hard);

    [Test]
    public void SlotAndCircles()
    {
        var tapSlot = Batch(Average, SlotSpot, ShotKind.Wrist, 0.05f, AtNet);
        var tapCircle = Batch(Average, CircleSpot, ShotKind.Wrist, 0.05f, AtNet);
        var heldSlot = Batch(Average, SlotSpot, ShotKind.Wrist, 0.2f, AtNet);
        var slap = Batch(Average, CircleSpot, ShotKind.Slap, 0.8f, AtNet);
        Console.WriteLine($"      tapped wrist shots from the slot: {tapSlot}");
        Console.WriteLine($"      tapped wrist shots from the circles: {tapCircle}");
        Console.WriteLine($"      held wrist shots from the slot: {heldSlot}");
        Console.WriteLine($"      slap shots from the circles: {slap}");
        var all = new[] { tapSlot, tapCircle, heldSlot, slap };
        float shooting = (float)all.Sum(t => t.Goals) / Math.Max(1, all.Sum(t => t.Saves + t.Goals));
        float held = (float)all.Sum(t => t.Caught) / Math.Max(1, all.Sum(t => t.Saves));
        Check(tapSlot.Shooting is > 0.07f and < 0.22f && heldSlot.Shooting is > 0.07f and < 0.22f, $"a slot shot goes in about one time in ten ({tapSlot.Shooting:P1} tapped, {heldSlot.Shooting:P1} held)");
        Check(shooting is > 0.06f and < 0.14f, $"about one shot in ten goes in overall ({shooting:P1})");
        Check(held < 0.45f, $"the goalie holds under half of what it stops ({held:P0})");
    }

    /// <summary>
    /// The shots of a played match: the stick pushed up the ice toward the end boards, a wound-up slap
    /// shot or a quick wrist shot from the slot, against an average goalie and against Rögle's on hard.
    /// </summary>
    [Test]
    public void PlayedShots()
    {
        var lines = new List<(string Name, Tally Tally)>();
        foreach (var (who, traits) in new[] { ("average", Average), ("hard Rögle", HardRogle) })
        {
            lines.Add(($"slap shots from the slot, {who}", Batch(traits, SlotSpot, ShotKind.Slap, 0.8f, UpIce)));
            lines.Add(($"tapped wrist shots from the slot, {who}", Batch(traits, SlotSpot, ShotKind.Wrist, 0.05f, UpIce)));
            lines.Add(($"held wrist shots from the slot, {who}", Batch(traits, SlotSpot, ShotKind.Wrist, 0.25f, UpIce)));
            lines.Add(($"slap shots from the circles, {who}", Batch(traits, CircleSpot, ShotKind.Slap, 0.8f, UpIce)));
        }
        foreach (var (name, tally) in lines) Console.WriteLine($"      {name}: {tally}");
        float Pct(string who) => (float)lines.Where(l => l.Name.EndsWith(who, StringComparison.Ordinal)).Sum(l => l.Tally.Goals)
            / Math.Max(1, lines.Where(l => l.Name.EndsWith(who, StringComparison.Ordinal)).Sum(l => l.Tally.Goals + l.Tally.Saves));
        Console.WriteLine($"      average goalie {Pct("average"):P1}, hard Rögle {Pct("hard Rögle"):P1}");
        Check(Pct("average") is > 0.07f and < 0.16f, $"a played shot beats an average goalie about one time in ten ({Pct("average"):P1})");
        Check(Pct("hard Rögle") > 0.06f, $"and still beats Rögle's goalie on hard ({Pct("hard Rögle"):P1})");
    }

    static Vector2 AtNet(Vector3 from, Random dice) => Vector2.Normalize(new Vector2(RinkSpec.GoalLineX - from.X, -from.Z));

    static Vector2 UpIce(Vector3 from, Random dice) => Vector2.Normalize(new Vector2(1f, (dice.NextSingle() * 2 - 1) * 0.3f));

    static Vector3 SlotSpot(Random dice) => new(RinkSpec.GoalLineX - 6 - (dice.NextSingle() * 4), 0, (dice.NextSingle() * 2 - 1) * 2);

    static Vector3 CircleSpot(Random dice) => new(RinkSpec.GoalLineX - 8 - (dice.NextSingle() * 4), 0, (dice.NextSingle() < 0.5f ? -1 : 1) * (5 + dice.NextSingle() * 2));

    static Tally Batch(Traits traits, Func<Random, Vector3> spot, ShotKind kind, float held, Func<Vector3, Random, Vector2> stick)
    {
        var world = new WorldBuilder().NoStarters().NotBegun().Build();
        var data = new PlayerData { Position = PlayingPosition.G, Traits = traits with { GoaliePuckHandling = 0 } };
        var g = world.AddPlayer(Side.Away, data, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0));   // away defends +x in period 1
        g.Heading = -MathF.PI / 2;
        var shooter = world.AddPlayer(Side.Home, new PlayerData { Position = PlayingPosition.C, Traits = new Traits { ShotAccuracy = 60, ShotPower = 60 } }, new Vector3(0, 0, 0));
        using var goalie = new GoalieSystem { Dice = new Random(3) };
        using var physics = new PuckPhysics();
        var dice = new Random(7);
        int saves = 0, caught = 0, goals = 0, wide = 0;
        for (int i = 0; i < Rounds; i++)
        {
            world.Puck.Carrier = -1;
            world.Puck.Live = false;
            var start = spot(dice) with { Y = 0.02f };
            world.Puck.Position = start;
            g.Position = GoalieModel.Spot(1, start, data.Traits.HockeyIq);
            Run(world, goalie, physics, 0.6f);
            shooter.Position = start with { X = start.X - 0.6f, Y = 0 };
            shooter.Command = new PlayerCommand { Move = stick(start, dice) };
            float speed = kind == ShotKind.Slap ? PuckHandling.SlapSpeed(shooter.Data.Traits, held) : PuckHandling.WristSpeed(shooter.Data.Traits);
            world.Puck.Live = true;
            world.Puck.Position = start;
            world.Puck.Velocity = PuckHandling.ShotVelocity(world, shooter, start, kind, speed, dice, held);
            world.Puck.LastTouch = shooter.Id;
            int outcome = Outcome(world, goalie, physics, g);
            wide += outcome == 0 ? 1 : 0;
            goals += outcome < 0 ? 1 : 0;
            saves += outcome > 0 ? 1 : 0;
            caught += outcome == 2 ? 1 : 0;
        }
        return new Tally(saves, caught, goals, wide);
    }

    /// <summary>Runs until the shot is decided: 0 wide, -1 a goal, 1 a rebound, 2 held.</summary>
    static int Outcome(World world, GoalieSystem goalie, PuckPhysics physics, Player g)
    {
        for (int i = 0; i < 96; i++)
        {
            world.Events.Clear();
            goalie.Update(world, Game.Step);
            physics.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            if (world.Events.Current.Any(e => e is GoalScored)) return -1;
            if (world.Events.Current.Any(e => e is ShotSaved)) return world.Puck.Carrier == g.Id ? 2 : 1;
        }
        return 0;
    }

    static void Run(World world, GoalieSystem goalie, PuckPhysics physics, float seconds)
    {
        for (int i = 0; i < (int)(seconds / Game.Step); i++)
        {
            world.Events.Clear();
            goalie.Update(world, Game.Step);
            physics.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }
    }
}
