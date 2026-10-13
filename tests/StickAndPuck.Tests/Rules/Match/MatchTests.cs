using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Whole matches with puck physics and the match rules (W7a): a full regulation match plays out
/// with the right score. Formerly SAP_SCENE=matchtest.
/// </summary>
public sealed class MatchTests : CheckedTest
{
    const float Period = 30;

    /// <summary>A match with 30-second periods, the starters on and the match flow begun.</summary>
    static World Make(OvertimeRule rule = OvertimeRule.Shl)
    {
        return new WorldBuilder().Setup(s => s with { PeriodSeconds = Period, Overtime = rule }).Build();
    }

    /// <summary>Runs physics and rules step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World>? each = null, float limit = 600)
    {
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules() };
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

    static List<MatchEvent> RunFor(World world, float seconds, Action<World>? each = null)
    {
        float end = world.Time + seconds;
        return Run(world, w => w.Time >= end - Game.Step / 2, each);
    }

    /// <summary>Fires the puck at the net the given club attacks.</summary>
    static void Shoot(World world, Side shooter, float fromGoalLine = 5, float speed = 25)
    {
        int end = -FaceoffFormation.DefendedEnd(world.Match, shooter);
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.Position = new Vector3(end * (RinkSpec.GoalLineX - fromGoalLine), 0, 0.3f);
        pk.Velocity = new Vector3(end * speed, 0, 0);
        pk.LastTouch = world.OnIce(shooter).First(p => !p.IsGoalie).Id;
    }

    static bool Same(float a, float b)
    {
        return MathF.Abs(a - b) < 1e-4f;
    }

    /// <summary>Plays three periods with a scripted list of goals and checks score, clock, ends, faceoffs and the final.</summary>
    [Test]
    public void FullMatch()
    {
        var w = Make();
        // (period, game seconds left when the shot goes, scorer)
        (int Period, float At, Side Scorer)[] script =
        [
            (1, 25, Side.Home), (1, 10, Side.Home), (2, 20, Side.Away), (3, 15, Side.Home),
        ];
        int next = 0;
        float[] playTime = new float[4];
        var defendsLeft = new Side[4];
        int lastPeriod = 0;
        float lastClock = 0;
        var events = Run(w, x => x.Match.Phase == MatchPhase.Final, x =>
        {
            var m = x.Match;
            // a step the clock ran in counts one step of play time
            if (m.Period == lastPeriod && m.Clock < lastClock) playTime[m.Period] += Game.Step;
            (lastPeriod, lastClock) = (m.Period, m.Clock);
            if (m.Phase == MatchPhase.Play) defendsLeft[m.Period] = m.DefendsLeft;
            if (next < script.Length && m.Phase == MatchPhase.Play && m.Period == script[next].Period && m.Clock <= script[next].At)
                Shoot(x, script[next++].Scorer);
        });

        var m = w.Match;
        Check(m.Phase == MatchPhase.Final, "a full match reaches the final");
        Check(m.Score[0] == 3 && m.Score[1] == 1, $"the score is 3-1 (got {m.Score[0]}-{m.Score[1]})");
        Check(m.Period == 3 && m.Clock == 0, "it ends in period 3 with the clock at 0:00");
        Check(events.Count(e => e is GoalScored) == 4, "four goals published");
        Check(events.Count(e => e is PeriodEnded) == 3, "three period ends");
        var final = events.OfType<MatchEnded>().ToList();
        Check(final.Count == 1 && final[0] is MatchWon { Winner: Side.Home }, "one MatchEnd, home wins");
        Check(events.Count(e => e is PuckDropped) == 1 + 4 + 2, "faceoffs: opening, after each goal, start of periods 2 and 3");
        Check(Enumerable.Range(1, 3).All(p => MathF.Abs(playTime[p] - Period) < 2 * Game.Step),
            $"each period has {Period} s of running clock ({string.Join(", ", playTime.Skip(1).Select(t => t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))})");
        Check(defendsLeft[1] == Side.Home && defendsLeft[2] == Side.Away && defendsLeft[3] == Side.Home, "clubs swap ends every period");
        Check(m.Shots[0] == 3 && m.Shots[1] == 1, "goals count as shots on goal");
    }

    /// <summary>The intro, then a faceoff at centre with everyone in their own half and still, then the drop.</summary>
    [Test]
    public void OpeningFaceoff()
    {
        var w = Make();
        Check(w.Match.Phase == MatchPhase.Intro && !w.Puck.Live, "the match starts in the intro with a dead puck");
        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, null, 10);
        Check(w.Match.Phase == MatchPhase.Faceoff && w.Puck.Position == RinkSpec.CentreDot, "then a faceoff at centre");
        bool Halves() => w.Players.Where(p => !p.IsGoalie).All(p => FaceoffFormation.DefendedEnd(w.Match, p.Side) * p.Position.X > 0);
        Check(Halves(), "every skater lines up in their own half");
        float clock = w.Match.Clock;
        var events = RunFor(w, 0.5f, x => { foreach (var p in x.Players) p.Position += new Vector3(0.1f, 0, 0); });
        Check(w.Players.All(p => p.Velocity == Vector3.Zero) && Halves() && Same(w.Match.Clock, clock), "players hold their spots and the clock waits");
        Check(events.Count == 0, "nothing is published before the drop");
        events = Run(w, x => x.Match.Phase == MatchPhase.Play, null, 5);
        Check(w.Puck.Live && events.Count(e => e is PuckDropped) == 1, "the puck drops live with one Faceoff event");
    }

    /// <summary>A puck that crosses after the clock runs out does not count.</summary>
    [Test]
    public void GoalAfterTheHorn()
    {
        var w = Make();
        Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Clock < 0.2f);
        Shoot(w, Side.Home, fromGoalLine: 12, speed: 15);
        var events = RunFor(w, 2);
        Check(w.Match.Score[0] == 0 && events.All(e => e is not GoalScored), "a shot still travelling at the horn is no goal");
        Check(w.Match.Phase == MatchPhase.Intermission && events.Count(e => e is PeriodEnded) == 1, "the period ends into the intermission");
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 20);
        Check(w.Match.Period == 2 && w.Match.Clock <= Period, "period 2 starts with a full clock");
    }

    /// <summary>What a tie after regulation leads to under each overtime rule.</summary>
    [Test]
    public void Ties()
    {
        var none = Make(OvertimeRule.None);
        var ev = Run(none, x => x.Match.Phase == MatchPhase.Final);
        Check(ev.Count(e => e is MatchTied) == 1, "no overtime: a 0-0 tie ends the match with no winner");

        var shl = Make(OvertimeRule.Shl);
        ev = Run(shl, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final);
        Check(shl.Match.Phase == MatchPhase.Overtime && ev.All(e => e is not MatchEnded), "SHL: a tie hands over to overtime, no MatchEnd yet");

        var so = Make(OvertimeRule.ShootoutOnly);
        Run(so, x => x.Match.Phase is MatchPhase.Shootout or MatchPhase.Final);
        Check(so.Match.Phase == MatchPhase.Shootout, "shootout only: a tie goes straight to the shootout");

        MatchFlow.StartPeriod(shl, 4, 10);
        Run(shl, x => x.Match.Phase is MatchPhase.Shootout or MatchPhase.Final, null, 30);
        Check(shl.Match.Phase == MatchPhase.Shootout && shl.Match.Period == 4, "a scoreless overtime goes to the shootout");
    }

    /// <summary>A goal in overtime ends the match at once.</summary>
    [Test]
    public void SuddenDeath()
    {
        var w = Make();
        Run(w, x => x.Match.Phase == MatchPhase.Overtime);
        MatchFlow.StartPeriod(w, 4, MatchTuning.OvertimeSeconds);
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 5);
        Check(Same(w.Match.Clock, MatchTuning.OvertimeSeconds) && w.Puck.Position == RinkSpec.CentreDot, "overtime starts at centre with a 5:00 clock");
        Shoot(w, Side.Away);
        var ev = RunFor(w, 1);
        var end = ev.OfType<MatchEnded>().ToList();
        Check(end.Count == 1 && end[0] is MatchWon { Winner: Side.Away } && w.Match.Score[1] == 1, "an overtime goal wins it for the scorer");
        Celebrates(w, Side.Away);
    }

    /// <summary>W40: after the horn of a decided game the winners gather and celebrate, the losers slump or leave, then the final.</summary>
    void Celebrates(World w, Side winner)
    {
        Check(w.Match.Phase == MatchPhase.Stopped && Celebration.Winner(w) == winner.OrNone(), "the horn starts the winners' celebration before the final");
        RunFor(w, MatchTuning.CelebrationSeconds - 1.5f);
        var pile = w.OnIce(winner).Where(p => !p.IsGoalie).Select(p => p.Position).ToList();
        var centre = pile.Aggregate(Vector3.Zero, (a, b) => a + b) / pile.Count;
        Check(pile.All(p => Vector3.Distance(p, centre) < 2.5f), "the winning skaters gather in a pile");
        Check(w.OnIce(winner).Where(p => !p.IsGoalie).All(p => Celebration.Pose(w, p).Is(c => string.Equals(c.Clip, "celebrate", StringComparison.Ordinal))), "every winning skater plays the celebrate clip");
        var losers = w.Players.Where(p => p.OnIce && !p.IsGoalie && p.Side != winner).ToList();
        Check(losers.All(p => Celebration.Pose(w, p).Is(c => c.Clip is "fall" or "block") || (!Celebration.Pose(w, p).HasValue && p.Speed > 1f && p.Velocity.Z < 0)),
            "every losing skater is down on the ice, or trudging to the bench or there with his head down");
        Check(losers.Count(p => Celebration.Pose(w, p).Is(c => string.Equals(c.Clip, "fall", StringComparison.Ordinal))) == Math.Min(2, losers.Count), "the two losers nearest the pile drop to the ice");
        Check(w.Players.Where(p => p.OnIce && p.IsGoalie && p.Side != winner).All(g => Celebration.Slumps(w, g)), "the losing goalie slumps");
        Check(w.Match.Phase == MatchPhase.Stopped && !w.Puck.Live, "the final waits for the celebration");
        var ev = RunFor(w, 2);
        Check(w.Match.Phase == MatchPhase.Final && ev.All(e => e is not MatchEnded), "then the match is final, with no second MatchEnd");
    }

    /// <summary>W7b's entry point: a whistle stops the clock and leads to a faceoff at the given dot.</summary>
    [Test]
    public void WhistleAndDot()
    {
        var w = Make();
        Check(!MatchFlow.Whistle(w, RinkSpec.CentreDot), "no whistle outside play");
        Run(w, x => x.Match.Phase == MatchPhase.Play);
        RunFor(w, 1);
        var dot = MatchFlow.NearestDot(new Vector3(19, 0, 8));
        Check(dot == new Vector3(20.7f, 0, 6.7f), "the nearest dot to the +x corner is the end-zone dot there");
        Check(MatchFlow.Whistle(w, dot) && w.Match.Phase == MatchPhase.Stopped && !w.Puck.Live, "a whistle stops play and kills the puck");
        float clock = w.Match.Clock;
        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, null, 5);
        Check(Same(w.Match.Clock, clock) && w.Puck.Position == dot && MatchFlow.FaceoffDot(w) == dot, "the clock waits and the puck goes to the dot");
        bool inside = w.Players.All(p => MathF.Abs(p.Position.X) <= RinkSpec.GoalLineX && MathF.Abs(p.Position.Z) < RinkSpec.HalfWidth);
        Check(inside, "an end-zone lineup stays inside the rink");
        var centre = w.Players.Where(p => p.Data.Position == PlayingPosition.C && p.OnIce).ToList();
        Check(centre.All(p => Vector3.Distance(p.Position, dot) < 1), "both centres face off at the dot");
        Run(w, x => x.Match.Phase == MatchPhase.Play, null, 5);
        Check(w.Puck.Live && w.Puck.Position == dot, "the puck drops at the dot");
    }

    /// <summary>A goalie holding the puck brings a whistle and an end-zone faceoff.</summary>
    [Test]
    public void FrozenPuck()
    {
        var w = Make();
        Run(w, x => x.Match.Phase == MatchPhase.Play);
        var g = w.OnIce(Side.Away).First(p => p.IsGoalie);
        w.Puck.Carrier = g.Id;
        var ev = RunFor(w, MatchTuning.FreezeSeconds + 0.1f, x => x.Puck.Position = g.Position);
        Check(ev.Any(e => e is WhistleBlown) && w.Match.Phase == MatchPhase.Stopped, "a frozen puck brings a whistle");
        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, null, 5);
        Check(MathF.Abs(MatchFlow.FaceoffDot(w).X) > 20 && w.Puck.Carrier < 0, "the faceoff is at an end-zone dot with the puck loose");
    }

    /// <summary>With the referee dropping the puck, the faceoff waits for the referee.</summary>
    [Test]
    public void RefereeDrop()
    {
        var w = Make();
        MatchFlow.SetRefereeDrops(w, true);
        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, null, 10);
        RunFor(w, MatchTuning.FaceoffSeconds * 3);
        Check(w.Match.Phase == MatchPhase.Faceoff && !w.Puck.Live, "the faceoff waits for the referee");
        Check(MatchFlow.Drop(w) && w.Puck.Live && w.Match.Phase == MatchPhase.Play, "Drop puts the puck in play");
        Check(!MatchFlow.Drop(w), "a second Drop does nothing");
    }
}
