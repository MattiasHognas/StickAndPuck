using System.Globalization;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.AI;

/// <summary>
/// SAP_SCENE=tacticstest: headless checks for W22d, printed one per line; exits 0 when all pass.
/// Checks every club file carries tactics, that the league plays more than one way, and that a
/// misspelt tactic fails the load. Then plays two clubs against each other, one with an
/// aggressive 2-1-2 forecheck that dumps the puck in and one with a 1-3-1 trap that carries it,
/// a period each way round, and checks they play visibly differently: the forecheckers stand
/// deep in the other zone while the trap waits back in the neutral zone as the puck comes out, and the dump-and-chase
/// club dumps where the other carries (<see cref="Compare"/>, also run by aimatchtest).
/// SAP_SEED picks the seed (default 1).
/// </summary>
public sealed class TacticsSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "tacticstest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        Data(clubs);
        int seed = int.TryParse(Environment.GetEnvironmentVariable("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : 1;
        var world = Compare(clubs, settings, seed, Check);
        Console.WriteLine(failures == 0 ? "TACTICSTEST: all checks passed" : $"TACTICSTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return world;
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    /// <summary>The club files: every club has tactics, the league is not all one style, and a typo fails the load.</summary>
    void Data(IReadOnlyList<ClubData> clubs)
    {
        string[] raw = Directory.Exists(Assets.ClubFolder) ? Directory.GetFiles(Assets.ClubFolder, "*.json") : [];
        bool all = raw.Length > 0 && raw.All(f => File.ReadAllText(f).Contains("\"tactics\"", StringComparison.Ordinal));
        Check(all, $"every club file sets its tactics ({raw.Length} files)");
        int styles = clubs.Select(c => c.Tactics.Forecheck).Distinct().Count();
        int entries = clubs.Select(c => c.Tactics.Entry).Distinct().Count();
        Console.WriteLine("      " + string.Join(", ", clubs.Select(c => $"{c.ShortName} {c.Tactics.Forecheck}/{c.Tactics.Entry}/{c.Tactics.PowerPlay}")));
        Check(styles == 3 && entries == 2, "the league plays every forecheck and both zone entries");

        Check(Fails("{\"forecheck\": \"zone\"}"), "an unknown tactic fails the club load");
        Check(Fails("{\"forcheck\": \"trap\"}"), "a misspelt tactics key fails the club load");
        string path = Path.Combine(Path.GetTempPath(), $"sap_tactics_{Environment.ProcessId}.json");
        File.WriteAllText(path, "{\"id\": \"t\", \"tactics\": {\"forecheck\": \"trap\", \"entry\": \"dumpAndChase\", \"powerPlay\": \"oneThreeOne\"}}");
        var club = Clubs.Load(path);
        File.Delete(path);
        Check(club.Tactics == new ClubTactics { Forecheck = Forecheck.Trap, Entry = ZoneEntry.DumpAndChase, PowerPlay = PowerPlayShape.OneThreeOne }, "a club file's tactics load as written");
    }

    static bool Fails(string tactics)
    {
        string path = Path.Combine(Path.GetTempPath(), $"sap_tactics_bad_{Environment.ProcessId}.json");
        File.WriteAllText(path, $"{{\"id\": \"t\", \"tactics\": {tactics}}}");
        try
        {
            Clubs.Load(path);
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

    /// <summary>
    /// Plays a 2-1-2 dump-and-chase club against a 1-3-1 trap club that carries the puck in, one
    /// period each way round, and checks they play differently. Shared with aimatchtest.
    /// </summary>
    /// <param name="clubs">Every club; the first two are used.</param>
    /// <param name="settings">Player options.</param>
    /// <param name="seed">Randomness seed.</param>
    /// <param name="check">Reports one check.</param>
    /// <returns>The last match played.</returns>
    public static World Compare(IReadOnlyList<ClubData> clubs, Settings settings, int seed, Action<bool, string> check)
    {
        var press = new ClubTactics { Forecheck = Forecheck.Aggressive, Entry = ZoneEntry.DumpAndChase };
        var trap = new ClubTactics { Forecheck = Forecheck.Trap, Entry = ZoneEntry.Carry };
        var p = new TacticsTally();
        var t = new TacticsTally();
        World world = null!;
        int goals = 0, pressShots = 0, trapShots = 0;
        foreach (var pressSide in new[] { Side.Home, Side.Away })
        {
            var trapSide = pressSide == Side.Home ? Side.Away : Side.Home;
            world = Make(clubs, settings, pressSide == Side.Home ? press : trap, pressSide == Side.Home ? trap : press);
            Play(world, Systems(world, seed + (int)pressSide), 20 * 60 + 300);
            Add(p, Tactics.Tally(world, pressSide));
            Add(t, Tactics.Tally(world, trapSide));
            goals += world.Match.Score[0] + world.Match.Score[1];
            pressShots += world.Match.Shots[(int)pressSide];
            trapShots += world.Match.Shots[(int)trapSide];
            Console.WriteLine($"      period with 2-1-2 at {pressSide}: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}");
        }
        Console.WriteLine($"      2-1-2 dump and chase: {p.Forecheckers:F1} skaters deep and {p.Trappers:F1} in the neutral zone on the other side's breakout, {p.Back:F1} back as it leaves the zone, {p.Dumps} dumps ({p.Recovered} got back), {p.Carried}/{p.Entries} entries carried ({p.CarryShare:P0})");
        Console.WriteLine($"      1-3-1 trap, carry in:  {t.Forecheckers:F1} skaters deep and {t.Trappers:F1} in the neutral zone on the other side's breakout, {t.Back:F1} back as it leaves the zone, {t.Dumps} dumps, {t.Carried}/{t.Entries} entries carried ({t.CarryShare:P0})");
        Console.WriteLine($"      two periods: {goals} goals, shots on goal {pressShots} for the 2-1-2 club and {trapShots} for the trap club");
        check(p.Forecheckers > 2.5f && p.Forecheckers > t.Forecheckers + 1.5f, "the 2-1-2 club forechecks deep in the other zone, the trap club does not");
        check(t.Back > 2.5f && t.Back > p.Back + 1.5f && t.Trappers > p.Trappers + 1f, "the trap club waits back in the neutral zone as the other side breaks out, the 2-1-2 club is caught deep");
        check(p.Dumps >= 10 && t.Dumps == 0, "the dump-and-chase club dumps the puck in, the carrying club never does");
        check(p.CarryShare + 0.2f < t.CarryShare, "the carrying club carries clearly more of its zone entries over the line");
        check(goals <= 14 && pressShots >= 6 && trapShots >= 6, "the tactics still play hockey: shots for both clubs, a normal number of goals");
        return world;
    }

    static void Add(TacticsTally into, TacticsTally from)
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

    internal static World Make(IReadOnlyList<ClubData> clubs, Settings settings, ClubTactics home, ClubTactics away)
    {
        var setup = Scenes.DefaultSetup(clubs);
        setup = setup with
        {
            Home = setup.Home with { Tactics = home },
            Away = setup.Away with { Tactics = away },
            PeriodSeconds = 20 * 60,
            Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)],
        };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world, log: false);
        return world;
    }

    /// <summary>Runs one period, or until a time limit.</summary>
    static void Play(World world, ISystem[] systems, float seconds)
    {
        int steps = (int)(seconds / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final && world.Match.Period < 2; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step;
            world.Tick++;
        }
    }

    internal static ISystem[] Systems(World world, int seed)
    {
#pragma warning disable S2245 // fixed seeds so the checks repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed)), new TacticsSystem(new Random(seed)),
            new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)), new PuckHandlingSystem(new Random(seed)),
            new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return systems;
    }
}

/// <summary>
/// SAP_SCENE=tactics: an all-AI exhibition with each club's tactics and its dumps and entries on
/// a line at the bottom. SAP_HOME and SAP_AWAY pick the clubs (default Frölunda's 2-1-2 against
/// Luleå's trap); SAP_TACTICS_HOME and SAP_TACTICS_AWAY override a club's forecheck
/// (balanced, aggressive, trap). SAP_PERIOD=seconds shortens the periods.
/// </summary>
public sealed class TacticsScene : IScene
{
    /// <inheritdoc/>
    public string Name => "tactics";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        ClubData Pick(string variable, string fallback, ClubData other)
        {
            string id = Environment.GetEnvironmentVariable(variable) is { Length: > 0 } v ? v : fallback;
            return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) ?? clubs.FirstOrDefault(c => c != other) ?? clubs[0];
        }
        var setup = Scenes.DefaultSetup(clubs);
        var home = Pick("SAP_HOME", "frolunda", setup.Away);
        var away = Pick("SAP_AWAY", "lulea", home);
        home = home with { Tactics = Override(home.Tactics, "SAP_TACTICS_HOME") };
        away = away with { Tactics = Override(away.Tactics, "SAP_TACTICS_AWAY") };
        float period = float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : 20 * 60;
        var world = new World(setup with { Home = home, Away = away, PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world);
        TacticsSystem.ShowLine = true;
        return world;
    }

    static ClubTactics Override(ClubTactics tactics, string variable)
    {
        return Enum.TryParse<Forecheck>(Environment.GetEnvironmentVariable(variable), ignoreCase: true, out var f) ? tactics with { Forecheck = f } : tactics;
    }
}
