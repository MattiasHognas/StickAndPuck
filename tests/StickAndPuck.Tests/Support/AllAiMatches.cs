using System.Collections.Concurrent;
using System.Globalization;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.Tests.Support
{
    /// <summary>
    /// The full all-AI matches that <see cref="Goalie.SaveRateTests"/> and <see cref="AI.Decisions.MovesTests"/> both judge, each
    /// played once: match m is club m at home to club m + 1, seed m + 1, the same system set as aimatchtest.
    /// <see cref="Goalie.SaveRateTests.PlayMatch"/> plays them, one test case each; anything that depends on it reads them back.
    /// </summary>
    public static class AllAiMatches
    {
        /// <summary>How many matches there are.</summary>
        public const int Count = 10;

        private static readonly ConcurrentDictionary<int, Lazy<AllAiMatch>> Matches = new();

        /// <summary>Gets the matches, 0 to <see cref="Count"/> - 1.</summary>
        public static IEnumerable<int> Numbers()
        {
            return Enumerable.Range(0, Count);
        }

        /// <summary>Gets match <paramref name="m"/>, playing it if nobody has yet.</summary>
        public static AllAiMatch Get(int m)
        {
            return Matches.GetOrAdd(m, i => new Lazy<AllAiMatch>(() => Play(i))).Value;
        }

        private static AllAiMatch Play(int m)
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            MatchSetup setup = WorldBuilder.DefaultSetup with
            {
                Home = clubs[m % clubs.Count],
                Away = clubs[(m + 1) % clubs.Count],
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new WorldBuilder().Setup(setup).Seed(m + 1).Build();
            ISystem[] systems = Systems(world, m + 1);
            Counts count = new();
            int steps = (int)(3 * 60 * 60 / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
            {
                int before = Tally(world).Dekes;
                int carrier = world.Puck.Carrier;
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
                count.Step(world, carrier, before);
            }
            List<Faced> faced =
            [
                .. count
                    .GoalieSaves.Keys.Union(count.GoalieGoals.Keys)
                    .Select(id => world.Players[id])
                    .Select(g => new Faced(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{world.Setup.Club(g.Side).ShortName} #{g.Data.Number}"
                        ),
                        g.Data.Traits.GoalieReflexes,
                        count.GoalieSaves.GetValueOrDefault(g.Id),
                        count.GoalieGoals.GetValueOrDefault(g.Id)
                    )),
            ];
            (int _, int OneTimers, int Passes, int Completed) = Tally(world);
            return new AllAiMatch(
                world.Match.Shots[0] + world.Match.Shots[1],
                world.Match.Score[0] + world.Match.Score[1],
                count.Saves,
                count.Caught,
                faced,
                new Moves(
                    count.Dekes,
                    count.Kept,
                    OneTimers,
                    count.OneTimers,
                    count.OnGoal,
                    count.OneTimerGoals,
                    count.Stray,
                    Passes,
                    Completed
                )
            );
        }

        /// <summary>What a match comes to so far, counted step by step.</summary>
        private sealed class Counts
        {
            private readonly Dictionary<int, float> struck = [];
            private int carried = -1;

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
                if (now > before)
                {
                    Dekes += now - before;
                    if (world.Puck.Carrier == carrier)
                    {
                        Kept++;
                    }
                }
                foreach (
                    int shooterId in world
                        .Events.Of<ShotTaken>()
                        .Select(e => e.ShooterId)
                        .Where(id => carrier != id)
                )
                {
                    OneTimers++;
                    struck[shooterId] = world.Time;
                }
                foreach (GoalScored goal in world.Events.Of<GoalScored>())
                {
                    if (struck.TryGetValue(goal.ScorerId, out float at) && world.Time - at < 2f)
                    {
                        OneTimerGoals++;
                        OnGoal++;
                        _ = struck.Remove(goal.ScorerId);
                    }
                    if (
                        world
                            .Players.FirstOrNone(p => p.OnIce && p.IsGoalie && p.Side != goal.Side)
                            .TryGet(out Player g)
                    )
                    {
                        GoalieGoals[g.Id] = GoalieGoals.GetValueOrDefault(g.Id) + 1;
                    }
                }
                foreach (ShotSaved save in world.Events.Of<ShotSaved>())
                {
                    Saves++;
                    if (world.Puck.Carrier == save.GoalieId)
                    {
                        Caught++;
                    }

                    if (GoalieModel.OnNet(save))
                    {
                        GoalieSaves[save.GoalieId] =
                            GoalieSaves.GetValueOrDefault(save.GoalieId) + 1;
                    }

                    if (struck.Values.Any(at => world.Time - at < 1.5f))
                    {
                        OnGoal++;
                        struck.Clear();
                    }
                }
                Puck pk = world.Puck;
                if (
                    pk.Carrier >= 0
                    && pk.Carrier != carried
                    && pk.Carrier < world.Players.Count
                    && world.Players[pk.Carrier].Command.Shoot
                    && !world.Players[pk.Carrier].IsGoalie
                )
                {
                    Stray++;
                }

                carried = pk.Carrier;
            }
        }

        /// <summary>Both clubs' AI decisions added up.</summary>
        private static (int Dekes, int OneTimers, int Passes, int Completed) Tally(World world)
        {
            DecisionTally h = DecisionState.Tally(world, Side.Home);
            DecisionTally a = DecisionState.Tally(world, Side.Away);
            return (
                h.Dekes + a.Dekes,
                h.OneTimers + a.OneTimers,
                h.Passes + a.Passes,
                h.Completed + a.Completed
            );
        }

        /// <summary>The all-AI system set (aimatchtest's), its dice seeded with <paramref name="seed"/>, started.</summary>
        public static ISystem[] Systems(World world, int seed)
        {
#pragma warning disable S2245 // fixed seeds so the matches repeat
            ISystem[] systems =
            [
                new PositioningSystem(new Random(seed)),
                new DefenceSystem(new Random(seed)),
                new SpecialTeamsSystem(new Random(seed)),
                new TacticsSystem(new Random(seed)),
                new DecisionSystem(new Random(seed)),
                new SkatingSystem(),
                new CheckingSystem(new Random(seed)),
                new PuckHandlingSystem(new Random(seed)),
                new GoalieSystem { Dice = new Random(seed) },
                new PuckPhysics(),
                new MatchRules(),
                new OvertimeRules(),
            ];
#pragma warning restore S2245
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return systems;
        }
    }
}
