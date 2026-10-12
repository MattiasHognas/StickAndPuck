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
/// side completes clearly more of its passes: the "done when" of W6b.
/// W22a adds: the jitter's odds, the cycle stations and their path round the net, that AI passes go to
/// moving teammates in open ice, that both clubs cycle and take the puck behind the net, and that a
/// hockey IQ 100 club still fumbles and misses now and then while a 20 club gets lucky. The defence (W22b) runs too,
/// so each side throws 15 to 30 checks; SAP_SCENE=defencetest checks its reads in detail.
/// W22c adds a power play, a man in the box throughout: the power play holds its umbrella or 1-3-1, the kill its box or
/// diamond, and the kill clears the puck (SAP_SCENE=specialteamstest runs the same with other seeds). W22d plays each club's tactics from
/// its file, and adds a 2-1-2 dump-and-chase club against a 1-3-1 trap club that carries the puck in: they must play visibly differently
/// (SAP_SCENE=tacticstest also checks the club files). SAP_SEED picks the full match's seed (default 1).
/// W63 plays full matches with the offside and icing rules on: under SHL over several seeds the AI ices the puck and
/// goes offside at a hockey-like rate, a hockey IQ 30 club clears blind more and holds up at the blue line less than an 85 one,
/// and under Arcade, rules off, never.
/// W68 ends with <see cref="CommandFixtures"/>: who writes an AI skater's command and which write wins.
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
        JitterMaths();
        CycleMaths();
        var match = FullMatch(clubs, settings);
        HockeyIq(clubs, settings);
        Stars(clubs, settings);
        SpecialTeamsSelfTest.PowerPlay(clubs, settings, 1, Check);
        TacticsSelfTest.Compare(clubs, settings, 1, Check);
        LineCalls(clubs, settings);
        CommandFixtures.Run(clubs, settings, Check);
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

    /// <summary>W22a: the jitter's odds come out as the trait says over many rolls.</summary>
    void JitterMaths()
    {
#pragma warning disable S2245 // fixed seed so the check repeats
        var rng = new Random(7);
#pragma warning restore S2245
        (float Fumble, float Lucky) Share(int iq)
        {
            int fumbles = 0, lucky = 0;
            for (int i = 0; i < 20000; i++)
            {
                var outcome = Jitter.Roll(rng, iq).Outcome;
                if (outcome == ReadOutcome.Fumble) fumbles++;
                else if (outcome == ReadOutcome.Lucky) lucky++;
            }
            return (fumbles / 20000f, lucky / 20000f);
        }
        var star = Share(100);
        var poor = Share(10);
        Check(star.Fumble is > 0.02f and < 0.07f && star.Lucky < 1e-6f, $"a hockey IQ 100 skater still fumbles now and then ({star.Fumble:P1} of reads)");
        Check(poor.Fumble > star.Fumble * 2.5f && poor.Lucky > 0.08f, $"a hockey IQ 10 skater fumbles more and sometimes gets lucky ({poor.Fumble:P1} fumbled, {poor.Lucky:P1} lucky)");
        Check(Jitter.Fumbled(100) < 70 && Jitter.Fumbled(100) > 40, $"a star's fumble plays like an average player (hockey IQ {Jitter.Fumbled(100)})");
    }

    /// <summary>W22a: the cycle's stations sit where they should and its path goes round the cage.</summary>
    void CycleMaths()
    {
        bool inside = true, order = true, around = true;
        foreach (int end in new[] { -1, 1 })
        {
            foreach (float cs in new[] { -1f, 1f })
            {
                var slot = Cycle.Spot(Station.Slot, end, cs);
                var corner = Cycle.Spot(Station.Corner, end, cs);
                var behind = Cycle.Spot(Station.BehindNet, end, cs);
                foreach (var spot in new[] { slot, corner, behind }) inside &= Vector2.Distance(spot, TeamShape.Inside(spot, 1.2f)) < 1e-4f && !Cycle.ThroughNet(spot, spot);
                order &= end * behind.X > RinkSpec.GoalLineX + RinkSpec.NetDepth && end * slot.X < RinkSpec.GoalLineX - 5f && corner.Y * cs > 5f;

                // walk from each station to each other one, a metre at a time toward the next waypoint
                foreach (var from in new[] { slot, corner, behind })
                {
                    foreach (var to in new[] { slot, corner, behind })
                    {
                        var at = from;
                        for (int i = 0; i < 80 && Vector2.Distance(at, to) > 0.5f; i++)
                        {
                            var way = Cycle.AroundNet(at, to, end);
                            var next = at + Vector2.Normalize(way - at) * Math.Min(1f, Vector2.Distance(at, way));
                            if (Cycle.ThroughNet(at, next)) around = false;
                            at = next;
                        }
                        if (Vector2.Distance(at, to) > 0.5f) around = false;
                    }
                }
            }
        }

        Check(inside, "every cycle station is on the ice and clear of the cage");
        Check(order, "the stations are behind the net, low in the corner on its side and out in the slot");
        Check(around, "a skater going from station to station goes round the net, never through it");
    }

    /// <summary>A whole regulation match, every skater AI.</summary>
    World FullMatch(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = Make(clubs, settings, 20 * 60, null, null);
        var systems = Systems(world, int.Parse(Environment.GetEnvironmentVariable("SAP_SEED") ?? "1", System.Globalization.CultureInfo.InvariantCulture));
        float still = Play(world, systems, 2 * 60 * 60);
        var h = Decisions.Tally(world, Side.Home);
        var a = Decisions.Tally(world, Side.Away);
        Console.WriteLine($"      match: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}, attempts {h.Shots}-{a.Shots}, passes {h.Completed}/{h.Passes} ({h.Completion:P0}) and {a.Completed}/{a.Passes} ({a.Completion:P0}), checks {h.Checks}-{a.Checks}, longest loose puck {still:F1} s");
        Check(world.Match.Phase == MatchPhase.Final, "three periods play out to the final");
        Check(h.Passes > 80 && a.Passes > 80, "both clubs pass the puck around");
        Check(h.Completion > 0.5f && a.Completion > 0.5f, "most passes reach a teammate");
        Check(world.Match.Shots[0] >= 10 && world.Match.Shots[1] >= 10 && world.Match.Shots[0] <= 70 && world.Match.Shots[1] <= 70, "both clubs get a hockey-like number of shots on goal");
        Check(world.Match.Score[0] + world.Match.Score[1] is >= 1 and <= 10, "goals are scored, about as many as in a real match");
        Check(h.Checks is >= 15 and <= 30 && a.Checks is >= 15 and <= 30, "the AI throws 15 to 30 checks a side, like a real match (W22b)");
        Check(still < 15f, "the AI never leaves a loose puck lying");

        // W22a: passes to moving, open teammates, and a cycle in the attacking zone
        float moving = (float)(h.ToMoving + a.ToMoving) / Math.Max(1, h.Aimed + a.Aimed);
        float open = (float)(h.ToOpen + a.ToOpen) / Math.Max(1, h.Aimed + a.Aimed);
        var hc = Positioning.CycleOf(world, Side.Home);
        var ac = Positioning.CycleOf(world, Side.Away);
        Console.WriteLine($"      passes to a moving teammate {moving:P0}, to one with nobody within 3 m {open:P0}; cycle: {hc.Entries} and {ac.Entries} times, {hc.Turns} and {ac.Turns} turns, {hc.Seconds:F0} and {ac.Seconds:F0} s, puck behind the net {hc.BehindNet:F0} and {ac.BehindNet:F0} s");
        Check(moving > 0.5f, "most AI passes go to a teammate on the move");
        Check(open > 0.5f, "most AI passes go to a teammate in open ice");
        Check(hc.Entries >= 10 && ac.Entries >= 10 && hc.Turns >= 20 && ac.Turns >= 20, "both clubs cycle in the attacking zone, turning the forwards over");
        Check(hc.BehindNet > 5f && ac.BehindNet > 5f, "the cycle takes the puck behind the net");
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

    /// <summary>W22a: hockey IQ 100 against 20 for two periods; the stars still fumble and miss now and then, the weak side gets lucky.</summary>
    void Stars(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = Make(clubs, settings, 15 * 60, 100, 20);
        Play(world, Systems(world, 23), 2 * 15 * 60 + 300, stopAfter: 3);
        var star = Decisions.Tally(world, Side.Home);
        var poor = Decisions.Tally(world, Side.Away);
        float rate = (float)star.StarFumbles / Math.Max(1, star.StarRolls);
        Console.WriteLine($"      hockey IQ 100: {star.StarFumbles} fumbles in {star.StarRolls} reads ({rate:P1}), {star.Completed}/{star.Passes} passes completed; hockey IQ 20: {poor.Lucky} lucky reads in {poor.Rolls}, {poor.Completed}/{poor.Passes} completed");
        Check(star.StarFumbles > 0 && rate < 0.08f, "a hockey IQ 100 club still fumbles a read now and then");
        Check(star.Completed < star.Passes, "a hockey IQ 100 club still misses a pass now and then");
        Check(poor.Lucky > 0, "a hockey IQ 20 club gets lucky now and then");
        Check(star.Completion > poor.Completion, "hockey IQ 100 still passes better than 20");
    }

    /// <summary>W63: icings and offsides over full matches, per preset and per hockey IQ.</summary>
    void LineCalls(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        const int Seeds = 4;
        int icings = 0, offsides = 0, fewest = int.MaxValue, most = 0, clears = 0, holds = 0;
        for (int seed = 31; seed < 31 + Seeds; seed++)
        {
            var (ice, off, world) = Calls(clubs, settings, RuleSet.Shl, seed, null, null);
            int i = ice[0] + ice[1], o = off[0] + off[1];
            var h = Decisions.Tally(world, Side.Home);
            var a = Decisions.Tally(world, Side.Away);
            Console.WriteLine($"      SHL seed {seed}: {i} icings, {o} offsides, {h.Clears + a.Clears} clears out of the own zone, {h.HoldUps + a.HoldUps} hold-ups at the blue line, {world.Match.Score[0]}-{world.Match.Score[1]}");
            icings += i; offsides += o; clears += h.Clears + a.Clears; holds += h.HoldUps + a.HoldUps;
            fewest = Math.Min(fewest, Math.Min(i, o));
            most = Math.Max(most, Math.Max(i, o));
        }
        float iceRate = (float)icings / Seeds, offRate = (float)offsides / Seeds;
        Console.WriteLine($"      SHL per match: {iceRate:F1} icings, {offRate:F1} offsides (a real match has about 4 to 10 of each)");
        Check(iceRate is >= 3f and <= 12f, $"under SHL the AI ices the puck at a hockey-like rate ({iceRate:F1} a match)");
        Check(offRate is >= 3f and <= 12f, $"under SHL the AI goes offside at a hockey-like rate ({offRate:F1} a match)");
        Check(fewest >= 1 && most <= 18, $"every match has some of each and none is full of them ({fewest} to {most})");
        Check(clears > 0 && holds > 0, "the AI clears its own zone under pressure and holds up at the blue line for a teammate offside");

        var (aIce, aOff, arcade) = Calls(clubs, settings, RuleSet.Arcade, 41, null, null);
        int arcadeClears = Decisions.Tally(arcade, Side.Home).Clears + Decisions.Tally(arcade, Side.Away).Clears;
        Console.WriteLine($"      Arcade: {aIce[0] + aIce[1]} icings, {aOff[0] + aOff[1]} offsides, {arcadeClears} clears");
        Check(aIce[0] + aIce[1] == 0 && aOff[0] + aOff[1] == 0 && arcade.Match.Phase == MatchPhase.Final, "under Arcade, rules off, a full match has no icing and no offside");

        // the same clubs with hockey IQ 85 on one side and 30 on the other, both ways round
        int[] smartTotals = new int[4], poorTotals = new int[4];
        foreach (var smart in new[] { Side.Home, Side.Away })
        {
            var (ice, off, world) = Calls(clubs, settings, RuleSet.Shl, smart == Side.Home ? 51 : 52, smart == Side.Home ? 85 : 30, smart == Side.Home ? 30 : 85);
            var poor = smart == Side.Home ? Side.Away : Side.Home;
            Add(smartTotals, ice[(int)smart], off[(int)smart], Decisions.Tally(world, smart));
            Add(poorTotals, ice[(int)poor], off[(int)poor], Decisions.Tally(world, poor));
        }
        Console.WriteLine($"      hockey IQ 85: {smartTotals[0]} icings, {smartTotals[1]} offsides, {smartTotals[2]} clears, {smartTotals[3]} hold-ups; hockey IQ 30: {poorTotals[0]} icings, {poorTotals[1]} offsides, {poorTotals[2]} clears, {poorTotals[3]} hold-ups");
        Check(poorTotals[2] > smartTotals[2] * 1.5f, "a hockey IQ 30 club fires the puck out of its zone blind far more often than an 85 one");
        float smartHolds = (float)smartTotals[3] / Math.Max(1, smartTotals[3] + smartTotals[1]), poorHolds = (float)poorTotals[3] / Math.Max(1, poorTotals[3] + poorTotals[1]);
        Check(smartHolds > poorHolds, $"a hockey IQ 85 club holds up at the line for a teammate offside more often than a 30 one ({smartHolds:P0} vs {poorHolds:P0})");
    }

    static void Add(int[] totals, int icings, int offsides, DecisionTally tally)
    {
        totals[0] += icings;
        totals[1] += offsides;
        totals[2] += tally.Clears;
        totals[3] += tally.HoldUps;
    }

    /// <summary>Plays a full match with the offside and icing rules on; returns each side's icings and offsides.</summary>
    static (int[] Icings, int[] Offsides, World World) Calls(IReadOnlyList<ClubData> clubs, Settings settings, RuleSet rules, int seed, int? smartHome, int? smartAway)
    {
        var world = Make(clubs, settings, 20 * 60, smartHome, smartAway, rules);
        ISystem[] systems = [.. Systems(world, seed), new OffsideRule(), new IcingRule()];
        int[] icings = [0, 0], offsides = [0, 0];
        int steps = (int)(2 * 60 * 60 / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            foreach (var e in world.Events.Current)
            {
                if (e.Side is not { } side) continue;
                if (e.Kind == MatchEventKind.Icing) icings[(int)side]++;
                else if (e.Kind == MatchEventKind.Offside) offsides[(int)side]++;
            }
        }
        return (icings, offsides, world);
    }

    static World Make(IReadOnlyList<ClubData> clubs, Settings settings, float period, int? smartHome, int? smartAway, RuleSet? rules = null)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = period, Rules = rules ?? RuleSet.Shl, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
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
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed)), new TacticsSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return systems;
    }
}
