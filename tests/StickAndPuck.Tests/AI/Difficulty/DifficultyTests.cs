using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using Level = StickAndPuck.App.Core.Difficulty;
using PassTally = StickAndPuck.App.AI.Decisions;

namespace StickAndPuck.Tests;

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
        var t = new Traits { HockeyIq = 60, ShotAccuracy = 95, TopSpeed = 4, GoalieReflexes = 70 };
        var easy = DifficultyScale.Scale(t, Level.Easy);
        var hard = DifficultyScale.Scale(t, Level.Hard);
        Check(easy.HockeyIq < t.HockeyIq && easy.ShotAccuracy < t.ShotAccuracy && easy.GoalieReflexes < t.GoalieReflexes,
            $"easy lowers the traits (hockey IQ {t.HockeyIq} -> {easy.HockeyIq}, reflexes {t.GoalieReflexes} -> {easy.GoalieReflexes})");
        Check(hard.HockeyIq > t.HockeyIq && hard.ShotAccuracy > t.ShotAccuracy && hard.GoalieReflexes > t.GoalieReflexes,
            $"hard raises the traits (hockey IQ {t.HockeyIq} -> {hard.HockeyIq}, reflexes {t.GoalieReflexes} -> {hard.GoalieReflexes})");
        Check(hard.ShotAccuracy == 100 && easy.TopSpeed == 0, "the scaled traits stay within 0 to 100");
        Check(DifficultyScale.Scale(t, Level.Normal) == t, "normal keeps the traits from the data files");

        var setup = WorldBuilder.DefaultSetup;     // the keyboard on the home club
        Check(DifficultyScale.AiSide(setup) == SideOrNone.Away, "the difficulty applies to the club no human plays");
        var scaled = new World(setup, new Settings { Difficulty = Level.Hard }).Setup;
        bool homeKept = ReferenceEquals(scaled.Home, setup.Home);
        bool awayRaised = scaled.Away.Roster.Zip(setup.Away.Roster).All(p => p.First.Traits.HockeyIq >= p.Second.Traits.HockeyIq)
            && scaled.Away.Roster.Average(p => p.Traits.HockeyIq) > setup.Away.Roster.Average(p => p.Traits.HockeyIq) + 10;
        Check(homeKept && awayRaised, "a hard match raises the AI club's whole squad and leaves the human's club as it is");
        Check(ReferenceEquals(new World(setup, new Settings()).Setup, setup), "a normal match plays the clubs as the data says");

        var versus = setup with { Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), new InputSlot(InputDevice.Gamepad1, Side.Away)] };
        var allAi = setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        Check(ReferenceEquals(new World(versus, new Settings { Difficulty = Level.Easy }).Setup, versus)
            && ReferenceEquals(new World(allAi, new Settings { Difficulty = Level.Easy }).Setup, allAi), "versus and all-AI matches are never scaled");
    }

    /// <summary>Ten full matches, hard against easy, ends swapped each game.</summary>
    [Test]
    public void TenGames()
    {
        const int games = 10;
        const int seed = 1;
        var settings = new Settings() with { Difficulty = Level.Normal };
        int hardWins = 0, hardGoals = 0, easyGoals = 0, hardShots = 0, easyShots = 0;
        float hardPass = 0, easyPass = 0;
        var played = Matches.Play(games, g =>
        {
            var hardSide = g % 2 == 0 ? Side.Home : Side.Away;
            var easySide = hardSide == Side.Home ? Side.Away : Side.Home;
            var setup = WorldBuilder.DefaultSetup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
            setup = DifficultyScale.Apply(setup, easySide, Level.Easy);
            setup = DifficultyScale.Apply(setup, hardSide, Level.Hard);
            var world = new WorldBuilder().Setup(setup).Settings(settings).Build();
            Play(world, Systems(world, seed + g), 3 * 60 * 60);
            return (hardSide, easySide, world);
        });
        for (int g = 0; g < games; g++)
        {
            var (hardSide, easySide, world) = played[g];
            int h = world.Match.Score[(int)hardSide], e = world.Match.Score[(int)easySide];
            if (h > e) hardWins++;
            hardGoals += h; easyGoals += e;
            hardShots += world.Match.Shots[(int)hardSide]; easyShots += world.Match.Shots[(int)easySide];
            hardPass += PassTally.Tally(world, hardSide).Completion; easyPass += PassTally.Tally(world, easySide).Completion;
            Console.WriteLine($"      game {g + 1}: hard ({hardSide.ToString().ToLowerInvariant()}) {h}-{e} easy, shots on goal {world.Match.Shots[(int)hardSide]}-{world.Match.Shots[(int)easySide]}{(world.Match.Phase == MatchPhase.Final ? "" : " (not finished)")}");
        }
        Console.WriteLine($"      hard won {hardWins} of {games}, goals {hardGoals}-{easyGoals}, shots on goal {hardShots}-{easyShots}, passes completed {hardPass / games:P0} against {easyPass / games:P0}");
        Check(hardWins >= games * 0.8f, $"hard beats easy clearly: {hardWins} wins in {games} games");
        Check(hardGoals >= easyGoals * 2, $"hard scores at least twice as many goals as easy ({hardGoals} against {easyGoals})");
        Check(hardShots > easyShots * 1.3f, $"hard outshoots easy ({hardShots} against {easyShots})");
    }

    static void Play(World world, ISystem[] systems, float seconds)
    {
        int steps = (int)(seconds / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }
    }

    // the same all-AI system set as aimatchtest
    static ISystem[] Systems(World world, int seed)
    {
#pragma warning disable S2245 // fixed seeds so the games repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return systems;
    }
}
