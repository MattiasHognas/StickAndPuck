using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests;

/// <summary>
/// How often shots go in: full all-AI matches between the clubs, each club home and away against two
/// others, counting shots on goal, goals and every starting goalie's saves. The "done when" of the
/// save tuning is about one shot in ten going in, with the best club goalies clearly saving more than
/// the weakest.
/// </summary>
public sealed class SaveRateTests : CheckedTest
{
    const int Matches = 56;

    /// <summary>One goalie's shots faced in one match.</summary>
    public sealed record Faced(string Goalie, int Reflexes, int Saves, int Goals);

    /// <summary>What one match came to: shots on goal and goals for both clubs, and each goalie's line.</summary>
    public sealed record SaveMatch(int Shots, int Goals, int Saves, int Caught, IReadOnlyList<Faced> Goalies);

    /// <summary>Gets the matches' results, shared by the class's tests.</summary>
    [ClassDataSource<Played<SaveMatch>>(Shared = SharedType.PerClass)]
    public required Played<SaveMatch> MatchesPlayed { get; init; }

    /// <summary>Gets the matches, 0 to <see cref="Matches"/> - 1.</summary>
    public static IEnumerable<int> MatchNumbers() => Enumerable.Range(0, Matches);

    /// <summary>One full match; <see cref="ShootingPercentage"/> judges them all.</summary>
    [Test]
    [ExecutionPriority(Priority.Critical)]
    [MethodDataSource(nameof(MatchNumbers))]
    public void PlayMatch(int m)
    {
        var clubs = WorldBuilder.Clubs;
        int home = m % clubs.Count, away = (home + 1 + (6 * (m / clubs.Count))) % clubs.Count;
        var setup = WorldBuilder.DefaultSetup with
        {
            Home = clubs[home], Away = clubs[away],
            Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)],
        };
        var world = new WorldBuilder().Setup(setup).Seed(m + 1).Build();
        var systems = Systems(world, m + 1);
        var saves = new Dictionary<int, int>();
        var goals = new Dictionary<int, int>();
        int all = 0, caught = 0;
        int steps = (int)(3 * 60 * 60 / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            foreach (var save in world.Events.Of<ShotSaved>())
            {
                all++;
                if (world.Puck.Carrier == save.GoalieId) caught++;
            }
            foreach (var save in world.Events.Of<ShotSaved>())
                if (GoalieModel.OnNet(save)) saves[save.GoalieId] = saves.GetValueOrDefault(save.GoalieId) + 1;
            foreach (var goal in world.Events.Of<GoalScored>())
                if (world.Players.FirstOrNone(p => p.OnIce && p.IsGoalie && p.Side != goal.Side).TryGet(out var g)) goals[g.Id] = goals.GetValueOrDefault(g.Id) + 1;
        }
        var faced = saves.Keys.Union(goals.Keys).Select(id => world.Players[id])
            .Select(g => new Faced($"{world.Setup.Club(g.Side).ShortName} #{g.Data.Number}", g.Data.Traits.GoalieReflexes, saves.GetValueOrDefault(g.Id), goals.GetValueOrDefault(g.Id)))
            .ToList();
        MatchesPlayed.Add(m, new SaveMatch(world.Match.Shots[0] + world.Match.Shots[1], world.Match.Score[0] + world.Match.Score[1], all, caught, faced));
    }

    /// <summary>About one shot on goal in ten goes in, and the best goalies save clearly more than the weakest.</summary>
    [Test]
    [DependsOn(nameof(PlayMatch), [typeof(int)])]
    public void ShootingPercentage()
    {
        var played = MatchesPlayed.All(Matches);
        int shots = played.Sum(p => p.Shots), goals = played.Sum(p => p.Goals);
        float shooting = (float)goals / Math.Max(1, shots);
        var lines = played.SelectMany(p => p.Goalies).GroupBy(f => f.Goalie)
            .Select(g => (Goalie: g.Key, g.First().Reflexes, Saves: g.Sum(f => f.Saves), Goals: g.Sum(f => f.Goals)))
            .Where(g => g.Saves + g.Goals >= 40)
            .OrderByDescending(g => g.Reflexes).ToList();
        foreach (var g in lines) Console.WriteLine($"      {g.Goalie} reflexes {g.Reflexes}: {g.Saves} saves, {g.Goals} goals, {Pct(g.Saves, g.Goals):P1}");
        Console.WriteLine($"      {played.Length} matches: {goals} goals on {shots} shots on goal, {shooting:P1}, {(float)goals / played.Length:F1} goals a match");
        int third = Math.Max(1, lines.Count / 3);
        var best = lines.Take(third).ToList();
        var weakest = lines.TakeLast(third).ToList();
        float bestPct = Pct(best.Sum(g => g.Saves), best.Sum(g => g.Goals)), weakestPct = Pct(weakest.Sum(g => g.Saves), weakest.Sum(g => g.Goals));
        Console.WriteLine($"      {played.Sum(p => p.Caught)} of {played.Sum(p => p.Saves)} saves held ({(float)played.Sum(p => p.Caught) / Math.Max(1, played.Sum(p => p.Saves)):P0})");
        Console.WriteLine($"      best third of goalies {bestPct:P1}, weakest third {weakestPct:P1}");
        Check(shooting is > 0.07f and < 0.13f, $"about one shot on goal in ten goes in ({shooting:P1})");
        Check(bestPct > weakestPct + 0.03f, $"the best goalies save clearly more than the weakest ({bestPct:P1} against {weakestPct:P1})");
    }

    static float Pct(int saves, int goals) => (float)saves / Math.Max(1, saves + goals);

    static ISystem[] Systems(World world, int seed)
    {
#pragma warning disable S2245 // fixed seeds so the matches repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed)), new TacticsSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return systems;
    }
}
