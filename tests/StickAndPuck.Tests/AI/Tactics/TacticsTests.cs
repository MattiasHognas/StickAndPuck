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
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.Tactics
{
    /// <summary>
    /// The club tactics: every club file carries tactics, the league plays more than one way, and a
    /// misspelt tactic fails the load. Then two clubs play each other, one with an aggressive 2-1-2
    /// forecheck that dumps the puck in and one with a 1-3-1 trap that carries it, a period each way
    /// round, three times over with different seeds, and they play visibly differently
    /// (<see cref="PressAgainstTrap"/>). Formerly SAP_SCENE=tacticstest.
    /// </summary>
    public sealed class TacticsTests : CheckedTest
    {
        /// <summary>The first seed (the scene's SAP_SEED fallback).</summary>
        private const int Seed = 1;

        /// <summary>The club files: every club has tactics, the league is not all one style, and a typo fails the load.</summary>
        [Test]
        public void Data()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string[] raw = [.. ResourceLoader.List(Assets.ClubFolder, ".json")];
            bool all =
                raw.Length > 0
                && raw.All(f =>
                    ResourceLoader.ReadText(f).Contains("\"tactics\"", StringComparison.Ordinal)
                );
            Check(all, $"every club file sets its tactics ({raw.Length} files)");
            int styles = clubs.Select(c => c.Tactics.Forecheck).Distinct().Count();
            int entries = clubs.Select(c => c.Tactics.Entry).Distinct().Count();
            Console.WriteLine(
                "      "
                    + string.Join(
                        ", ",
                        clubs.Select(c =>
                            $"{c.ShortName} {c.Tactics.Forecheck}/{c.Tactics.Entry}/{c.Tactics.PowerPlay}"
                        )
                    )
            );
            Check(
                styles == 3 && entries == 2,
                "the league plays every forecheck and both zone entries"
            );

            Check(
                Fails( /*lang=json,strict*/
                    "{\"forecheck\": \"zone\"}"
                ),
                "an unknown tactic fails the club load"
            );
            Check(
                Fails( /*lang=json,strict*/
                    "{\"forcheck\": \"trap\"}"
                ),
                "a misspelt tactics key fails the club load"
            );
            string path = Path.Combine(
                Path.GetTempPath(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"sap_tactics_{Environment.ProcessId}.json"
                )
            );
            File.WriteAllText(
                path,
                /*lang=json,strict*/
                "{\"id\": \"t\", \"tactics\": {\"forecheck\": \"trap\", \"entry\": \"dumpAndChase\", \"powerPlay\": \"oneThreeOne\"}}"
            );
            ClubData club = Clubs.Load(path);
            File.Delete(path);
            Check(
                club.Tactics
                    == new ClubTactics
                    {
                        Forecheck = Forecheck.Trap,
                        Entry = ZoneEntry.DumpAndChase,
                        PowerPlay = PowerPlayShape.OneThreeOne,
                    },
                "a club file's tactics load as written"
            );
        }

        private static bool Fails(string tactics)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"sap_tactics_bad_{Environment.ProcessId}.json"
                )
            );
            File.WriteAllText(path, $"{{\"id\": \"t\", \"tactics\": {tactics}}}");
            try
            {
                _ = Clubs.Load(path);
                return false;
            }
            catch (System.Text.Json.JsonException)
            {
                return true;
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>How many times <see cref="PressAgainstTrap"/> plays its pair of periods, each with its own seeds.</summary>
        private const int Rounds = 3;

        /// <summary>Gets the periods' worlds, shared by the class's tests.</summary>
        [ClassDataSource<Played<World>>(Shared = SharedType.PerClass)]
        public required Played<World> ComparePlayed { get; init; }

        /// <summary>Gets the periods: round by round, the 2-1-2 club at home then away.</summary>
        public static IEnumerable<int> PeriodNumbers()
        {
            return Enumerable.Range(0, Rounds * 2);
        }

        /// <summary>One period of the 2-1-2 dump-and-chase club against the 1-3-1 trap club; <see cref="PressAgainstTrap"/> judges them.</summary>
        [Test]
        [ExecutionPriority(Priority.High)]
        [MethodDataSource(nameof(PeriodNumbers))]
        public void PlayPeriod(int i)
        {
            ComparePlayed.Add(i, ComparePeriod(WorldBuilder.Clubs, new Settings(), Seed, i));
        }

        /// <summary>The 2-1-2 dump-and-chase club against the 1-3-1 trap club that carries the puck in.</summary>
        [Test]
        [DependsOn(nameof(PlayPeriod), [typeof(int)])]
        public void PressAgainstTrap()
        {
            Compare(ComparePlayed.All(Rounds * 2));
        }

        /// <summary>Plays period <paramref name="i"/> of the comparison: a 2-1-2 dump-and-chase club against a 1-3-1 trap club
        /// that carries the puck in, the 2-1-2 club at home in the even ones.</summary>
        /// <param name="clubs">Every club; the first two are used.</param>
        /// <param name="settings">Player options.</param>
        /// <param name="seed">Randomness seed.</param>
        /// <param name="i">Which period, 0 to 5.</param>
        /// <returns>The match played.</returns>
        private static World ComparePeriod(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            int seed,
            int i
        )
        {
            ClubTactics press = new()
            {
                Forecheck = Forecheck.Aggressive,
                Entry = ZoneEntry.DumpAndChase,
            };
            ClubTactics trap = new() { Forecheck = Forecheck.Trap, Entry = ZoneEntry.Carry };
            Side side = (Side)(i % 2);
            World w = Make(
                clubs,
                settings,
                side == Side.Home ? press : trap,
                side == Side.Home ? trap : press
            );
            Play(w, Systems(w, seed + i), (20 * 60) + 300); // seed + 2 * round + side
            return w;
        }

        /// <summary>Checks the clubs played the comparison's periods differently.</summary>
        /// <param name="played">The periods, from <see cref="ComparePeriod"/>.</param>
        private void Compare(World[] played)
        {
            TacticsTally p = new();
            TacticsTally t = new();
            int goals = 0,
                pressShots = 0,
                trapShots = 0;
            for (int round = 0; round < Rounds; round++)
            {
                foreach (Side pressSide in new[] { Side.Home, Side.Away })
                {
                    Side trapSide = pressSide == Side.Home ? Side.Away : Side.Home;
                    World world = played[(2 * round) + (int)pressSide];
                    Add(p, TacticsState.Tally(world, pressSide));
                    Add(t, TacticsState.Tally(world, trapSide));
                    goals += world.Match.Score[0] + world.Match.Score[1];
                    pressShots += world.Match.Shots[(int)pressSide];
                    trapShots += world.Match.Shots[(int)trapSide];
                    Console.WriteLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"      period with 2-1-2 at {pressSide}, seed {Seed + (2 * round) + (int)pressSide}: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}"
                        )
                    );
                }
            }
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      2-1-2 dump and chase: {p.Forecheckers:F1} skaters deep and {p.Trappers:F1} in the neutral zone on the other side's breakout, {p.Back:F1} back as it leaves the zone, {p.Dumps} dumps ({p.Recovered} got back), {p.Carried}/{p.Entries} entries carried ({p.CarryShare:P0})"
                )
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      1-3-1 trap, carry in:  {t.Forecheckers:F1} skaters deep and {t.Trappers:F1} in the neutral zone on the other side's breakout, {t.Back:F1} back as it leaves the zone, {t.Dumps} dumps, {t.Carried}/{t.Entries} entries carried ({t.CarryShare:P0})"
                )
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      {2 * Rounds} periods: {goals} goals, shots on goal {pressShots} for the 2-1-2 club and {trapShots} for the trap club"
                )
            );
            Check(
                p.Forecheckers > 2.5f && p.Forecheckers > t.Forecheckers + 1.5f,
                "the 2-1-2 club forechecks deep in the other zone, the trap club does not"
            );
            Check(
                t.Back > 2.5f && t.Back > p.Back + 1.5f && t.Trappers > p.Trappers + 1f,
                "the trap club waits back in the neutral zone as the other side breaks out, the 2-1-2 club is caught deep"
            );
            Check(
                p.Dumps >= 10 * Rounds && t.Dumps == 0,
                "the dump-and-chase club dumps the puck in, the carrying club never does"
            );
            Check(
                p.CarryShare + 0.1f < t.CarryShare,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the carrying club carries clearly more of its zone entries over the line ({t.CarryShare:P0} vs {p.CarryShare:P0})"
                )
            );
            Check(
                goals <= 14 * Rounds && pressShots >= 6 * Rounds && trapShots >= 6 * Rounds,
                "the tactics still play hockey: shots for both clubs, a normal number of goals"
            );
        }

        private static void Add(TacticsTally into, TacticsTally from)
        {
            into.Entries += from.Entries;
            into.Carried += from.Carried;
            into.Dumps += from.Dumps;
            into.Recovered += from.Recovered;
            into.BreakoutSeconds += from.BreakoutSeconds;
            into.DeepSkaterSeconds += from.DeepSkaterSeconds;
            into.NeutralSkaterSeconds += from.NeutralSkaterSeconds;
            into.Exits += from.Exits;
            into.BackAtExits += from.BackAtExits;
        }

        /// <summary>A 20 minute all-AI match between the first two clubs, each with its own tactics, at the opening faceoff.</summary>
        /// <param name="clubs">Every club; the first two are used.</param>
        /// <param name="settings">Player options.</param>
        /// <param name="home">The home club's tactics.</param>
        /// <param name="away">The away club's tactics.</param>
        /// <returns>The match.</returns>
        internal static World Make(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            ClubTactics home,
            ClubTactics away
        )
        {
            MatchSetup setup = Scenes.DefaultSetup(clubs);
            setup = setup with
            {
                Home = setup.Home with { Tactics = home },
                Away = setup.Away with { Tactics = away },
                PeriodSeconds = 20 * 60,
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world, log: false);
            return world;
        }

        /// <summary>Runs one period, or until a time limit.</summary>
        private static void Play(World world, ISystem[] systems, float seconds)
        {
            int steps = (int)(seconds / Game.Step);
            for (
                int i = 0;
                i < steps && world.Match.Phase != MatchPhase.Final && world.Match.Period < 2;
                i++
            )
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
            }
        }

        /// <summary>Every AI, skating, puck and rules system of a full match, started on the world.</summary>
        /// <param name="world">The match.</param>
        /// <param name="seed">The seed of every random stream.</param>
        /// <returns>The systems, in update order.</returns>
        internal static ISystem[] Systems(World world, int seed)
        {
#pragma warning disable S2245 // fixed seeds so the checks repeat
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
                new OffsideRule(),
                new IcingRule(),
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
