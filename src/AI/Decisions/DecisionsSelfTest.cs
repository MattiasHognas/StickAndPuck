using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.AI;

/// <summary>
/// SAP_SCENE=aimatchtest: headless checks for W6b, printed one per line; exits 0 when all pass.
/// Plays a full three-period match with every skater AI (positioning, decisions, skating, checks,
/// puck handling, goalies, puck physics, match rules) and checks it reaches the final with
/// passes, shots, checks and goals on both sides. Then plays the same clubs with every skater's
/// hockey IQ set to 90 on one side and 20 on the other, both ways round, and checks the smart
/// side completes clearly more of its passes: the "done when" of W6b. SAP_SEED picks the full match's seed (default 1).
/// </summary>
public sealed class DecisionsSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "aimatchtest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        ChoicesMaths();
        var match = FullMatch(clubs, settings);
        HockeyIq(clubs, settings);
        Console.WriteLine(failures == 0 ? "AIMATCHTEST: all checks passed" : $"AIMATCHTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return match;
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    void ChoicesMaths()
    {
        Check(Choices.ShotQuality(1, new Vector2(21f, 0)) > Choices.ShotQuality(1, new Vector2(12f, 0)), "a shot from the slot beats one from the blue line");
        Check(Choices.ShotQuality(1, new Vector2(25f, 9f)) < Choices.ShotQuality(1, new Vector2(20f, 0)), "a shot from a bad angle is worse than one from the slot");
        Check(Choices.ShotQuality(1, new Vector2(28f, 0)) == 0f, "nobody shoots from behind the goal line");
        (Vector2, float)[] inLane = [(new Vector2(5f, 0.3f), 7f)];
        (Vector2, float)[] wide = [(new Vector2(5f, 8f), 7f)];
        float covered = Choices.LaneSuccess(Vector2.Zero, new Vector2(10f, 0), 12f, inLane, 80);
        float open = Choices.LaneSuccess(Vector2.Zero, new Vector2(10f, 0), 12f, wide, 80);
        Check(covered < 0.2f && open > 0.8f, $"a defender in the lane spoils a pass, one far off does not ({covered:F2} vs {open:F2})");
        (Vector2, float)[] near = [(new Vector2(5f, 2.6f), 7f)];
        float smartSees = Choices.LaneSuccess(Vector2.Zero, new Vector2(10f, 0), 12f, near, 90);
        float poorSees = Choices.LaneSuccess(Vector2.Zero, new Vector2(10f, 0), 12f, near, 15);
        Check(poorSees > smartSees + 0.2f, $"a poor passer thinks a lane a defender can close is open ({poorSees:F2} vs {smartSees:F2})");
    }

    /// <summary>A whole regulation match, every skater AI.</summary>
    World FullMatch(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = Make(clubs, settings, 20 * 60, null, null);
        var systems = Systems(world, int.Parse(Environment.GetEnvironmentVariable("SAP_SEED") ?? "1", System.Globalization.CultureInfo.InvariantCulture));
        var still = Play(world, systems, 2 * 60 * 60);
        var h = Decisions.Tally(world, Side.Home);
        var a = Decisions.Tally(world, Side.Away);
        Console.WriteLine($"      match: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}, attempts {h.Shots}-{a.Shots}, passes {h.Completed}/{h.Passes} ({h.Completion:P0}) and {a.Completed}/{a.Passes} ({a.Completion:P0}), checks {h.Checks}-{a.Checks}, longest loose puck {still:F1} s");
        Check(world.Match.Phase == MatchPhase.Final, "three periods play out to the final");
        Check(h.Passes > 80 && a.Passes > 80, "both clubs pass the puck around");
        Check(h.Completion > 0.5f && a.Completion > 0.5f, "most passes reach a teammate");
        Check(world.Match.Shots[0] >= 10 && world.Match.Shots[1] >= 10 && world.Match.Shots[0] <= 70 && world.Match.Shots[1] <= 70, "both clubs get a hockey-like number of shots on goal");
        Check(world.Match.Score[0] + world.Match.Score[1] is >= 1 and <= 10, "goals are scored, about as many as in a real match");
        Check(h.Checks + a.Checks >= 10, "the AI throws checks");
        Check(still < 15f, "the AI never leaves a loose puck lying");
        return world;
    }

    /// <summary>The same clubs with hockey IQ 90 against 20, both ways round, over two periods each.</summary>
    void HockeyIq(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        int smartTotal = 0, smartDone = 0, poorPasses = 0, poorDone = 0, smartLost = 0, poorLost = 0;
        foreach (var smartSide in new[] { Side.Home, Side.Away })
        {
            var poorSide = smartSide == Side.Home ? Side.Away : Side.Home;
            var world = Make(clubs, settings, 15 * 60, smartSide == Side.Home ? 90 : 20, smartSide == Side.Home ? 20 : 90);
            Play(world, Systems(world, smartSide == Side.Home ? 21 : 22), 2 * 15 * 60 + 300, stopAfter: 3);
            var s = Decisions.Tally(world, smartSide);
            var p = Decisions.Tally(world, poorSide);
            smartTotal += s.Passes; smartDone += s.Completed; smartLost += s.Intercepted;
            poorPasses += p.Passes; poorDone += p.Completed; poorLost += p.Intercepted;
        }
        float smart = (float)smartDone / Math.Max(1, smartTotal), poor = (float)poorDone / Math.Max(1, poorPasses);
        Console.WriteLine($"      hockey IQ 90: {smartDone}/{smartTotal} completed ({smart:P0}), {smartLost} picked off; hockey IQ 20: {poorDone}/{poorPasses} completed ({poor:P0}), {poorLost} picked off");
        Check(smart > poor + 0.1f, "the smarter club completes clearly more of its passes");
        float smartLostShare = (float)smartLost / Math.Max(1, smartTotal), poorLostShare = (float)poorLost / Math.Max(1, poorPasses);
        Check(smartLostShare < poorLostShare * 0.75f, "the smarter club's passes are picked off less often");
    }

    static World Make(IReadOnlyList<ClubData> clubs, Settings settings, float period, int? smartHome, int? smartAway)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = period, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        DecisionScenes.SetHockeyIq(world, Side.Home, smartHome);
        DecisionScenes.SetHockeyIq(world, Side.Away, smartAway);
        MatchFlow.Begin(world, log: false);
        return world;
    }

    /// <summary>Runs the match until the final, a time limit, or a number of periods; returns the longest a live puck lay loose and still.</summary>
    static float Play(World world, ISystem[] systems, float seconds, int stopAfter = 99)
    {
        float longest = 0, since = 0;
        var at = world.Puck.Position;
        int steps = (int)(seconds / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            if (world.Match.Period >= stopAfter) break;
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            var pk = world.Puck;
            bool loose = pk.Live && pk.Carrier < 0 && world.Match.Phase == MatchPhase.Play;
            if (!loose || Vector3.Distance(pk.Position, at) > 0.5f) { at = pk.Position; since = world.Time; }
            longest = Math.Max(longest, world.Time - since);
        }
        return longest;
    }

    static ISystem[] Systems(World world, int seed)
    {
#pragma warning disable S2245 // fixed seeds so the checks repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return systems;
    }
}
