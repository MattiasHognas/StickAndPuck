using System.Collections.Concurrent;
using System.Globalization;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using AiDecisions = StickAndPuck.App.AI.Decisions;

namespace StickAndPuck.Tests.Support;

/// <summary>
/// The full all-AI matches that <see cref="SaveRateTests"/> and <see cref="MovesTests"/> both judge, each
/// played once: match m is club m at home to club m + 1, seed m + 1, the same system set as aimatchtest.
/// <see cref="SaveRateTests.PlayMatch"/> plays them, one test case each; anything that depends on it reads them back.
/// </summary>
public static class AllAiMatches
{
    /// <summary>How many matches there are.</summary>
    public const int Count = 10;

    static readonly ConcurrentDictionary<int, Lazy<AllAiMatch>> Matches = new();

    /// <summary>Gets the matches, 0 to <see cref="Count"/> - 1.</summary>
    public static IEnumerable<int> Numbers() => Enumerable.Range(0, Count);

    /// <summary>Gets match <paramref name="m"/>, playing it if nobody has yet.</summary>
    public static AllAiMatch Get(int m) => Matches.GetOrAdd(m, i => new Lazy<AllAiMatch>(() => Play(i))).Value;

    static AllAiMatch Play(int m)
    {
        var clubs = WorldBuilder.Clubs;
        var setup = WorldBuilder.DefaultSetup with
        {
            Home = clubs[m % clubs.Count], Away = clubs[(m + 1) % clubs.Count],
            Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)],
        };
        var world = new WorldBuilder().Setup(setup).Seed(m + 1).Build();
        var systems = Systems(world, m + 1);
        var count = new Counts();
        int steps = (int)(3 * 60 * 60 / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            int before = Tally(world).Dekes;
            int carrier = world.Puck.Carrier;
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            count.Step(world, carrier, before);
        }
        var faced = count.GoalieSaves.Keys.Union(count.GoalieGoals.Keys).Select(id => world.Players[id])
            .Select(g => new Faced(string.Create(CultureInfo.InvariantCulture, $"{world.Setup.Club(g.Side).ShortName} #{g.Data.Number}"), g.Data.Traits.GoalieReflexes, count.GoalieSaves.GetValueOrDefault(g.Id), count.GoalieGoals.GetValueOrDefault(g.Id)))
            .ToList();
        var tally = Tally(world);
        return new AllAiMatch(
            world.Match.Shots[0] + world.Match.Shots[1], world.Match.Score[0] + world.Match.Score[1], count.Saves, count.Caught, faced,
            new Moves(count.Dekes, count.Kept, tally.OneTimers, count.OneTimers, count.OnGoal, count.OneTimerGoals, count.Stray, tally.Passes, tally.Completed));
    }

    /// <summary>What a match comes to so far, counted step by step.</summary>
    sealed class Counts
    {
        readonly Dictionary<int, float> struck = [];
        int carried = -1;

        public Dictionary<int, int> GoalieSaves { get; } = [];

        public Dictionary<int, int> GoalieGoals { get; } = [];

        public int Saves { get; private set; }

        public int Caught { get; private set; }

        public int Dekes { get; private set; }

        public int Kept { get; private set; }

        public int OneTimers { get; private set; }

        public int OnGoal { get; private set; }

        public int OneTimerGoals { get; private set; }

        public int Stray { get; private set; }

        /// <summary>Counts one step's events; <paramref name="carrier"/> had the puck and <paramref name="before"/> dekes were made before it.</summary>
        public void Step(World world, int carrier, int before)
        {
            int now = Tally(world).Dekes;
            if (now > before) { Dekes += now - before; if (world.Puck.Carrier == carrier) Kept++; }
            foreach (var e in world.Events.Of<ShotTaken>())
            {
                if (carrier == e.ShooterId) continue;
                OneTimers++;
                struck[e.ShooterId] = world.Time;
            }
            foreach (var goal in world.Events.Of<GoalScored>())
            {
                if (struck.TryGetValue(goal.ScorerId, out float at) && world.Time - at < 2f) { OneTimerGoals++; OnGoal++; struck.Remove(goal.ScorerId); }
                if (world.Players.FirstOrNone(p => p.OnIce && p.IsGoalie && p.Side != goal.Side).TryGet(out var g)) GoalieGoals[g.Id] = GoalieGoals.GetValueOrDefault(g.Id) + 1;
            }
            foreach (var save in world.Events.Of<ShotSaved>())
            {
                Saves++;
                if (world.Puck.Carrier == save.GoalieId) Caught++;
                if (GoalieModel.OnNet(save)) GoalieSaves[save.GoalieId] = GoalieSaves.GetValueOrDefault(save.GoalieId) + 1;
                if (struck.Values.Any(at => world.Time - at < 1.5f)) { OnGoal++; struck.Clear(); }
            }
            var pk = world.Puck;
            if (pk.Carrier >= 0 && pk.Carrier != carried && pk.Carrier < world.Players.Count && world.Players[pk.Carrier].Command.Shoot && !world.Players[pk.Carrier].IsGoalie) Stray++;
            carried = pk.Carrier;
        }
    }

    /// <summary>Both clubs' AI decisions added up.</summary>
    static (int Dekes, int OneTimers, int Passes, int Completed) Tally(World world)
    {
        var h = AiDecisions.Tally(world, Side.Home);
        var a = AiDecisions.Tally(world, Side.Away);
        return (h.Dekes + a.Dekes, h.OneTimers + a.OneTimers, h.Passes + a.Passes, h.Completed + a.Completed);
    }

    /// <summary>The all-AI system set (aimatchtest's), its dice seeded with <paramref name="seed"/>, started.</summary>
    public static ISystem[] Systems(World world, int seed)
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

/// <summary>One goalie's shots faced in one match.</summary>
public sealed record Faced(string Goalie, int Reflexes, int Saves, int Goals);

/// <summary>The AI's moves in one match: dekes (and how many kept the puck), one-timers armed and struck, passes.</summary>
public sealed record Moves(int Dekes, int DekesKept, int Armed, int Struck, int OnGoal, int OneTimerGoals, int Stray, int Passes, int Completed);

/// <summary>What one all-AI match came to: shots on goal, goals, saves (and how many held), each goalie's line, and the AI's moves.</summary>
public sealed record AllAiMatch(int Shots, int Goals, int Saves, int Caught, IReadOnlyList<Faced> Goalies, Moves Moves)
{
    public override string ToString() => $"goals {Goals}, shots on goal {Shots}, dekes {Moves.Dekes} ({Moves.DekesKept} kept), one-timers armed {Moves.Armed}, struck {Moves.Struck} ({Moves.OnGoal} on goal), scored {Moves.OneTimerGoals}, stray {Moves.Stray}";
}
