using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.AI;

/// <summary>
/// SAP_SCENE=difficultytest: headless checks for W29's difficulty, one line per check; exits 0 when all
/// pass. Checks the trait scaling (easy lowers, hard raises, normal and versus untouched, only the
/// club no human plays), then plays ten full all-AI matches of the same two clubs, one scaled hard and
/// one easy, swapping ends every game so home ice evens out: the "done when" is that hard clearly
/// beats easy. SAP_DIFFICULTY_GAMES changes the number of games, SAP_SEED the first seed, and SAP_DIFFICULTY_HIGH and
/// SAP_DIFFICULTY_LOW (easy, normal, hard) the two levels, to compare neighbouring levels when tuning.
/// </summary>
public sealed class DifficultySelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "difficultytest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var normal = settings with { Difficulty = Difficulty.Normal };
        Scaling(clubs);
        TenGames(clubs, normal);
        Console.WriteLine(failures == 0 ? "DIFFICULTYTEST: all checks passed" : $"DIFFICULTYTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return new World(Scenes.DefaultSetup(clubs), normal);
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    void Scaling(IReadOnlyList<ClubData> clubs)
    {
        var t = new Traits { HockeyIq = 60, ShotAccuracy = 95, TopSpeed = 4, GoalieReflexes = 70 };
        var easy = DifficultyScale.Scale(t, Difficulty.Easy);
        var hard = DifficultyScale.Scale(t, Difficulty.Hard);
        Check(easy.HockeyIq < t.HockeyIq && easy.ShotAccuracy < t.ShotAccuracy && easy.GoalieReflexes < t.GoalieReflexes,
            $"easy lowers the traits (hockey IQ {t.HockeyIq} -> {easy.HockeyIq}, reflexes {t.GoalieReflexes} -> {easy.GoalieReflexes})");
        Check(hard.HockeyIq > t.HockeyIq && hard.ShotAccuracy > t.ShotAccuracy && hard.GoalieReflexes > t.GoalieReflexes,
            $"hard raises the traits (hockey IQ {t.HockeyIq} -> {hard.HockeyIq}, reflexes {t.GoalieReflexes} -> {hard.GoalieReflexes})");
        Check(hard.ShotAccuracy == 100 && easy.TopSpeed == 0, "the scaled traits stay within 0 to 100");
        Check(DifficultyScale.Scale(t, Difficulty.Normal) == t, "normal keeps the traits from the data files");

        var setup = Scenes.DefaultSetup(clubs);     // the keyboard on the home club
        Check(DifficultyScale.AiSide(setup) == Side.Away, "the difficulty applies to the club no human plays");
        var scaled = new World(setup, new Settings { Difficulty = Difficulty.Hard }).Setup;
        bool homeKept = ReferenceEquals(scaled.Home, setup.Home);
        bool awayRaised = scaled.Away.Roster.Zip(setup.Away.Roster).All(p => p.First.Traits.HockeyIq >= p.Second.Traits.HockeyIq)
            && scaled.Away.Roster.Average(p => p.Traits.HockeyIq) > setup.Away.Roster.Average(p => p.Traits.HockeyIq) + 10;
        Check(homeKept && awayRaised, "a hard match raises the AI club's whole squad and leaves the human's club as it is");
        Check(ReferenceEquals(new World(setup, new Settings()).Setup, setup), "a normal match plays the clubs as the data says");

        var versus = setup with { Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), new InputSlot(InputDevice.Gamepad1, Side.Away)] };
        var allAi = setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        Check(ReferenceEquals(new World(versus, new Settings { Difficulty = Difficulty.Easy }).Setup, versus)
            && ReferenceEquals(new World(allAi, new Settings { Difficulty = Difficulty.Easy }).Setup, allAi), "versus and all-AI matches are never scaled");
    }

    /// <summary>Ten full matches, hard against easy, ends swapped each game.</summary>
    void TenGames(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        int games = int.TryParse(Environment.GetEnvironmentVariable("SAP_DIFFICULTY_GAMES"), System.Globalization.CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 10;
        int seed = int.TryParse(Environment.GetEnvironmentVariable("SAP_SEED"), System.Globalization.CultureInfo.InvariantCulture, out int s) ? s : 1;
        int hardWins = 0, hardGoals = 0, easyGoals = 0, hardShots = 0, easyShots = 0;
        float hardPass = 0, easyPass = 0;
        for (int g = 0; g < games; g++)
        {
            var hardSide = g % 2 == 0 ? Side.Home : Side.Away;
            var easySide = hardSide == Side.Home ? Side.Away : Side.Home;
            var setup = Scenes.DefaultSetup(clubs) with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
            setup = DifficultyScale.Apply(setup, easySide, Enum.Parse<Difficulty>(Environment.GetEnvironmentVariable("SAP_DIFFICULTY_LOW") ?? "Easy", ignoreCase: true));
            setup = DifficultyScale.Apply(setup, hardSide, Enum.Parse<Difficulty>(Environment.GetEnvironmentVariable("SAP_DIFFICULTY_HIGH") ?? "Hard", ignoreCase: true));
            var world = new World(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world, log: false);
            Play(world, Systems(world, seed + g), 3 * 60 * 60);
            int h = world.Match.Score[(int)hardSide], e = world.Match.Score[(int)easySide];
            if (h > e) hardWins++;
            hardGoals += h; easyGoals += e;
            hardShots += world.Match.Shots[(int)hardSide]; easyShots += world.Match.Shots[(int)easySide];
            hardPass += Decisions.Tally(world, hardSide).Completion; easyPass += Decisions.Tally(world, easySide).Completion;
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
