using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Benches;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Replays;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The benches' reactions: every match event maps to its reaction, the benches take them a moment
/// apart and fall back to watching, the strongest wins, the staff join in, a long shift takes a
/// helmet off until the next shift, and none of it touches the match.
/// </summary>
public sealed class BenchReactionsTests : CheckedTest
{
    static readonly Vector3 At = Vector3.Zero;

    static World Make() => new WorldBuilder().Then(w => new LinesSystem().Start(w)).Build();

    static (BenchCrowd Crowd, BenchReactions Reactions) Benches(World w)
    {
        var crowd = new BenchCrowd(1);
        crowd.Update(w, Game.Step);
        return (crowd, new BenchReactions(Seeds.Dice(1, "bench", 105)));
    }

    /// <summary>Runs the crowd and the reactions for a while, with <paramref name="events"/> on the first step.</summary>
    static void Run(World w, (BenchCrowd Crowd, BenchReactions Reactions) b, float seconds, IReadOnlyList<MatchEvent> events, bool fight = false)
    {
        for (int i = 0; i < seconds / Game.Step; i++)
        {
            b.Crowd.Update(w, Game.Step);
            b.Reactions.Update(w, b.Crowd, i == 0 ? events : [], fight, Game.Step);
            w.Time += Game.Step;
            w.Tick++;
        }
    }

    static List<BenchFigure> Skaters(BenchCrowd crowd, Side side) => [.. crowd.Shown.Where(f => f.Player.Side == side && !f.Player.IsGoalie)];

    static BenchReaction First(MatchEvent e, Side bench) => BenchCue.For(e, bench) is [var cue, ..] ? cue.Reaction : BenchReaction.None;

    [Test]
    public void EveryEventMapsToItsReaction()
    {
        const Side Home = Side.Home, Away = Side.Away;
        (MatchEvent Event, BenchReaction Home, BenchReaction Away, string What)[] table =
        [
            (new GoalScored(0, Home, 0, At, 20), BenchReaction.Cheer, BenchReaction.Slump, "goal for / against"),
            (new MatchWon(0, Home), BenchReaction.Cheer, BenchReaction.Slump, "match won / lost"),
            (new PenaltyCalled(0, Home, 0, At, 120), BenchReaction.Argue, BenchReaction.FistPump, "penalty against / for"),
            (new FightStarted(0, Home, 0, At), BenchReaction.StandFight, BenchReaction.StandFight, "fight"),
            (new CheckLanded(0, Home, 0, At, 2f), BenchReaction.Bang, BenchReaction.None, "hard check"),
            (new CheckLanded(0, Home, 0, At, 1f), BenchReaction.None, BenchReaction.None, "an ordinary check or a punch"),
            (new ShotSaved(0, Home, 0, At, GoalieTuning.HardShot + 1f), BenchReaction.Bang, BenchReaction.None, "big save"),
            (new ShotSaved(0, Home, 0, At, 10f), BenchReaction.None, BenchReaction.None, "routine save"),
            (new PuckHitPost(0, At, 30), BenchReaction.Ooh, BenchReaction.Ooh, "post"),
            (new OffsideCalled(0, Home, At), BenchReaction.Point, BenchReaction.None, "offside"),
            (new IcingCalled(0, Home, 0, At), BenchReaction.Point, BenchReaction.None, "icing"),
            (new WhistleBlown(0, At), BenchReaction.Chat, BenchReaction.Chat, "stoppage"),
            (new PeriodEnded(0, 1), BenchReaction.HelmetOff, BenchReaction.HelmetOff, "period end"),
            (new PuckDropped(0, At), BenchReaction.None, BenchReaction.None, "faceoff"),
            (new ShotTaken(0, Home, 0, At, 30), BenchReaction.None, BenchReaction.None, "shot"),
            (new PassMade(0, Home, 0, At, 10), BenchReaction.None, BenchReaction.None, "pass"),
            (new PuckHitBoards(0, At, 10), BenchReaction.None, BenchReaction.None, "boards"),
            (new LineChanged(0, Home, 0, At), BenchReaction.None, BenchReaction.None, "line change"),
            (new MatchTied(0), BenchReaction.None, BenchReaction.None, "tie"),
        ];
        foreach (var (e, home, away, what) in table)
        {
            var (h, a) = (First(e, Home), First(e, Away));
            Check(h == home && a == away, $"{what}: home {h}, away {a}");
        }
        Check(BenchCue.For(new WhistleBlown(0, At), Home).Any(c => c.Reaction == BenchReaction.Trainer), "a stoppage also sends the trainer to a player");
        Check(BenchCue.For(new GoalScored(0, Home, 0, At, 20), Home)[0].Count == BenchCue.All && BenchCue.For(new PenaltyCalled(0, Home, 0, At, 120), Home)[0].Count == 3,
            "a goal moves the whole bench, a penalty three of it");
    }

    [Test]
    public void GoalForCheersGoalAgainstSlumps()
    {
        var w = Make();
        var b = Benches(w);
        float t = w.Time;
        Run(w, b, 0.5f, [new GoalScored(t, Side.Home, 0, At, 25)]);
        var home = Skaters(b.Crowd, Side.Home);
        var away = Skaters(b.Crowd, Side.Away);
        Check(home.TrueForAll(f => f.Showing(w.Time) is BenchReaction.Cheer or BenchReaction.Bang && f.Standing), $"all {home.Count} home skaters up cheering or banging the boards");
        Check(home.Exists(f => f.Reaction == BenchReaction.Bang) && home.Count(f => f.Reaction == BenchReaction.Cheer) > home.Count / 2,
            $"most with their arms up ({home.Count(f => f.Reaction == BenchReaction.Cheer)}), some banging ({home.Count(f => f.Reaction == BenchReaction.Bang)})");
        Check(away.TrueForAll(f => f.Showing(w.Time) == BenchReaction.Slump && !f.Standing), $"all {away.Count} away skaters slumped in their seats");
        var starts = home.Concat(away).Select(f => f.ReactFrom).ToList();
        Check(starts.TrueForAll(s => s >= t && s <= t + BenchReactions.MaxDelay) && starts.Distinct().Count() > 10, $"each a moment after the goal, not as one ({starts.Distinct().Count()} start times)");
        Check(b.Reactions.StaffOf(Side.Home, true).Pose == StaffPose.Clap && b.Reactions.StaffOf(Side.Home, false).Pose == StaffPose.Clap, "the home coach and trainer clap");
        Check(b.Reactions.StaffOf(Side.Away, true).Pose == StaffPose.Idle, "the away coach stands with his arms folded");
        Run(w, b, 4.5f, []);
        Check(b.Crowd.Shown.All(f => f.Reaction == BenchReaction.None && !f.Standing), "and after a few seconds everyone sits watching again");
    }

    [Test]
    public void PenaltyArguesAtTheDoorAndPumpsAFist()
    {
        var w = Make();
        var b = Benches(w);
        Run(w, b, 0.5f, [new PenaltyCalled(w.Time, Side.Home, 0, At, 120)]);
        var home = Skaters(b.Crowd, Side.Home);
        var arguing = home.Where(f => f.Reaction == BenchReaction.Argue).ToList();
        float door = BenchLayout.DoorX(Side.Home);
        float farthest = arguing.Max(f => MathF.Abs(f.X - door));
        Check(arguing.Count == 3 && arguing.TrueForAll(f => f.Standing) && home.Except(arguing).All(f => MathF.Abs(f.X - door) >= farthest - 1e-4f),
            $"the {arguing.Count} home skaters nearest the door are up arguing");
        Check(b.Reactions.StaffOf(Side.Home, true).Pose == StaffPose.ArmsUp, "the home coach throws his arms up");
        Check(Skaters(b.Crowd, Side.Away).Count(f => f.Reaction == BenchReaction.FistPump) == 2, "two of the away bench pump a fist");
        Run(w, b, 1f, []);
        Check(Skaters(b.Crowd, Side.Away).TrueForAll(f => f.Reaction == BenchReaction.None) && arguing.TrueForAll(f => f.Reaction == BenchReaction.Argue), "the fist pump is short, the arguing goes on");
    }

    [Test]
    public void FightStandsBothBenchesUntilItEnds()
    {
        var w = Make();
        var b = Benches(w);
        Run(w, b, 0.5f, [new FightStarted(w.Time, Side.Home, 0, At)], fight: true);
        Run(w, b, 8f, [], fight: true);
        var all = Skaters(b.Crowd, Side.Home).Concat(Skaters(b.Crowd, Side.Away)).ToList();
        Check(all.TrueForAll(f => f.Showing(w.Time) == BenchReaction.StandFight && f.Standing), $"all {all.Count} skaters on both benches stand at the boards for as long as it lasts");
        Run(w, b, 0.5f, [new GoalScored(w.Time, Side.Home, 0, At, 25)], fight: true);
        Check(all.TrueForAll(f => f.Reaction == BenchReaction.StandFight), "a stronger reaction it is: a goal does not sit them down");
        Run(w, b, 0.5f, []);
        Check(b.Crowd.Shown.All(f => !f.Standing), "they sit down once it is over");
    }

    [Test]
    public void HardCheckAndPostAndOffside()
    {
        var w = Make();
        var b = Benches(w);
        Run(w, b, 0.5f, [new CheckLanded(w.Time, Side.Away, 0, At, 2.2f), new PuckHitPost(w.Time, At, 30)]);
        var away = Skaters(b.Crowd, Side.Away);
        Check(away.Count(f => f.Reaction == BenchReaction.Bang && f.Standing) == 4, "a hard check: four of the checker's bench bang the boards");
        Check(Skaters(b.Crowd, Side.Home).Count(f => f.Reaction == BenchReaction.Ooh) == 3, "the post: three of the other bench go ooh, the banging ones keep banging");
        Run(w, b, 2f, []);
        Run(w, b, 0.5f, [new OffsideCalled(w.Time, Side.Home, At)]);
        var pointing = Skaters(b.Crowd, Side.Home).Where(f => f.Reaction == BenchReaction.Point).ToList();
        Check(pointing.Count == 1 && pointing[0].Standing, "an offside: one by the door stands and points");
    }

    [Test]
    public void StoppageChatsAndTheTrainerComes()
    {
        var w = Make();
        var b = Benches(w);
        var tired = Skaters(b.Crowd, Side.Home).Last();
        tired.Player.Energy = 0.1f;
        var trainer = b.Reactions.StaffOf(Side.Home, false);
        Run(w, b, 0.2f, [new WhistleBlown(w.Time, At)]);
        var home = Skaters(b.Crowd, Side.Home);
        var talking = home.Where(f => f.Reaction == BenchReaction.Chat).ToList();
        Check(talking.Count == 2 && talking.TrueForAll(f => home.Exists(o => o.Reaction == BenchReaction.Listen && MathF.Abs(o.X - f.X) <= BenchLayout.MaxPitch + 0.06f)),
            $"two pairs of neighbours chat ({talking.Count} talking)");
        Check(trainer.Pose == StaffPose.Walk && tired.Reaction == BenchReaction.Trainer, "the trainer walks to the most tired player");
        Run(w, b, MathF.Abs(tired.X - trainer.Home) / BenchStaff.WalkSpeed + 0.5f, []);
        Check(trainer.Pose == StaffPose.LeanTalk && MathF.Abs(trainer.X - tired.X) < 0.5f, string.Create(CultureInfo.InvariantCulture, $"and leans in to him ({trainer.X - tired.X:0.00} m along)"));
        Check(tired.Showing(w.Time) == BenchReaction.Trainer && MathF.Abs(tired.Heading) > 0.8f, "who turns back to him");
        Run(w, b, 12f, []);
        Check(trainer.Free && trainer.Pose == StaffPose.Idle, "then the trainer walks back to his spot");
    }

    [Test]
    public void CoachInstructsAfterAGoalAgainst()
    {
        var w = Make();
        var b = Benches(w);
        Run(w, b, 4f, [new GoalScored(w.Time, Side.Away, 0, At, 25)]);
        Run(w, b, 0.2f, [new WhistleBlown(w.Time, At)]);
        var coach = b.Reactions.StaffOf(Side.Home, true);
        var player = Skaters(b.Crowd, Side.Home).Where(f => f.Reaction == BenchReaction.Instruct).ToList();
        Check(player.Count == 1 && coach.Pose == StaffPose.Walk, "at the stoppage after a goal against the coach walks to a player");
        Run(w, b, MathF.Abs(player[0].X - coach.Home) / BenchStaff.WalkSpeed + 0.3f, []);
        Check(coach.Pose == StaffPose.LeanTalk, "leans over him");
        Run(w, b, 2f, []);
        Check(coach.Pose == StaffPose.Point, "and points out at the ice");
    }

    [Test]
    public void LongShiftTakesTheHelmetOffUntilTheNextShift()
    {
        var w = Make();
        var b = Benches(w);
        var p = Must(Bench.SquadOf(w, Side.Home)).Lines[0].Members[0];
        p.Energy = 0.2f;
        Run(w, b, 0.1f, []);
        p.OnIce = false;
        Run(w, b, 0.1f, []);
        var f = b.Crowd.Of(p);
        Check(f.Is(x => x.Pose == BenchPose.Return && x.Spent < BenchCue.LongShift), "he comes off spent");
        Run(w, b, 6f, []);
        Check(f.Is(x => x.Bare && x.Reaction == BenchReaction.None), "sits down and takes his helmet off");
        Check(b.Reactions.StaffOf(Side.Home, false).Pose is StaffPose.LeanTalk or StaffPose.Walk, "and the trainer comes to see him");
        var rested = Must(Bench.SquadOf(w, Side.Home)).Lines[1].Members[0];
        Check(b.Crowd.Of(rested).Is(x => !x.Bare), "a skater who has not been on keeps his on");
        p.OnIce = true;
        Run(w, b, 0.1f, []);
        p.OnIce = false;
        p.Energy = 0.9f;
        Run(w, b, 6f, []);
        Check(b.Crowd.Of(p).Is(x => !x.Bare), "back on the ice with it on, and after a short shift he keeps it on");
        Run(w, b, 0.1f, [new PeriodEnded(w.Time, 1)]);
        Run(w, b, 2f, []);
        Check(Skaters(b.Crowd, Side.Home).Count(x => x.Bare) == 1, "at the end of a period one of the bench takes his off");
    }

    [Test]
    public void ReactionsNeverTouchTheMatchAndRepeat()
    {
        string Play()
        {
            var w = Make();
            var b = Benches(w);
            bool same = true;
            MatchEvent[] script =
            [
                new GoalScored(0, Side.Home, 0, At, 25), new PenaltyCalled(0, Side.Away, 0, At, 120), new WhistleBlown(0, At),
                new CheckLanded(0, Side.Home, 0, At, 2f), new FightStarted(0, Side.Away, 0, At), new PeriodEnded(0, 1),
            ];
            var log = new List<string>();
            foreach (var e in script)
            {
                for (int i = 0; i < 3f / Game.Step; i++)
                {
                    b.Crowd.Update(w, Game.Step);
                    ulong before = WorldChecksum.Of(w);
                    b.Reactions.Update(w, b.Crowd, i == 0 ? [e with { Time = w.Time }] : [], e is FightStarted, Game.Step);
                    same &= before == WorldChecksum.Of(w);
                    w.Time += Game.Step;
                    w.Tick++;
                }
                log.Add(string.Join(",", b.Crowd.Shown.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Reaction}{f.ReactFrom:0.000}{f.Bare}"))));
            }
            Check(same, "the world is the same before and after every run of the reactions");
            return string.Join("|", log);
        }
        string first = Play(), second = Play();
        Check(string.Equals(first, second, StringComparison.Ordinal), "the same seed gives the same reactions at the same moments");
    }

    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");
}
