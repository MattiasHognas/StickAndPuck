using System.Globalization;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Difficulty;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.Difficulty
{
    /// <summary>
    /// The difficulty: the trait scaling (easy lowers, hard raises, normal and versus untouched, only
    /// the club no human plays), then ten full all-AI matches of the same two clubs, one scaled hard and
    /// one easy, swapping ends every game so home ice evens out: the "done when" is that hard clearly
    /// beats easy. Formerly SAP_SCENE=difficultytest.
    /// </summary>
    public sealed class DifficultyTests : CheckedTest
    {
        [Test]
        public void Scaling()
        {
            Traits t = new()
            {
                HockeyIq = 60,
                ShotAccuracy = 95,
                TopSpeed = 4,
                GoalieReflexes = 70,
            };
            Traits easy = DifficultyScale.Scale(t, App.Core.Difficulty.Easy);
            Traits hard = DifficultyScale.Scale(t, App.Core.Difficulty.Hard);
            Check(
                easy.HockeyIq < t.HockeyIq && easy.ShotAccuracy < t.ShotAccuracy,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"easy lowers the skater traits (hockey IQ {t.HockeyIq} -> {easy.HockeyIq})"
                )
            );
            Check(
                hard.HockeyIq > t.HockeyIq && hard.ShotAccuracy > t.ShotAccuracy,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"hard raises the skater traits (hockey IQ {t.HockeyIq} -> {hard.HockeyIq})"
                )
            );
            Check(
                hard.ShotAccuracy == 100 && easy.TopSpeed == 0,
                "the scaled traits stay within 0 to 100"
            );
            Check(
                DifficultyScale.Scale(t, App.Core.Difficulty.Normal) == t,
                "normal keeps the skater traits from the data files"
            );
            int[] reflexes =
            [
                .. new[]
                {
                    App.Core.Difficulty.Easy,
                    App.Core.Difficulty.Normal,
                    App.Core.Difficulty.Hard,
                }.Select(l => DifficultyScale.Goalie(t, l).GoalieReflexes),
            ];
            Check(
                reflexes[0] < reflexes[1]
                    && reflexes[1] < reflexes[2]
                    && reflexes[2] <= t.GoalieReflexes,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"every difficulty takes the AI goalie down, easy most and hard least (reflexes {t.GoalieReflexes} -> {string.Join(", ", reflexes)})"
                )
            );

            MatchSetup setup = WorldBuilder.DefaultSetup; // the keyboard on the home club
            Check(
                DifficultyScale.AiSide(setup) == SideOrNone.Away,
                "the difficulty applies to the club no human plays"
            );
            MatchSetup scaled = new World(
                setup,
                new Settings { Difficulty = App.Core.Difficulty.Hard }
            ).Setup;
            bool homeKept = ReferenceEquals(scaled.Home, setup.Home);
            bool awayRaised =
                scaled
                    .Away.Roster.Zip(setup.Away.Roster)
                    .All(p => p.First.Traits.HockeyIq >= p.Second.Traits.HockeyIq)
                && scaled.Away.Roster.Average(p => p.Traits.HockeyIq)
                    > setup.Away.Roster.Average(p => p.Traits.HockeyIq) + 10;
            VerifyScaledTraits(setup, homeKept, awayRaised);
        }

        private void VerifyScaledTraits(MatchSetup setup, bool homeKept, bool awayRaised)
        {
            Check(
                homeKept && awayRaised,
                "a hard match raises the AI club's whole squad and leaves the human's club as it is"
            );
            MatchSetup normal = new World(setup, new Settings()).Setup;
            bool skatersKept = normal
                .Away.Roster.Zip(setup.Away.Roster)
                .All(p =>
                    p.First.Position == PlayingPosition.G || p.First.Traits == p.Second.Traits
                );
            bool goaliesDown = normal
                .Away.Roster.Zip(setup.Away.Roster)
                .Where(p => p.First.Position == PlayingPosition.G)
                .All(p => p.First.Traits.GoalieReflexes < p.Second.Traits.GoalieReflexes);
            Check(
                ReferenceEquals(normal.Home, setup.Home) && skatersKept && goaliesDown,
                "a normal match plays the skaters as the data says and takes the AI goalies down"
            );

            MatchSetup versus = setup with
            {
                Inputs =
                [
                    new InputSlot(InputDevice.KeyboardMouse, Side.Home),
                    new InputSlot(InputDevice.Gamepad1, Side.Away),
                ],
            };
            MatchSetup allAi = setup with
            {
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            Check(
                ReferenceEquals(
                    new World(versus, new Settings { Difficulty = App.Core.Difficulty.Easy }).Setup,
                    versus
                )
                    && ReferenceEquals(
                        new World(
                            allAi,
                            new Settings { Difficulty = App.Core.Difficulty.Easy }
                        ).Setup,
                        allAi
                    ),
                "versus and all-AI matches are never scaled"
            );
        }

        private const int Games = 10;

        /// <summary>Gets the ten games' results, shared by the class's tests.</summary>
        [ClassDataSource<Played<DifficultyGame>>(Shared = SharedType.PerClass)]
        public required Played<DifficultyGame> TenGamesPlayed { get; init; }

        /// <summary>Gets the games, 0 to 9.</summary>
        public static IEnumerable<int> GameNumbers()
        {
            return Enumerable.Range(0, Games);
        }

        /// <summary>One full match, hard against easy, ends swapped each game; <see cref="TenGames"/> judges them.</summary>
        [Test]
        [ExecutionPriority(Priority.Critical)]
        [MethodDataSource(nameof(GameNumbers))]
        public void PlayGame(int g)
        {
            const int seed = 1;
            Settings settings = new Settings() with { Difficulty = App.Core.Difficulty.Normal };
            Side hardSide = g % 2 == 0 ? Side.Home : Side.Away;
            Side easySide = hardSide == Side.Home ? Side.Away : Side.Home;
            MatchSetup setup = WorldBuilder.DefaultSetup with
            {
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            setup = DifficultyScale.Apply(setup, easySide, App.Core.Difficulty.Easy);
            setup = DifficultyScale.Apply(setup, hardSide, App.Core.Difficulty.Hard);
            World world = new WorldBuilder().Setup(setup).Settings(settings).Build();
            Play(world, Systems(world, seed + g), 3 * 60 * 60);
            TenGamesPlayed.Add(g, new DifficultyGame(hardSide, easySide, world));
        }

        /// <summary>Ten full matches, hard against easy, ends swapped each game.</summary>
        [Test]
        [DependsOn(nameof(PlayGame), [typeof(int)])]
        public void TenGames()
        {
            int hardWins = 0,
                hardGoals = 0,
                easyGoals = 0,
                hardShots = 0,
                easyShots = 0;
            float hardPass = 0,
                easyPass = 0;
            DifficultyGame[] played = TenGamesPlayed.All(Games);
            for (int g = 0; g < Games; g++)
            {
                (Side hardSide, Side easySide, World world) = played[g];
                int h = world.Match.Score[(int)hardSide],
                    e = world.Match.Score[(int)easySide];
                if (h > e)
                {
                    hardWins++;
                }

                hardGoals += h;
                easyGoals += e;
                hardShots += world.Match.Shots[(int)hardSide];
                easyShots += world.Match.Shots[(int)easySide];
                hardPass += DecisionState.Tally(world, hardSide).Completion;
                easyPass += DecisionState.Tally(world, easySide).Completion;
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      game {g + 1}: hard ({hardSide.ToString().ToLowerInvariant()}) {h}-{e} easy, shots on goal {world.Match.Shots[(int)hardSide]}-{world.Match.Shots[(int)easySide]}{(world.Match.Phase == MatchPhase.Final ? "" : " (not finished)")}"
                    )
                );
            }
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      hard won {hardWins} of {Games}, goals {hardGoals}-{easyGoals}, shots on goal {hardShots}-{easyShots}, passes completed {hardPass / Games:P0} against {easyPass / Games:P0}"
                )
            );
            Check(
                hardWins >= Games * 0.8f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"hard beats easy clearly: {hardWins} wins in {Games} games"
                )
            );
            Check(
                hardGoals >= easyGoals * 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"hard scores at least twice as many goals as easy ({hardGoals} against {easyGoals})"
                )
            );
            Check(
                hardShots > easyShots * 1.3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"hard outshoots easy ({hardShots} against {easyShots})"
                )
            );
        }

        /// <summary>What one of the ten games came to.</summary>
        public sealed record DifficultyGame(Side Hard, Side Easy, World World);

        private static void Play(World world, ISystem[] systems, float seconds)
        {
            int steps = (int)(seconds / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
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

        // the same all-AI system set as aimatchtest
        private static ISystem[] Systems(World world, int seed)
        {
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(seed)),
                new DefenceSystem(Seeds.Dice(seed)),
                new SpecialTeamsSystem(Seeds.Dice(seed)),
                new DecisionSystem(Seeds.Dice(seed)),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(seed)),
                new PuckHandlingSystem(Seeds.Dice(seed)),
                new GoalieSystem { Dice = Seeds.Dice(seed) },
                new PuckPhysics(),
                new MatchRules(),
                new OvertimeRules(),
            ];
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return systems;
        }
    }
}
