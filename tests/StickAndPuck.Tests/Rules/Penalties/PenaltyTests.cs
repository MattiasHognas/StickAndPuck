using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests;

// inside the namespace, so these win over the test namespaces of the same names (this one, and Tests.Referee)
using StickAndPuck.App.Referee;
using Penalties = StickAndPuck.App.Rules.Penalties;

/// <summary>
/// Scripted calls under the match rules, the bench and the referee, then a full AI match. Covers
/// calls happen, the penalised club plays short, time is served correctly,
/// and that a penalty in 3 on 3 overtime gives 4 on 3, the goalie is pulled on a delayed call
/// and when trailing late; that a penalty running at 60:00 continues in overtime, a third penalty
/// is served after the first expires, coincidental minors stay 5 on 5 under SHL and play 4 on 4
/// under NHL; that a major with a game misconduct ejects the skater and refills the lines, a
/// power play goal ends half a double minor and never a major, a misconduct keeps the skater out
/// while a teammate serves, Arcade calls only minors, and each new infraction is called and stays
/// silent when its switch is off. In period 1 home defends -x. Formerly SAP_SCENE=penaltytest.
/// </summary>
public sealed class PenaltyTests : CheckedTest
{
    const float Period = 600;

    static T Must<T>(Maybe<T> m)
        where T : notnull => m.TryGet(out var v) ? v : throw new InvalidOperationException("expected a value");

    /// <summary>A match in play with 10:00 periods, in the given period and under the given rules (none for the default).</summary>
    static World Make(int period = 1, Maybe<RuleSet> rules = default)
    {
        var builder = new WorldBuilder().Setup(s => s with { PeriodSeconds = Period });
        if (rules.TryGet(out var r)) builder.Rules(r);
        return builder
            .Then(w =>
            {
                if (period > 1) MatchFlow.StartPeriod(w, period, Period);
                Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            })
            .Build();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>Physics, the match rules, the bench, penalties and the referee: everything but the players moving themselves.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new LinesSystem(), new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new OvertimeRules(), new PenaltyRules(), new RefereeSystem()]);
    }

    static List<MatchEvent> Run(World world, Func<World, bool> until, float limit = 10) => Run(world, until, _ => { }, limit);

    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World> each, float limit = 10)
    {
        var systems = Systems(world);
        var events = new List<MatchEvent>();
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each(world);
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
    static PenaltyState Penalise(World w, Player p, string infraction = Infraction.Tripping, Severity severity = Severity.Minor)
    {
        Penalties.Signal(w, p.Id, infraction, severity);
        w.Puck.Carrier = (w.OnIce(p.Side).FirstOrDefault(o => o.Data.Position == PlayingPosition.C && o != p) ?? w.OnIce(p.Side).First(o => !o.IsGoalie && o != p)).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        return w.Match.Penalties.Last(x => x.PlayerId == p.Id);
    }

    static void ToPlay(World w)
    {
        Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
    }

    static void PublishGoal(World w, Side scorer)
    {
        bool sent = false;
        Run(w, x => sent, x =>
        {
            x.Events.Publish(new GoalScored(x.Time, scorer, x.OnIce(scorer).First(p => !p.IsGoalie).Id, RinkSpec.NetMouth(1), 0));
            sent = true;
        }, 1);
    }

    [Test]
    public void DelayedCall()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var ac = Skater(w, Side.Away, PlayingPosition.C);
        w.Puck.Carrier = ac.Id;
        w.Puck.Position = new Vector3(-10, 0, 3);
        bool signalled = Penalties.Signal(w, lw.Id, Infraction.Tripping);
        Check(signalled && Penalties.Delayed(w).Count == 1 && Referee.Of(w).Call.Is(c => c.PlayerId == lw.Id), "a trip is signalled: the referee's arm goes up");
        Run(w, _ => false, 1);
        Check(w.Match.Phase == MatchPhase.Play && w.Match.Penalties.Count == 0 && lw.OnIce, "play goes on while the fouled club keeps the puck");

        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        var events = Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        var call = events.OfType<PenaltyCalled>().First();
        Check(w.Match.Phase == MatchPhase.Stopped && events.Exists(e => e is WhistleBlown), "the whistle goes when the offending club touches the puck");
        Check(call.Side == Side.Home && call.PlayerId == lw.Id && Math.Abs(call.Seconds - Penalties.MinorSeconds(w)) < 0.01f, "a Penalty event names the club, the skater and the minutes");
        Check(w.Match.Penalties is [{ Infraction: Infraction.Tripping, SecondsLeft: var left }] && Math.Abs(left - Penalties.MinorSeconds(w)) < 0.01f
            && Math.Abs(Penalties.MinorSeconds(w) - (2 * 60 * Period / 1200)) < 0.01f, "a two minute minor, shortened with the period");
        Check(!lw.OnIce && Penalties.InBox(w, lw.Id) && lw.Position.Z > RinkSpec.HalfWidth, "the skater goes to the box off the ice");
        Check(MatchFlow.FaceoffDot(w) is { X: < -20 } dot && Math.Abs(dot.X + 20.7f) < 0.01f, "the faceoff is in the penalised club's end");
        Check(Referee.Of(w).Announced.Is(c => string.Equals(c.Infraction, Infraction.Tripping, StringComparison.Ordinal)) && !Referee.Of(w).Call.HasValue, "the referee calls tripping and lowers the arm");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == SideOrNone.Away, "home kills it four on five, away is on the power play");
    }

    [Test]
    public void ServedInFull()
    {
        var w = Make();
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
        Run(w, x => !Penalties.InBox(x, d.Id), Watch, 120);
        float released = w.Match.Clock;
        Check(Math.Abs(start - released - Penalties.MinorSeconds(w)) < Game.Step * 1.5f, $"served exactly {MatchFlow.ClockText(Penalties.MinorSeconds(w))} of game time ({start - released:0.00} s)");
        Check(!boxedOn && !extra && changes > 3, "the bench changes during the kill but never sends him on or a fifth skater");
        Check(d.OnIce && Skaters(w, Side.Home) == 5 && w.Match.Penalties.Count == 0 && Vector3.Distance(d.Position, Penalties.BoxDoor(Side.Home)) < 1, "he comes back through the box door: five on five");
    }

    [Test]
    public void PowerPlayGoal()
    {
        var w = Make();
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw, Infraction.Slashing);
        ToPlay(w);
        PublishGoal(w, Side.Away);
        Check(w.Match.Penalties.Count == 0 && rw.OnIce, "a power play goal ends the penalty");
    }

    [Test]
    public void ShortHandedGoal()
    {
        var w = Make();
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        Penalise(w, rw);
        ToPlay(w);
        PublishGoal(w, Side.Home);
        Check(w.Match.Penalties.Count == 1 && !rw.OnIce, "a short-handed goal does not end it");
    }

    [Test]
    public void GoalWavesOffDelayedCall()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Hooking);
        PublishGoal(w, Side.Away);
        Check(Penalties.Delayed(w).Count == 0 && w.Match.Penalties.Count == 0 && lw.OnIce, "a goal while the call is delayed waves it off");
    }

    [Test]
    public void TwoAtMost()
    {
        var w = Make(rules: RuleSet.Arcade);
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        Penalise(w, skaters[0]);
        ToPlay(w);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        bool second = Penalties.Signal(w, skaters[1].Id, Infraction.Slashing);
        bool third = Penalties.Signal(w, skaters[2].Id, Infraction.Slashing);
        bool goalie = Penalties.Signal(w, w.OnIce(Side.Away).First(p => p.IsGoalie).Id, Infraction.Slashing);
        Check(second && !third && !goalie, "Arcade: a club is never more than two short, and goalies are not called");
        w.Puck.Carrier = skaters[2].Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Penalties.Serving(w, Side.Home) == 2, "two penalties: three on five");
    }

    [Test]
    public void ThirdWaits()
    {
        var w = Make();
        var skaters = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        var first = Penalise(w, skaters[0]);
        ToPlay(w);
        Run(w, _ => false, 5);
        Penalise(w, skaters[1]);
        ToPlay(w);
        Penalties.Signal(w, skaters[2].Id, Infraction.Slashing);
        w.Puck.Carrier = skaters[3].Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Penalties.Serving(w, Side.Home) == 2 && Penalties.Waiting(w) is [{ PlayerId: var waiting }] && waiting == skaters[2].Id && Penalties.InBox(w, skaters[2].Id) && Skaters(w, Side.Home) == 3,
            "SHL: a third penalty goes to the box and waits, home stays three on five");
        float clock = w.Match.Clock;
        bool steady = true;
        Run(w, x => !x.Match.Penalties.Exists(p => p.PlayerId == first.PlayerId), x => steady &= Skaters(x, Side.Home) == 3, 120);
        var third = w.Match.Penalties.FirstOrNone(p => p.PlayerId == skaters[2].Id);
        Check(steady && third.Is(t => Math.Abs(t.SecondsLeft - Penalties.MinorSeconds(w)) < 0.05f) && Penalties.Waiting(w).Count == 0 && Math.Abs(clock - w.Match.Clock - (Penalties.MinorSeconds(w) - 5)) < 0.05f,
            "the third penalty starts in full when the first expires");
        Check(Penalties.InBox(w, skaters[0].Id) && !skaters[0].OnIce && Skaters(w, Side.Home) == 3 && Penalties.Serving(w, Side.Home) == 2,
            "the skater whose time is up stays in the box, home still three");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(!Penalties.InBox(w, skaters[0].Id) && Skaters(w, Side.Home) == 3, "at the whistle he goes to the bench and home plays on with three");
        Run(w, x => !Penalties.InBox(x, skaters[2].Id), 120);
        Check(skaters[1].OnIce || !Penalties.InBox(w, skaters[1].Id), "the second and third penalties run out in turn");
    }

    [Test]
    [Arguments("SHL")]
    [Arguments("NHL")]
    public void Coincidental(string preset)
    {
        var (w, rules) = preset switch
        {
            "NHL" => (Make(rules: RuleSet.Nhl), RuleSet.Nhl),
            _ => (Make(), RuleSet.Shl),
        };
        bool shl = rules.CoincidentalMinors == CoincidentalMinors.Substitute;
        var home = Skater(w, Side.Home, PlayingPosition.LW);
        var away = Skater(w, Side.Away, PlayingPosition.RW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, home.Id, Infraction.Hooking);
        Penalties.Signal(w, away.Id, Infraction.Slashing);
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        int want = shl ? 5 : 4;
        Check(Penalties.InBox(w, home.Id) && Penalties.InBox(w, away.Id) && Skaters(w, Side.Home) == want && Skaters(w, Side.Away) == want && Penalties.PowerPlay(w) == SideOrNone.None,
            shl ? "SHL: coincidental minors are substituted, 5 on 5" : "NHL: coincidental minors from full strength play 4 on 4");
        bool steady = true;
        Run(w, x => !Penalties.InBox(x, home.Id), x => steady &= Skaters(x, Side.Home) == want && Skaters(x, Side.Away) == want, 120);
        if (shl)
        {
            Check(steady && !home.OnIce && !away.OnIce && !Penalties.InBox(w, away.Id) && w.Match.Penalties.Count == 0 && Skaters(w, Side.Home) == 5,
                "both serve their time, 5 on 5 throughout, and go to the bench");
        }
        else
        {
            Check(steady && home.OnIce && away.OnIce && Skaters(w, Side.Home) == 5 && Skaters(w, Side.Away) == 5, "both come out together: 5 on 5");
        }
    }

    [Test]
    public void CoincidentalOnPowerPlay()
    {
        var w = Make(rules: RuleSet.Nhl);
        Penalise(w, Skater(w, Side.Home, PlayingPosition.C));
        ToPlay(w);
        var home = Skater(w, Side.Home, PlayingPosition.LW);
        var away = Skater(w, Side.Away, PlayingPosition.RW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, home.Id, Infraction.Hooking);
        Penalties.Signal(w, away.Id, Infraction.Slashing);
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.D).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5 && Penalties.PowerPlay(w) == SideOrNone.Away,
            "NHL: coincidental minors during a power play are substituted, still 5 on 4");
    }

    [Test]
    public void FourOnFour()
    {
        var w = Make();
        Penalise(w, Skater(w, Side.Home, PlayingPosition.LW));
        ToPlay(w);
        Penalise(w, Skater(w, Side.Away, PlayingPosition.LW));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4 && Penalties.PowerPlay(w) == SideOrNone.None, "one each: four on four, no power play");
    }

    [Test]
    public void CarriesOverIntermission()
    {
        var w = Make();
        w.Match.Clock = 10;
        var c = Skater(w, Side.Home, PlayingPosition.C);
        Penalise(w, c);
        Run(w, x => x.Match.Period == 2 && x.Match.Phase == MatchPhase.Play, 60);
        var p = Maybe.From<PenaltyState>(w.Match.Penalties.SingleOrDefault());
        Check(p.Is(x => Math.Abs(x.SecondsLeft - (Penalties.MinorSeconds(w) - 10)) < 0.05f) && !c.OnIce && Skaters(w, Side.Home) == 4,
            "a penalty carries over the intermission: still short in period 2 with the rest of the time");
    }

    [Test]
    public void EndsWithRegulation()
    {
        var w = Make(3, RuleSet.Arcade);
        w.Match.Clock = 5;
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        Penalise(w, lw);
        Run(w, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final, 30);
        Run(w, _ => false, 0.1f);
        Check(w.Match.Phase == MatchPhase.Overtime && w.Match.Penalties.Count == 0 && !lw.OnIce, "Arcade: penalties end with regulation (he waits on the bench)");
    }

    [Test]
    public void CarriesIntoOvertime()
    {
        var w = Make(3);
        w.Match.Clock = 5;
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        Penalise(w, lw);
        ToPlay(w);
        Run(w, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final, 30);
        var carried = Maybe.From<PenaltyState>(w.Match.Penalties.SingleOrDefault());
        Check(w.Match.Phase == MatchPhase.Overtime && carried.Is(x => x.PlayerId == lw.Id && Math.Abs(x.SecondsLeft - (Penalties.MinorSeconds(w) - 5)) < 0.05f),
            "SHL: a penalty running at 60:00 carries into overtime with its time left");
        Run(w, x => x.Match.Period == 4 && x.Match.Phase == MatchPhase.Play, 30);
        Check(Penalties.InBox(w, lw.Id) && Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 4 && Penalties.BaseSkaters(w) == 3 && Penalties.PowerPlay(w) == SideOrNone.Away,
            "overtime starts 4 on 3 for the rest of it");
        float clock = w.Match.Clock;
        Run(w, x => !Penalties.InBox(x, lw.Id), 120);
        Check(lw.OnIce && Math.Abs(clock - w.Match.Clock - Must(carried).SecondsLeft) < 0.05f && Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 4,
            "it runs out in overtime and he plays on, 4 on 4 until the whistle");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 3 && Penalties.BaseSkaters(w) == 3, "back to 3 on 3 at the next faceoff");
    }

    [Test]
    public void MajorAndEjection()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var squad = Must(Bench.SquadOf(w, Side.Home));
        var line = Must(squad.UnitOf(lw));
        int lastCount = squad.Lines[^1].Members.Count;
        var major = Penalise(w, lw, Infraction.Boarding, Severity.MajorAndGameMisconduct);
        Check(Penalties.Ejected(w, lw.Id) && Penalties.InBox(w, lw.Id) && !lw.OnIce && Math.Abs(major.SecondsLeft - Penalties.ClubSeconds(w, Severity.Major)) < 0.01f
            && Math.Abs(Penalties.ClubSeconds(w, Severity.Major) - (2.5f * Penalties.MinorSeconds(w))) < 0.01f, "boarding at full force: a five minute major and a game misconduct, the skater is ejected");
        var refilled = squad.Lines.First(u => u.Number == line.Number);
        Check(!squad.UnitOf(lw).HasValue && refilled.Members.Count == line.Members.Count && squad.Lines[^1].Members.Count == lastCount - 1,
            $"the lines refill without him: {refilled.Name} takes a skater from the last line, which plays short ({string.Join(" ", squad.Lines.Select(u => $"{u.Name}:{u.Members.Count}"))})");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Penalties.PowerPlay(w) == SideOrNone.Away, "home kills the major four on five");
        PublishGoal(w, Side.Away);
        ToPlay(w);
        Check(w.Match.Penalties.Exists(p => p.PlayerId == lw.Id) && Skaters(w, Side.Home) == 4, "a power play goal does not end a major");
        bool back = false;
        void Watch(World x)
        {
            back |= lw.OnIce;
            if (x.Tick % 180 == 0) Bench.RequestChange(x, Side.Home);
        }
        Run(w, x => !x.Match.Penalties.Exists(p => p.PlayerId == lw.Id), Watch, 400);
        Check(!w.Match.Penalties.Exists(p => p.PlayerId == lw.Id) && Skaters(w, Side.Home) == 5 && Penalties.InBox(w, lw.Id) && !lw.OnIce,
            "when the five minutes are up a teammate comes out of the box: five on five");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Run(w, _ => false, Watch, 30);
        Check(!back && !lw.OnIce && Skaters(w, Side.Home) == 5, "the ejected skater never comes back on, through every change");
    }

    [Test]
    public void DoubleMinor()
    {
        var w = Make();
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        var p = Penalise(w, rw, Infraction.HighSticking, Severity.DoubleMinor);
        Check(Math.Abs(p.SecondsLeft - (2 * Penalties.MinorSeconds(w))) < 0.01f && Penalties.SeverityOf(w, rw.Id) == Severity.DoubleMinor, "a high stick that draws blood: a double minor, four minutes");
        ToPlay(w);
        PublishGoal(w, Side.Away);
        Check(w.Match.Penalties.Find(x => x.PlayerId == rw.Id) is { } half && Math.Abs(half.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f && !rw.OnIce,
            "a power play goal ends the first half: two minutes left, still in the box");
        ToPlay(w);
        PublishGoal(w, Side.Away);
        Check(!w.Match.Penalties.Exists(x => x.PlayerId == rw.Id) && rw.OnIce, "a second power play goal ends the second half");
    }

    [Test]
    public void MinorAndMisconduct()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        Penalise(w, d, Infraction.Roughing, Severity.MinorAndMisconduct);
        Check(Penalties.Misconducts(w) is [{ SecondsLeft: var sits }] && Math.Abs(sits - (Penalties.MinorSeconds(w) + Penalties.MisconductSeconds(w))) < 0.01f
            && Math.Abs(Penalties.MisconductSeconds(w) - (5 * Penalties.MinorSeconds(w))) < 0.01f, "roughing with a misconduct: two minutes for the club, twelve in the box for him");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4, "home kills the minor four on five");
        Run(w, x => !x.Match.Penalties.Exists(p => p.PlayerId == d.Id), 200);
        Check(Skaters(w, Side.Home) == 5 && !d.OnIce && Penalties.InBox(w, d.Id), "after the minor a teammate comes out, he sits on: five on five");
        Run(w, x => Penalties.Misconducts(x).Count == 0, 600);
        Check(Penalties.Misconducts(w).Count == 0 && Penalties.InBox(w, d.Id), "the misconduct runs out on its own clock and he waits for the whistle");
        MatchFlow.Whistle(w, RinkSpec.CentreDot);
        ToPlay(w);
        Check(!Penalties.InBox(w, d.Id) && Skaters(w, Side.Home) == 5, "at the whistle he goes to the bench, free to play");
    }

    [Test]
    public void MinorsOnly()
    {
        var w = Make(rules: RuleSet.Arcade);
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var p = Penalise(w, c, Infraction.Tripping, Severity.MajorAndGameMisconduct);
        Check(!Penalties.Ejected(w, c.Id) && Math.Abs(p.SecondsLeft - Penalties.MinorSeconds(w)) < 0.01f && Penalties.Misconducts(w).Count == 0, "Arcade: every call is a two minute minor");
        Check(PenaltyModel.Grade(RuleSet.Arcade, Infraction.Boarding, 3f, 0f) == Severity.Minor && PenaltyModel.Grade(RuleSet.Shl, Infraction.Boarding, 3f, 0f) == Severity.MajorAndGameMisconduct
            && PenaltyModel.Grade(RuleSet.Shl, Infraction.Boarding, 1.6f, 0f) == Severity.Minor && PenaltyModel.Grade(RuleSet.Shl, Infraction.HighSticking, 0, 0.1f) == Severity.DoubleMinor
            && PenaltyModel.Grade(RuleSet.Shl, Infraction.CheckingFromBehind, 1.6f, 0.9f) == Severity.MinorAndMisconduct && PenaltyModel.Grade(RuleSet.Nhl, Infraction.Hooking, 3f, 0f) == Severity.Minor,
            "the referee's grading: majors from very hard boarding, double minors from a bloody high stick, minors in Arcade");
    }

    [Test]
    public void GiveForFights()
    {
        var w = Make();
        var lw = Skater(w, Side.Away, PlayingPosition.LW);
        Penalties.Give(w, lw.Id, "fighting", Severity.MajorAndGameMisconduct);
        Check(Penalties.Ejected(w, lw.Id) && w.Match.Penalties.Exists(p => p.PlayerId == lw.Id && Math.Abs(p.SecondsLeft - Penalties.ClubSeconds(w, Severity.Major)) < 0.01f),
            "W48's hook: Give hands out a major and a game misconduct at once");
    }

    /// <summary>One switchable infraction: how to stage it, the rules with its switch off, and the hit (0 for a chase).</summary>
    sealed record InfractionScene(string Infraction, Func<RuleSet, RuleSet> Off, Action<World, Player, Player> SetUp, float Strength, bool Late = false);

    static readonly InfractionScene[] InfractionScenes =
    [
        new(Infraction.Interference, r => r with { Interference = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, 0), MathF.PI / 2, c, new Vector3(0, 0, -1));
            w.Puck.Position = new Vector3(20, 0, 0);
        }, 1f),
        new(Infraction.Holding, r => r with { Holding = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, 0), MathF.PI / 2, c, new Vector3(-0.6f, 0, 0));
            t.Velocity = new Vector3(2, 0, 0);
            w.Puck.Carrier = t.Id;
        }, 0f),
        new(Infraction.HighSticking, r => r with { HighSticking = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, 0), MathF.PI / 2, c, new Vector3(1, 0, 0));
            w.Puck.Carrier = t.Id;
        }, 1f),
        new(Infraction.CrossChecking, r => r with { CrossChecking = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, 0), MathF.PI / 2, c, new Vector3(1, 0, 0));
            w.Puck.Carrier = t.Id;
        }, CheckingModel.HardHit + 0.3f),
        new(Infraction.Boarding, r => r with { Boarding = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, RinkSpec.HalfWidth - 0.8f), 0, c, new Vector3(0, 0, RinkSpec.HalfWidth - 1.8f));
        }, CheckingModel.HardHit + 0.3f),
        new(Infraction.Roughing, r => r with { Roughing = false }, (w, t, c) =>
        {
            Place(t, new Vector3(0, 0, 0), MathF.PI / 2, c, new Vector3(0, 0, -1));
            MatchFlow.Whistle(w, RinkSpec.CentreDot);
        }, 1f, Late: true),
    ];

    static void Place(Player target, Vector3 at, float heading, Player checker, Vector3 from)
    {
        target.Position = at;
        target.Heading = heading;
        target.Velocity = Vector3.Zero;
        target.Begin(PlayerAction.None);
        checker.Position = from;
        checker.Velocity = Vector3.Zero;
        checker.Begin(PlayerAction.None);
    }

    /// <summary>Stages an infraction with a referee who calls everything he sees, once with its switch on and once off.</summary>
    [Test]
    [Arguments(Infraction.Interference)]
    [Arguments(Infraction.Holding)]
    [Arguments(Infraction.HighSticking)]
    [Arguments(Infraction.CrossChecking)]
    [Arguments(Infraction.Boarding)]
    [Arguments(Infraction.Roughing)]
    public void Infractions(string infraction)
    {
        var scene = InfractionScenes.Single(s => string.Equals(s.Infraction, infraction, StringComparison.Ordinal));
        var on = Stage(Make(), scene);
        var off = Stage(Make(rules: scene.Off(RuleSet.Shl)), scene);
        Check(on.Contains(scene.Infraction, StringComparer.Ordinal) && !off.Contains(scene.Infraction, StringComparer.Ordinal),
            $"{scene.Infraction} is called{(scene.Late ? " after the whistle" : "")} and stays silent when switched off (on: {string.Join(", ", on)}; off: {(off.Count == 0 ? "nothing" : string.Join(", ", off))})");
    }

    static List<string> Stage(World w, InfractionScene scene)
    {
        var target = Skater(w, Side.Home, PlayingPosition.C);
        var checker = Skater(w, Side.Away, PlayingPosition.D);
        w.Puck.Carrier = -1;
        w.Puck.LastTouch = -1;
        w.Puck.Position = Vector3.Zero;
        w.Puck.Velocity = Vector3.Zero;
        scene.SetUp(w, target, checker);
        w.Switches.PenaltyStrictness = 1e6f;
        bool hit = false;
        Run(w, _ => hit && (scene.Strength > 0 || w.Time > 1), x =>
        {
            if (!hit && scene.Strength > 0) x.Events.Publish(new CheckLanded(x.Time, Side.Away, checker.Id, target.Position, scene.Strength));
            hit = true;
        }, scene.Strength > 0 ? 1 : 2);
        w.Switches.PenaltyStrictness = 1f;
        return [.. Penalties.Delayed(w).Select(d => d.Infraction).Concat(w.Match.Penalties.Select(p => p.Infraction))];
    }

    [Test]
    public void NoCallsWhenOff()
    {
        var off = Make();
        off.Settings = off.Settings with { Penalties = false };
        bool a = Penalties.Signal(off, Skater(off, Side.Home, PlayingPosition.C).Id, Infraction.Tripping);
        Check(!a, "no calls with penalties off in the settings");
    }

    /// <summary>A match in its 3 on 3 overtime period, play running.</summary>
    static World Overtime()
    {
        return new WorldBuilder()
            .Setup(s => s with { PeriodSeconds = Period })
            .Then(w =>
            {
                new LinesSystem().Start(w);
                OvertimeRules.ThreeOnThree(w);
                MatchFlow.StartPeriod(w, 4, 300);
                Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            })
            .Build();
    }

    [Test]
    public void OvertimeFourOnThree()
    {
        var w = Overtime();
        Check(Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 3 && Penalties.BaseSkaters(w) == 3, "overtime starts 3 on 3, and that is the full strength now");
        var d = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.D);
        Penalise(w, d, Infraction.Hooking);
        ToPlay(w);
        Check(Penalties.InBox(w, d.Id) && !d.OnIce && Skaters(w, Side.Home) == 3 && Skaters(w, Side.Away) == 4 && Penalties.PowerPlay(w) == SideOrNone.Away,
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

    [Test]
    public void OvertimeFiveOnThree()
    {
        var w = Overtime();
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

    [Test]
    public void OvertimeFiveOnFive()
    {
        var w = Make(4);
        Check(Penalties.BaseSkaters(w) == 5, "an overtime played 5 on 5 (playoffs) keeps five as the full strength");
        Penalise(w, Skater(w, Side.Home, PlayingPosition.LW));
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 4 && Skaters(w, Side.Away) == 5, "a penalty there gives 5 on 4, keyed off the skater count");
    }

    [Test]
    public void DelayedCallPullsGoalie()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        var goalie = w.OnIce(Side.Away).First(p => p.IsGoalie);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        w.Puck.Position = new Vector3(-10, 0, 3);
        Penalties.Signal(w, lw.Id, Infraction.Tripping);
        Run(w, _ => false, 0.5f);
        Check(GoaliePull.IsPulled(w, Side.Away) && !goalie.OnIce && Skaters(w, Side.Away) == 6 && GoaliePull.ExtraAttacker(w, Side.Away).Is(e => e.Data.Position != PlayingPosition.D),
            "a delayed call against home: away pulls its goalie for a sixth skater at once");
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        Run(w, _ => false, 0.1f);
        Check(!GoaliePull.IsPulled(w, Side.Away) && goalie.OnIce && Math.Abs(goalie.Position.X - (RinkSpec.GoalLineX - 1)) < 0.01f,
            "the goalie is back in his crease at the whistle");
        ToPlay(w);
        Check(Skaters(w, Side.Away) == 5 && Skaters(w, Side.Home) == 4, "the extra attacker goes off: 5 on 4 for the power play");
    }

    [Test]
    public void TrailingLatePullsGoalie()
    {
        var w = Make(3);
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        w.Match.Score[1] = 1;
        w.Match.Clock = GoaliePull.PullSeconds(w) + 5;
        w.Puck.Position = new Vector3(5, 0, 0);
        Run(w, _ => false, 1);
        Check(!GoaliePull.IsPulled(w, Side.Home) && goalie.OnIce, "a goal down with more than the window left: the goalie stays in");
        Run(w, x => GoaliePull.IsPulled(x, Side.Home), 10);
        Check(GoaliePull.IsPulled(w, Side.Home) && !goalie.OnIce && Skaters(w, Side.Home) == 6 && w.Match.Clock <= GoaliePull.PullSeconds(w),
            $"inside the last {MatchFlow.ClockText(GoaliePull.PullSeconds(w))} the AI pulls the goalie for a sixth skater");
        PublishGoal(w, Side.Home);
        Run(w, _ => false, 0.1f);
        Check(!GoaliePull.IsPulled(w, Side.Home) && goalie.OnIce, "the equaliser: the goalie is back for the faceoff");
        ToPlay(w);
        Check(Skaters(w, Side.Home) == 5 && !GoaliePull.IsPulled(w, Side.Home), "tied now, so five skaters and no pull");
        w.Match.Score[1] = 4;
        Run(w, _ => false, 1);
        Check(!GoaliePull.IsPulled(w, Side.Home), "three down the AI leaves the goalie in");
    }

    [Test]
    public void HumanPullsGoalie()
    {
        var w = Make(3);
        Skater(w, Side.Home, PlayingPosition.C).Slot = 0;
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        w.Match.Score[1] = 1;
        w.Match.Clock = 10;
        w.Puck.Position = new Vector3(5, 0, 0);
        Run(w, _ => false, 2);
        Check(goalie.OnIce && !GoaliePull.IsPulled(w, Side.Home), "a human's club does not pull by itself when trailing");
        bool pulled = GoaliePull.Pull(w, Side.Home);
        Run(w, _ => false, 0.5f);
        Check(pulled && !goalie.OnIce && Skaters(w, Side.Home) == 6, "the button pulls it: six skaters");
        bool back = GoaliePull.Return(w, Side.Home);
        Check(back && goalie.OnIce && Skaters(w, Side.Home) == 5, "the button again brings him back and the extra attacker off");
    }

    [Test]
    public void HumanHandOff()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        lw.Slot = 0;
        Penalise(w, lw);
        Check(lw.Slot < 0 && w.Controlled(0).TryGet(out var now) && now.Side == Side.Home && now.OnIce && !now.IsGoalie, "a human whose skater is penalised takes a teammate");
    }

    [Test]
    public void ChangedOffDuringDelay()
    {
        var w = Make();
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
        Penalties.Signal(w, lw.Id, Infraction.Tripping);
        // he changes off before the whistle
        var fresh = w.Players.First(p => p.Side == Side.Home && !p.OnIce && !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards);
        fresh.OnIce = true;
        fresh.Position = lw.Position;
        lw.OnIce = false;
        w.Puck.Carrier = Skater(w, Side.Home, PlayingPosition.C).Id;
        Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
        w.Puck.Carrier = -1;
        ToPlay(w);
        Check(Penalties.InBox(w, lw.Id) && !lw.OnIce && Skaters(w, Side.Home) == 4, "a skater who changed off during the delay still serves, and his club still plays short");
    }

    [Test]
    public void Judgement()
    {
        var w = Make();
        var checker = Skater(w, Side.Away, PlayingPosition.D);
        var target = Skater(w, Side.Home, PlayingPosition.C);
        w.Puck.Carrier = -1;
        w.Puck.LastTouch = -1;
        target.Position = new Vector3(0, 0, 0);
        target.Heading = MathF.PI / 2;      // facing +x
        checker.Position = new Vector3(-1, 0, 0);   // behind him
        CheckLanded Hit(float strength) => new CheckLanded(w.Time, Side.Away, checker.Id, target.Position, strength);

        target.Begin(PlayerAction.Fallen);
        Check(PenaltyModel.Judge(w, Hit(1)).TryGet(out var f1) && f1 is { Infraction: Infraction.Tripping, Chance: > 0.05f } && f1.PlayerId == checker.Id, "knocking a skater down from behind is tripping");
        target.Begin(PlayerAction.None);
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)).Is(f => f is { Infraction: Infraction.Hooking }), "a hit from behind on the carrier who stays up is hooking");
        checker.Position = new Vector3(1, 0, 0);    // in front
        w.Puck.Carrier = -1;
        Check(PenaltyModel.Judge(w, Hit(CheckingModel.HardHit + 0.2f)).Is(f => f is { Infraction: Infraction.Slashing, Chance: > 0.08f }), "a hard hit away from the puck is slashing");
        w.Puck.Carrier = target.Id;
        Check(PenaltyModel.Judge(w, Hit(1)).Is(f => f is { Chance: < 0.03f }), "a clean hit on the carrier from the front is rarely called");

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

    /// <summary>A full AI match with a strict referee, for the rules.</summary>
    [Test]
    [ExecutionPriority(Priority.High)]
    public void FullMatch()
    {
        var setup = WorldBuilder.DefaultSetup with { PeriodSeconds = Period };
        var (calls, broken, served) = Match(setup with { PeriodSeconds = 240 }, 8f, 1);
        Check(calls >= 4 && broken == 0 && served, $"AI match with a strict referee: {calls} calls, every club at exactly its strength (penalties, pulled goalie) at every step in play");
    }

    /// <summary>Gets the normal referee's matches, shared by the class's tests.</summary>
    [ClassDataSource<Played<RefereeCalls>>(Shared = SharedType.PerClass)]
    public required Played<RefereeCalls> NormalPlayed { get; init; }

    /// <summary>Gets the normal referee's matches, one a seed.</summary>
    public static IEnumerable<int> NormalMatchNumbers() => Enumerable.Range(0, Seeds.Length);

    /// <summary>One full AI match with a normal referee; <see cref="NormalReferee"/> judges them.</summary>
    [Test]
    [ExecutionPriority(Priority.High)]
    [MethodDataSource(nameof(NormalMatchNumbers))]
    public void PlayNormalMatch(int i) => NormalPlayed.Add(i, Match(WorldBuilder.DefaultSetup with { PeriodSeconds = 600 }, 1f, Seeds[i]));

    /// <summary>Full AI matches with a normal referee, for how often he calls.</summary>
    [Test]
    [DependsOn(nameof(PlayNormalMatch), [typeof(int)])]
    public void NormalReferee()
    {
        int normal = 0, broken2 = 0;
        foreach (var (n, b, _) in NormalPlayed.All(Seeds.Length))
        {
            normal += n;
            broken2 += b;
        }
        float per60 = normal / (30f * Seeds.Length) * 60;
        Check(per60 is >= 4 and <= 16 && broken2 == 0, $"AI match of 3 x 10:00 with a normal referee: {normal / (float)Seeds.Length:0.0} calls a match ({per60:0.0} per 60 minutes, 4 to 16 wanted)");
    }

    /// <summary>What one full match came to: the calls, steps a club was not at its strength, and whether every penalty was served.</summary>
    public sealed record RefereeCalls(int Calls, int Broken, bool Served);

    static readonly int[] Seeds = [2, 3, 4, 5];

    static RefereeCalls Match(MatchSetup setup, float strictness, int seed)
    {
        var w = new WorldBuilder()
            .Setup(setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] })
            .Build();
        w.Switches.PenaltyStrictness = strictness;
#pragma warning disable S2245 // fixed seeds so the checks repeat
        Penalties.Book(w).Rng = new Random(seed);
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new LinesSystem(), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OffsideRule(),
            new IcingRule(), new PenaltyRules(), new RefereeSystem(),
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
            foreach (var e in w.Events.Of<PenaltyCalled>())
            {
                calls++;
                string what = w.Match.Penalties.FindLast(p => p.PlayerId == e.PlayerId)?.Infraction ?? "?";
                kinds[what] = kinds.GetValueOrDefault(what) + 1;
            }
            // penalties end with regulation, leaving the box empty for overtime
            foreach (var side in w.Match.Phase is MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout ? [] : new[] { Side.Home, Side.Away })
            {
                // in play: a call at a stoppage can leave a club a skater short until the faceoff brings it to strength (a third penalty waiting)
                if (w.Match.Phase == MatchPhase.Play && Skaters(w, side) != Penalties.TargetSkaters(w, side))
                    broken++;
                if (w.Match.Penalties.Exists(p => w.Players[p.PlayerId].OnIce)) broken++;
            }
            served &= w.Match.Penalties.TrueForAll(p => p.SecondsLeft <= Penalties.ClubSeconds(w, Severity.Major) + 0.01f);
        }
        Console.WriteLine($"      {string.Join(", ", kinds.Select(k => $"{k.Value} {k.Key}"))}");
        return new RefereeCalls(calls, broken, served);
    }
}
