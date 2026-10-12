using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Goalie;
using StickAndPuck.Lines;
using StickAndPuck.Physics;
using StickAndPuck.Players;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=penaltytest: scripted calls under the match rules, the bench and the referee, then a
/// full AI match, no window; prints one line per check and exits with 0 when all pass, 1 otherwise.
/// Covers the W14 "done when": calls happen, the penalised club plays short, time is served correctly,
/// and W23's: a penalty in 3 on 3 overtime gives 4 on 3, the goalie is pulled on a delayed call
/// and when trailing late; W46's: a penalty running at 60:00 continues in overtime, a third penalty
/// is served after the first expires, coincidental minors stay 5 on 5 under SHL and play 4 on 4
/// under NHL. In period 1 home defends -x.
/// </summary>
public sealed class PenaltySelfTest : IScene
{
    const float Period = 600;
    int failures;

    /// <inheritdoc/>
    public string Name => "penaltytest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = Period };
        World Make(int period = 1, RuleSet? rules = null)
        {
            var w = new World(rules == null ? setup : setup.WithRules(rules), settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            if (period > 1) MatchFlow.StartPeriod(w, period, Period);
            Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
            return w;
        }

        Penalties.Strictness = 1f;
        DelayedCall(Make());
        ServedInFull(Make());
        PowerPlayGoal(Make());
        ShortHandedGoal(Make());
        GoalWavesOffDelayedCall(Make());
        TwoAtMost(Make(rules: RuleSet.Arcade));
        ThirdWaits(Make());
        FourOnFour(Make());
        Coincidental(Make(), RuleSet.Shl);
        Coincidental(Make(rules: RuleSet.Nhl), RuleSet.Nhl);
        CoincidentalOnPowerPlay(Make(rules: RuleSet.Nhl));
        CarriesOverIntermission(Make());
        EndsWithRegulation(Make(3, RuleSet.Arcade));
        CarriesIntoOvertime(Make(3));
        NoCallsWhenOff(Make());
        HumanHandOff(Make());
        ChangedOffDuringDelay(Make());
        Judgement(Make());
        OvertimeFourOnThree(Overtime(setup, settings));
        OvertimeFiveOnThree(Overtime(setup, settings));
        OvertimeFiveOnFive(Make(4));
        DelayedCallPullsGoalie(Make());
        TrailingLatePullsGoalie(Make(3));
        HumanPullsGoalie(Make(3));
        FullMatch(setup, settings);

        Console.WriteLine(failures == 0 ? "PENALTYTEST: all checks passed" : $"PENALTYTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>Physics, the match rules, the bench, penalties and the referee: everything but the players moving themselves.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new OvertimeRules(), new PenaltyRules(), new Referee.RefereeSystem()]);
    }

    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World>? each = null, float limit = 10)
    {
        var systems = Systems(world);
        var events = new List<MatchEvent>();
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static Player Skater(World w, Side side, PlayingPosition position)
    {
        return w.OnIce(side).First(p => p.Data.Position == position);
    }

    static int Skaters(World w, Side side)
    {
        return w.OnIce(side).Count(p => !p.IsGoalie);
    }

    /// <summary>Calls a penalty on a skater and makes it at once by giving his club the puck.</summary>
    static PenaltyState Penalise(World w, Player p, string infraction = Infraction.Tripping)
    {
        Penalties.Signal(w, p.Id, infraction);
        w.Puck.Carrier = (w.OnIce(p.Side).FirstOrDefault(o => o.Data.Position == PlayingPosition.C && o != p) ?? w.OnIce(p.Side).First(o => !o.IsGoalie && o != p)).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        return w.Match.Penalties.Last(x => x.PlayerId == p.Id);
    }

    static void ToPlay(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
    }

    static void PublishGoal(World w, Side scorer)
    {
        bool sent = false;
        Run(w, x => sent, x =>
        {
            x.Events.Publish(new MatchEvent(MatchEventKind.Goal, x.Time, scorer, -1, RinkSpec.NetMouth(1)));
            sent = true;
        }, 1);
    }

    void DelayedCall(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var ac = Skater(w, Side.Away, PlayingPosition.C);
        w.Puck.Carrier = ac.Id;
        w.Puck.Position = new Vector3(-10, 0, 3);
        bool signalled = Penalties.Signal(w, lw.Id, Infraction.Tripping);
        Check(signalled && Penalties.Delayed(w).Count == 1 && Referee.Referee.Of(w).Call?.PlayerId == lw.Id, "a trip is signalled: the referee's arm goes up");
        Run(w, _ => false, null, 1);
        Check(w.Match.Phase == MatchPhase.Play && w.Match.Penalties.Count == 0 && lw.OnIce, "play goes on while the fouled club keeps the puck");

        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        var events = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        var call = events.Find(e => e.Kind == MatchEventKind.Penalty);
        Check(w.Match.Phase == MatchPhase.Stopped && events.Exists(e => e.Kind == MatchEventKind.Whistle), "the whistle goes when the offending club touches the puck");
        Check(call.Side == Side.Home && call.PlayerId == lw.Id && Math.Abs(call.Strength - Penalties.MinorSeconds(w)) < 0.01f, "a Penalty event names the club, the skater and the minutes");
        Check(w.Match.Penalties is [{ Infraction: Infraction.Tripping, SecondsLeft: var left }] && Math.Abs(left - Penalties.MinorSeconds(w)) < 0.01f
            && Math.Abs(Penalties.MinorSeconds(w) - (2 * 60 * Period / 1200)) < 0.01f, "a two minute minor, shortened with the period");
        Check(!lw.OnIce && Penalties.InBox(w, lw.Id) && lw.Position.Z > RinkSpec.HalfWidth, "the skater goes to the box off the ice");
        Check(MatchFlow.FaceoffDot(w) is { X: < -20 } dot && Math.Abs(dot.X + 20.7f) < 0.01f, "the faceoff is in the penalised club's end");
        Check(string.Equals(Referee.Referee.Of(w).Announced?.Infraction, Infraction.Tripping, StringComparison.Ordinal) && Referee.Referee.Of(w).Call == null, "the referee calls tripping and lowers the arm");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == Side.Away, "home kills it four on five, away is on the power play");
    }

    void ServedInFull(World w)
    {
        var d = Skater(w, Side.Home, PlayingPosition.D);
        Penalise(w, d, Infraction.Hooking);
        ToPlay(w);
        float start = w.Match.Clock;
        bool boxedOn = false, extra = false;
        int changes = 0;
        void Watch(World x)
        {
            if (Penalties.InBox(x, d.Id))
            {
                boxedOn |= d.OnIce;
                extra |= Skaters(x, Side.Home) != 4;
            }
            // ask for a change every few seconds, so the bench changes all through the kill
            if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Home); changes++; }
        }
        Run(w, x => x.Match.Clock <= start - 20, Watch, 30);
        float left = w.Match.Penalties.Single().SecondsLeft;
        Check(Math.Abs(left - (Penalties.MinorSeconds(w) - 20)) < 0.02f, $"the penalty clock runs with the game clock ({MatchFlow.ClockText(left)} left after 0:20)");
        float released = float.NaN;
        Run(w, x => !Penalties.InBox(x, d.Id), Watch, 120);
        released = w.Match.Clock;
        Check(Math.Abs(start - released - Penalties.MinorSeconds(w)) < Game.Step * 1.5f, $"served exactly {MatchFlow.ClockText(Penalties.MinorSeconds(w))} of game time ({start - released:0.00} s)");
        Check(!boxedOn && !extra && changes > 3, "the bench changes during the kill but never sends him on or a fifth skater");
        Check(d.OnIce && Skaters(w, Side.Home) == 5 && w.Match.Penalties.Count == 0 && Vector3.Distance(d.Position, Penalties.BoxDoor(Side.Home)) < 1, "he comes back through the box door: five on five");
    }

    void PowerPlayGoal(World w)
    {
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw, Infraction.Slashing);
        ToPlay(w);
        PublishGoal(w, Side.Away);
        Check(w.Match.Penalties.Count == 0 && rw.OnIce, "a power play goal ends the penalty");
    }

    void ShortHandedGoal(World w)
    {
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw);
        ToPlay(w);
        PublishGoal(w, Side.Home);
        Check(w.Match.Penalties.Count == 1 && !rw.OnIce, "a short-handed goal does not end it");
    }

    void GoalWavesOffDelayedCall(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Hooking);
        PublishGoal(w, Side.Away);
        Check(Penalties.Delayed(w).Count == 0 && w.Match.Penalties.Count == 0 && lw.OnIce, "a goal while the call is delayed waves it off");
    }

    void TwoAtMost(World w)
    {
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        Penalise(w, skaters[0]);
        ToPlay(w);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        bool second = Penalties.Signal(w, skaters[1].Id, Infraction.Slashing);
        bool third = Penalties.Signal(w, skaters[2].Id, Infraction.Slashing);
        bool goalie = Penalties.Signal(w, w.OnIce(Side.Away).First(p => p.IsGoalie).Id, Infraction.Slashing);
        Check(second && !third && !goalie, "Arcade: a club is never more than two short, and goalies are not called");
        w.Puck.Carrier = skaters[2].Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Penalties.Serving(w, Side.Home) == 2, "two penalties: three on five");
    }

    void ThirdWaits(World w)
    {
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        var first = Penalise(w, skaters[0]);
        ToPlay(w);
        Run(w, _ => false, null, 5);
        Penalise(w, skaters[1]);
        ToPlay(w);
        Penalties.Signal(w, skaters[2].Id, Infraction.Slashing);
        w.Puck.Carrier = skaters[3].Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Penalties.Serving(w, Side.Home) == 2 && Penalties.Waiting(w) is [{ PlayerId: var waiting }] && waiting == skaters[2].Id && Penalties.InBox(w, skaters[2].Id) && Skaters(w, Side.Home) == 3,
            "SHL: a third penalty goes to the box and waits, home stays three on five");
        float clock = w.Match.Clock;
        bool steady = true;
        Run(w, x => !x.Match.Penalties.Exists(p => p.PlayerId == first.PlayerId), x => steady &= Skaters(x, Side.Home) == 3, 120);
        var third = w.Match.Penalties.Find(p => p.PlayerId == skaters[2].Id);
        Check(steady && third != null && Math.Abs(third.SecondsLeft - Penalties.MinorSeconds(w)) < 0.05f && Penalties.Waiting(w).Count == 0 && Math.Abs(clock - w.Match.Clock - (Penalties.MinorSeconds(w) - 5)) < 0.05f,
            "the third penalty starts in full when the first expires");
        Check(Penalties.InBox(w, skaters[0].Id) && !skaters[0].OnIce && Skaters(w, Side.Home) == 3 && Penalties.Serving(w, Side.Home) == 2,
            "the skater whose time is up stays in the box, home still three");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(!Penalties.InBox(w, skaters[0].Id) && Skaters(w, Side.Home) == 3, "at the whistle he goes to the bench and home plays on with three");
        Run(w, x => !Penalties.InBox(x, skaters[2].Id), null, 120);
        Check(skaters[1].OnIce || !Penalties.InBox(w, skaters[1].Id), "the second and third penalties run out in turn");
    }

    void Coincidental(World w, RuleSet rules)
    {
        bool shl = rules.CoincidentalMinors == CoincidentalMinors.Substitute;
        var home = Skater(w, Side.Home, PlayingPosition.LW);
        var away = Skater(w, Side.Away, PlayingPosition.RW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, home.Id, Infraction.Hooking);
        Penalties.Signal(w, away.Id, Infraction.Slashing);
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        int want = shl ? 5 : 4;
        Check(Penalties.InBox(w, home.Id) && Penalties.InBox(w, away.Id) && Skaters(w, Side.Home) == want && Skaters(w, Side.Away) == want && Penalties.PowerPlay(w) == null,
            shl ? "SHL: coincidental minors are substituted, 5 on 5" : "NHL: coincidental minors from full strength play 4 on 4");
        bool steady = true;
        Run(w, x => !Penalties.InBox(x, home.Id), x => steady &= Skaters(x, Side.Home) == want && Skaters(x, Side.Away) == want, 120);
        if (shl)
            Check(steady && !home.OnIce && !away.OnIce && !Penalties.InBox(w, away.Id) && w.Match.Penalties.Count == 0 && Skaters(w, Side.Home) == 5,
                "both serve their time, 5 on 5 throughout, and go to the bench");
        else
            Check(steady && home.OnIce && away.OnIce && Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5, "both come out together: 5 on 5");
    }

    void CoincidentalOnPowerPlay(World w)
    {
        Penalise(w, Skater(w, Side.Home, PlayingPosition.C));
        ToPlay(w);
        var home = Skater(w, Side.Home, PlayingPosition.LW);
        var away = Skater(w, Side.Away, PlayingPosition.RW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, home.Id, Infraction.Hooking);
        Penalties.Signal(w, away.Id, Infraction.Slashing);
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.D).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == Side.Away,
            "NHL: coincidental minors during a power play are substituted, still 5 on 4");
    }

    void FourOnFour(World w)
    {
        Penalise(w, Skater(w, Side.Home, PlayingPosition.LW));
        ToPlay(w);
        Penalise(w, Skater(w, Side.Away, PlayingPosition.LW));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4 && Penalties.PowerPlay(w) == null, "one each: four on four, no power play");
    }

    void CarriesOverIntermission(World w)
    {
        w.Match.Clock = 10;
        var c = Skater(w, Side.Home, PlayingPosition.C);
        Penalise(w, c);
        Run(w, x => x.Match.Period == 2 && x.Match.Phase == MatchPhase.Play, null, 60);
        var p = w.Match.Penalties.SingleOrDefault();
        Check(p != null && Math.Abs(p.SecondsLeft - (Penalties.MinorSeconds(w) - 10)) < 0.05f && !c.OnIce && Skaters(w, Side.Home) == 4,
            "a penalty carries over the intermission: still short in period 2 with the rest of the time");
    }

    void EndsWithRegulation(World w)
    {
        w.Match.Clock = 5;
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        Penalise(w, lw);
        Run(w, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final, null, 30);
        Run(w, _ => false, null, 0.1f);
        Check(w.Match.Phase == MatchPhase.Overtime && w.Match.Penalties.Count == 0 && !lw.OnIce, "Arcade: penalties end with regulation (he waits on the bench)");
    }

    void CarriesIntoOvertime(World w)
    {
        w.Match.Clock = 5;
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        Penalise(w, lw);
        ToPlay(w);
        Run(w, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final, null, 30);
        var carried = w.Match.Penalties.SingleOrDefault();
        Check(w.Match.Phase == MatchPhase.Overtime && carried != null && carried.PlayerId == lw.Id && Math.Abs(carried.SecondsLeft - (Penalties.MinorSeconds(w) - 5)) < 0.05f,
            "SHL: a penalty running at 60:00 carries into overtime with its time left");
        Run(w, x => x.Match.Period == 4 && x.Match.Phase == MatchPhase.Play, null, 30);
        Check(Penalties.InBox(w, lw.Id) && Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 4 && Penalties.BaseSkaters(w) == 3 && Penalties.PowerPlay(w) == Side.Away,
            "overtime starts 4 on 3 for the rest of it");
        float clock = w.Match.Clock;
        Run(w, x => !Penalties.InBox(x, lw.Id), null, 120);
        Check(lw.OnIce && Math.Abs(clock - w.Match.Clock - carried!.SecondsLeft) < 0.05f && Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4,
            "it runs out in overtime and he plays on, 4 on 4 until the whistle");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 3 && Penalties.BaseSkaters(w) == 3, "back to 3 on 3 at the next faceoff");
    }

    void NoCallsWhenOff(World off)
    {
        off.Settings = off.Settings with { Penalties = false };
        bool a = Penalties.Signal(off, Skater(off, Side.Home, PlayingPosition.C).Id, Infraction.Tripping);
        Check(!a, "no calls with penalties off in the settings");
    }

    /// <summary>A match in its 3 on 3 overtime period, play running.</summary>
    static World Overtime(MatchSetup setup, Settings settings)
    {
        var w = new World(setup, settings);
        Scenes.AddStarters(w);
        MatchFlow.Begin(w, log: false);
        new LinesSystem().Start(w);
        OvertimeRules.ThreeOnThree(w);
        MatchFlow.StartPeriod(w, 4, 300);
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 15);
        return w;
    }

    void OvertimeFourOnThree(World w)
    {
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 3 && Penalties.BaseSkaters(w) == 3, "overtime starts 3 on 3, and that is the full strength now");
        var d = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.D);
        var penalty = Penalise(w, d, Infraction.Hooking);
        ToPlay(w);
        Check(Penalties.InBox(w, d.Id) && !d.OnIce && Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 4 && Penalties.PowerPlay(w) == Side.Away,
            "a penalty in overtime: home keeps three, away adds a skater, 4 on 3");
        Check(w.OnIce(Side.Away).Count(p => !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards) == 2, "the skater away adds is a forward");
        bool steady = true;
        int changes = 0;
        Run(w, x => !Penalties.InBox(x, d.Id), x =>
        {
            steady &= Skaters(x, Side.Home) == 3 && Skaters(x, Side.Away) == 4;
            if (x.Tick % 180 == 0) { Bench.RequestChange(x, Side.Away); changes++; }
        }, 120);
        Check(steady && changes > 3 && d.OnIce && Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4,
            "4 on 3 through the kill and its line changes; when he comes out it is 4 on 4 until the whistle");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 3 && w.Match.Penalties.Count == 0, "back to 3 on 3 at the next faceoff");
    }

    void OvertimeFiveOnThree(World w)
    {
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        Penalise(w, skaters[0]);
        ToPlay(w);
        Penalise(w, skaters[1]);
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 5 && Penalties.Serving(w, Side.Home) == 2, "two overtime penalties: 5 on 3");
        Penalise(w, w.OnIce(Side.Away).First(p => !p.IsGoalie));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 4, "one back the other way: 4 on 3");
    }

    void OvertimeFiveOnFive(World w)
    {
        Check(Penalties.BaseSkaters(w) == 5, "an overtime played 5 on 5 (playoffs) keeps five as the full strength");
        Penalise(w, Skater(w, Side.Home, PlayingPosition.LW));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5, "a penalty there gives 5 on 4, keyed off the skater count");
    }

    void DelayedCallPullsGoalie(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var goalie = w.OnIce(Side.Away).First(p => p.IsGoalie);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        w.Puck.Position = new Vector3(-10, 0, 3);
        Penalties.Signal(w, lw.Id, Infraction.Tripping);
        Run(w, _ => false, null, 0.5f);
        Check(GoaliePull.IsPulled(w, Side.Away) && !goalie.OnIce && Skaters(w, Side.Away) == 6 && GoaliePull.ExtraAttacker(w, Side.Away) is { Data.Position: not PlayingPosition.D },
            "a delayed call against home: away pulls its goalie for a sixth skater at once");
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        Run(w, _ => false, null, 0.1f);
        Check(!GoaliePull.IsPulled(w, Side.Away) && goalie.OnIce && Math.Abs(goalie.Position.X - (RinkSpec.GoalLineX - 1)) < 0.01f,
            "the goalie is back in his crease at the whistle");
        ToPlay(w);
        Check(Skaters(w, Side.Away) == 5 && Skaters(w, Side.Home) == 4, "the extra attacker goes off: 5 on 4 for the power play");
    }

    void TrailingLatePullsGoalie(World w)
    {
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        w.Match.Score[1] = 1;
        w.Match.Clock = GoaliePull.PullSeconds(w) + 5;
        w.Puck.Position = new Vector3(5, 0, 0);
        Run(w, _ => false, null, 1);
        Check(!GoaliePull.IsPulled(w, Side.Home) && goalie.OnIce, "a goal down with more than the window left: the goalie stays in");
        Run(w, x => GoaliePull.IsPulled(x, Side.Home), null, 10);
        Check(GoaliePull.IsPulled(w, Side.Home) && !goalie.OnIce && Skaters(w, Side.Home) == 6 && w.Match.Clock <= GoaliePull.PullSeconds(w),
            $"inside the last {MatchFlow.ClockText(GoaliePull.PullSeconds(w))} the AI pulls the goalie for a sixth skater");
        PublishGoal(w, Side.Home);
        Run(w, _ => false, null, 0.1f);
        Check(!GoaliePull.IsPulled(w, Side.Home) && goalie.OnIce, "the equaliser: the goalie is back for the faceoff");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 5 && !GoaliePull.IsPulled(w, Side.Home), "tied now, so five skaters and no pull");
        w.Match.Score[1] = 4;
        Run(w, _ => false, null, 1);
        Check(!GoaliePull.IsPulled(w, Side.Home), "three down the AI leaves the goalie in");
    }

    void HumanPullsGoalie(World w)
    {
        Skater(w, Side.Home, PlayingPosition.C).Slot = 0;
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        w.Match.Score[1] = 1;
        w.Match.Clock = 10;
        w.Puck.Position = new Vector3(5, 0, 0);
        Run(w, _ => false, null, 2);
        Check(goalie.OnIce && !GoaliePull.IsPulled(w, Side.Home), "a human's club does not pull by itself when trailing");
        bool pulled = GoaliePull.Pull(w, Side.Home);
        Run(w, _ => false, null, 0.5f);
        Check(pulled && !goalie.OnIce && Skaters(w, Side.Home) == 6, "the button pulls it: six skaters");
        bool back = GoaliePull.Return(w, Side.Home);
        Check(back && goalie.OnIce && Skaters(w, Side.Home) == 5, "the button again brings him back and the extra attacker off");
    }

    void HumanHandOff(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        lw.Slot = 0;
        Penalise(w, lw);
        var now = w.Controlled(0);
        Check(lw.Slot < 0 && now != null && now.Side == Side.Home && now.OnIce && !now.IsGoalie, "a human whose skater is penalised takes a teammate");
    }

    void ChangedOffDuringDelay(World w)
    {
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Tripping);
        // he changes off before the whistle
        var fresh = w.Players.First(p => p.Side == Side.Home && !p.OnIce && !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards);
        fresh.OnIce = true;
        fresh.Position = lw.Position;
        lw.OnIce = false;
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, null, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Penalties.InBox(w, lw.Id) && !lw.OnIce && Skaters(w, Side.Home) == 4, "a skater who changed off during the delay still serves, and his club still plays short");
    }

    void Judgement(World w)
    {
        var checker = Skater(w, Side.Away, PlayingPosition.D);
        var target = Skater(w, Side.Home, PlayingPosition.C);
        w.Puck.Carrier = -1;
        w.Puck.LastTouch = -1;
        target.Position = new Vector3(0, 0, 0);
        target.Heading = MathF.PI / 2;      // facing +x
        checker.Position = new Vector3(-1, 0, 0);   // behind him
        MatchEvent Hit(float strength) => new(MatchEventKind.Check, w.Time, Side.Away, checker.Id, target.Position, strength);

        target.Begin(PlayerAction.Fallen);
        Check(PenaltyModel.Judge(w, Hit(1)) is { Infraction: Infraction.Tripping, Chance: > 0.05f } f1 && f1.PlayerId == checker.Id, "knocking a skater down from behind is tripping");
        target.Begin(PlayerAction.None);
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)) is { Infraction: Infraction.Hooking }, "a hit from behind on the carrier who stays up is hooking");
        checker.Position = new Vector3(1, 0, 0);    // in front
        w.Puck.Carrier = -1;
        Check(PenaltyModel.Judge(w, Hit(CheckingModel.HardHit + 0.2f)) is { Infraction: Infraction.Slashing, Chance: > 0.08f }, "a hard hit away from the puck is slashing");
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)) is { Chance: < 0.03f }, "a clean hit on the carrier from the front is rarely called");

        // a fast carrier with a defender on his back
        target.Velocity = new Vector3(6, 0, 0);
        checker.Position = new Vector3(-1, 0, 0.2f);
        checker.Begin(PlayerAction.None);
        bool behind = PenaltyModel.Chases(w, Game.Step).Any(f => f.PlayerId == checker.Id && string.Equals(f.Infraction, Infraction.Hooking, StringComparison.Ordinal));
        checker.Position = new Vector3(1, 0, 0.2f);
        bool ahead = PenaltyModel.Chases(w, Game.Step).Any(f => f.PlayerId == checker.Id);
        Check(behind && !ahead, "a defender trailing a fast carrier can be called for hooking, one ahead of him cannot");
        w.Puck.Carrier = -1;
        target.Velocity = Vector3.Zero;
    }

    /// <summary>Full AI matches: a strict referee for the rules, then a normal one for how often he calls.</summary>
    void FullMatch(MatchSetup setup, Settings settings)
    {
        var (calls, broken, served) = Match(setup with { PeriodSeconds = 240 }, settings, 8f, 1);
        Check(calls >= 4 && broken == 0 && served, $"AI match with a strict referee: {calls} calls, every club at exactly its strength (penalties, pulled goalie) at every step");
        int normal = 0, broken2 = 0;
        foreach (int seed in Seeds)
        {
            var (n, b, _) = Match(setup with { PeriodSeconds = 600 }, settings, 1f, seed);
            normal += n;
            broken2 += b;
        }
        float per60 = normal / (30f * Seeds.Length) * 60;
        Check(per60 is >= 4 and <= 16 && broken2 == 0, $"AI match of 3 x 10:00 with a normal referee: {normal / (float)Seeds.Length:0.0} calls a match ({per60:0.0} per 60 minutes, 4 to 16 wanted)");
    }

    static readonly int[] Seeds = [2, 3, 4, 5];

    static (int Calls, int Broken, bool Served) Match(MatchSetup setup, Settings settings, float strictness, int seed)
    {
        var w = new World(setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }, settings);
        Scenes.AddStarters(w);
        MatchFlow.Begin(w, log: false);
        Penalties.Strictness = strictness;
#pragma warning disable S2245 // fixed seeds so the checks repeat
        Penalties.Book(w).Rng = new Random(seed);
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new Referee.RefereeSystem(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is PositioningSystem or DecisionSystem or LinesSystem or SkatingSystem or CheckingSystem or PuckHandlingSystem) s.Start(w);
        int calls = 0, broken = 0;
        bool served = true;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < 4000 * 60 && w.Match.Phase is not (MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout); i++)
        {
            w.Events.Clear();
            foreach (var s in systems) s.Update(w, Game.Step);
            w.Time += Game.Step; w.Tick++;
            foreach (var e in w.Events.Of(MatchEventKind.Penalty))
            {
                calls++;
                string what = w.Match.Penalties.FindLast(p => p.PlayerId == e.PlayerId)?.Infraction ?? "?";
                kinds[what] = kinds.GetValueOrDefault(what) + 1;
            }
            // penalties end with regulation, leaving the box empty for overtime
            foreach (var side in w.Match.Phase is MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout ? [] : new[] { Side.Home, Side.Away })
            {
                if (Skaters(w, side) != Penalties.TargetSkaters(w, side))
                    broken++;
                if (w.Match.Penalties.Exists(p => w.Players[p.PlayerId].OnIce)) broken++;
            }
            served &= w.Match.Penalties.TrueForAll(p => p.SecondsLeft <= Penalties.MinorSeconds(w) + 0.01f);
        }
        Penalties.Strictness = 1f;
        Console.WriteLine($"      {string.Join(", ", kinds.Select(k => $"{k.Value} {k.Key}"))}");
        return (calls, broken, served);
    }
}
