using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.Rules.Match
{
    /// <summary>
    /// Whole matches with puck physics and the match rules: a full regulation match plays out
    /// with the right score. Formerly SAP_SCENE=matchtest.
    /// </summary>
    public sealed class MatchTests : CheckedTest
    {
        private const float Period = 30;

        /// <summary>A match with 30-second periods, the starters on and the match flow begun.</summary>
        private static World Make(OvertimeRule rule = OvertimeRule.Shl)
        {
            return new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = Period, Overtime = rule })
                .Build();
        }

        private static List<MatchEvent> Run(World world, Func<World, bool> until, float limit = 600)
        {
            return Run(world, until, _ => { }, limit);
        }

        /// <summary>Runs physics and rules step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            Action<World> each,
            float limit = 600
        )
        {
            ISystem[] systems = [new PuckPhysics(), new MatchRules()];
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                each(world);
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static List<MatchEvent> RunFor(World world, float seconds)
        {
            return RunFor(world, seconds, _ => { });
        }

        private static List<MatchEvent> RunFor(World world, float seconds, Action<World> each)
        {
            float end = world.Time + seconds;
            return Run(world, w => w.Time >= end - (Game.Step / 2), each);
        }

        /// <summary>Fires the puck at the net the given club attacks.</summary>
        private static void Shoot(
            World world,
            Side shooter,
            float fromGoalLine = 5,
            float speed = 25
        )
        {
            int end = -FaceoffFormation.DefendedEnd(world.Match, shooter);
            Puck pk = world.Puck;
            pk.Carrier = -1;
            pk.Position = new Vector3(end * (RinkSpec.GoalLineX - fromGoalLine), 0, 0.3f);
            pk.Velocity = new Vector3(end * speed, 0, 0);
            pk.LastTouch = world.OnIce(shooter).First(p => !p.IsGoalie).Id;
        }

        private static bool Same(float a, float b)
        {
            return MathF.Abs(a - b) < 1e-4f;
        }

        /// <summary>Plays three periods with a scripted list of goals and checks score, clock, ends, faceoffs and the final.</summary>
        [Test]
        [ExecutionPriority(Priority.Critical)]
        public void FullMatch()
        {
            World w = Make();
            // (period, game seconds left when the shot goes, scorer)
            (int Period, float At, Side Scorer)[] script =
            [
                (1, 25, Side.Home),
                (1, 10, Side.Home),
                (2, 20, Side.Away),
                (3, 15, Side.Home),
            ];
            int next = 0;
            float[] playTime = new float[4];
            Side[] defendsLeft = new Side[4];
            int lastPeriod = 0;
            float lastClock = 0;
            List<MatchEvent> events = Run(
                w,
                x => x.Match.Phase == MatchPhase.Final,
                x =>
                {
                    MatchState m = x.Match;
                    // a step the clock ran in counts one step of play time
                    if (m.Period == lastPeriod && m.Clock < lastClock)
                    {
                        playTime[m.Period] += Game.Step;
                    }

                    (lastPeriod, lastClock) = (m.Period, m.Clock);
                    if (m.Phase == MatchPhase.Play)
                    {
                        defendsLeft[m.Period] = m.DefendsLeft;
                    }

                    if (
                        next < script.Length
                        && m.Phase == MatchPhase.Play
                        && m.Period == script[next].Period
                        && m.Clock <= script[next].At
                    )
                    {
                        Shoot(x, script[next++].Scorer);
                    }
                }
            );

            MatchState m = w.Match;
            Check(m.Phase == MatchPhase.Final, "a full match reaches the final");
            Check(
                m.Score[0] == 3 && m.Score[1] == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the score is 3-1 (got {m.Score[0]}-{m.Score[1]})"
                )
            );
            Check(m.Period == 3 && m.Clock == 0, "it ends in period 3 with the clock at 0:00");
            Check(events.Where(e => e is GoalScored).Take(5).Count() == 4, "four goals published");
            Check(events.Where(e => e is PeriodEnded).Take(4).Count() == 3, "three period ends");
            List<MatchEnded> final = [.. events.OfType<MatchEnded>()];
            Check(
                final.Count == 1 && final[0] is MatchWon { Winner: Side.Home },
                "one MatchEnd, home wins"
            );
            Check(
                events.Where(e => e is PuckDropped).Take(8).Count() == 1 + 4 + 2,
                "faceoffs: opening, after each goal, start of periods 2 and 3"
            );
            Check(
                Enumerable.Range(1, 3).All(p => MathF.Abs(playTime[p] - Period) < 2 * Game.Step),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"each period has {Period} s of running clock ({string.Join(", ", playTime.Skip(1).Select(t => t.ToString("0.00", CultureInfo.InvariantCulture)))})"
                )
            );
            Check(
                defendsLeft[1] == Side.Home
                    && defendsLeft[2] == Side.Away
                    && defendsLeft[3] == Side.Home,
                "clubs swap ends every period"
            );
            Check(m.Shots[0] == 3 && m.Shots[1] == 1, "goals count as shots on goal");
        }

        /// <summary>The intro, then a faceoff at centre with everyone in their own half and still, then the drop.</summary>
        [Test]
        public void OpeningFaceoff()
        {
            World w = Make();
            Check(
                w.Match.Phase == MatchPhase.Intro && !w.Puck.Live,
                "the match starts in the intro with a dead puck"
            );
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff, 10);
            Check(
                w.Match.Phase == MatchPhase.Faceoff && w.Puck.Position == RinkSpec.CentreDot,
                "then a faceoff at centre"
            );
            bool Halves()
            {
                return w
                    .Players.Where(p => !p.IsGoalie)
                    .All(p => FaceoffFormation.DefendedEnd(w.Match, p.Side) * p.Position.X > 0);
            }

            Check(Halves(), "every skater lines up in their own half");
            float clock = w.Match.Clock;
            List<MatchEvent> events = RunFor(
                w,
                0.5f,
                x =>
                {
                    foreach (Player p in x.Players)
                    {
                        p.Position += new Vector3(0.1f, 0, 0);
                    }
                }
            );
            Check(
                w.Players.All(p => p.Velocity == Vector3.Zero)
                    && Halves()
                    && Same(w.Match.Clock, clock),
                "players hold their spots and the clock waits"
            );
            Check(events.Count == 0, "nothing is published before the drop");
            events = Run(w, x => x.Match.Phase == MatchPhase.Play, 5);
            Check(
                w.Puck.Live && events.Where(e => e is PuckDropped).Take(2).Count() == 1,
                "the puck drops live with one Faceoff event"
            );
        }

        /// <summary>A puck that crosses after the clock runs out does not count.</summary>
        [Test]
        public void GoalAfterTheHorn()
        {
            World w = Make();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play && x.Match.Clock < 0.2f);
            Shoot(w, Side.Home, fromGoalLine: 12, speed: 15);
            List<MatchEvent> events = RunFor(w, 2);
            Check(
                w.Match.Score[0] == 0 && events.TrueForAll(e => e is not GoalScored),
                "a shot still travelling at the horn is no goal"
            );
            Check(
                w.Match.Phase == MatchPhase.Intermission
                    && events.Where(e => e is PeriodEnded).Take(2).Count() == 1,
                "the period ends into the intermission"
            );
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 20);
            Check(
                w.Match.Period == 2 && w.Match.Clock <= Period,
                "period 2 starts with a full clock"
            );
        }

        /// <summary>What a tie after regulation leads to under each overtime rule.</summary>
        [Test]
        public void Ties()
        {
            World none = Make(OvertimeRule.None);
            List<MatchEvent> ev = Run(none, x => x.Match.Phase == MatchPhase.Final);
            Check(
                ev.Where(e => e is MatchTied).Take(2).Count() == 1,
                "no overtime: a 0-0 tie ends the match with no winner"
            );

            World shl = Make(OvertimeRule.Shl);
            ev = Run(shl, x => x.Match.Phase is MatchPhase.Overtime or MatchPhase.Final);
            Check(
                shl.Match.Phase == MatchPhase.Overtime && ev.TrueForAll(e => e is not MatchEnded),
                "SHL: a tie hands over to overtime, no MatchEnd yet"
            );

            World so = Make(OvertimeRule.ShootoutOnly);
            _ = Run(so, x => x.Match.Phase is MatchPhase.Shootout or MatchPhase.Final);
            Check(
                so.Match.Phase == MatchPhase.Shootout,
                "shootout only: a tie goes straight to the shootout"
            );

            MatchFlow.StartPeriod(shl, 4, 10);
            _ = Run(shl, x => x.Match.Phase is MatchPhase.Shootout or MatchPhase.Final, 30);
            Check(
                shl.Match.Phase == MatchPhase.Shootout && shl.Match.Period == 4,
                "a scoreless overtime goes to the shootout"
            );
        }

        /// <summary>A goal in overtime ends the match at once.</summary>
        [Test]
        public void SuddenDeath()
        {
            World w = Make();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Overtime);
            MatchFlow.StartPeriod(w, 4, MatchTuning.OvertimeSeconds);
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 5);
            Check(
                Same(w.Match.Clock, MatchTuning.OvertimeSeconds)
                    && w.Puck.Position == RinkSpec.CentreDot,
                "overtime starts at centre with a 5:00 clock"
            );
            Shoot(w, Side.Away);
            List<MatchEvent> ev = RunFor(w, 1);
            List<MatchEnded> end = [.. ev.OfType<MatchEnded>()];
            Check(
                end.Count == 1 && end[0] is MatchWon { Winner: Side.Away } && w.Match.Score[1] == 1,
                "an overtime goal wins it for the scorer"
            );
            Celebrates(w, Side.Away);
        }

        /// <summary>After the horn of a decided game the winners gather and celebrate, the losers slump or leave, then the final.</summary>
        private void Celebrates(World w, Side winner)
        {
            Check(
                w.Match.Phase == MatchPhase.Stopped && Celebration.Winner(w) == winner.OrNone(),
                "the horn starts the winners' celebration before the final"
            );
            _ = RunFor(w, MatchTuning.CelebrationSeconds - 1.5f);
            List<Vector3> pile =
            [
                .. w.OnIce(winner).Where(p => !p.IsGoalie).Select(p => p.Position),
            ];
            Vector3 centre = pile.Aggregate(Vector3.Zero, (a, b) => a + b) / pile.Count;
            Check(
                pile.TrueForAll(p => Vector3.Distance(p, centre) < 2.5f),
                "the winning skaters gather in a pile"
            );
            Check(
                w.OnIce(winner)
                    .Where(p => !p.IsGoalie)
                    .All(p =>
                        Celebration
                            .Pose(w, p)
                            .Is(c => string.Equals(c.Clip, "celebrate", StringComparison.Ordinal))
                    ),
                "every winning skater plays the celebrate clip"
            );
            List<Player> losers =
            [
                .. w.Players.Where(p => p.OnIce && !p.IsGoalie && p.Side != winner),
            ];
            Check(
                losers.TrueForAll(p =>
                    Celebration
                        .Pose(w, p)
                        .Is(c =>
                            string.Equals(c.Clip, "fall", StringComparison.Ordinal)
                            || string.Equals(c.Clip, "block", StringComparison.Ordinal)
                        ) || (!Celebration.Pose(w, p).HasValue && p.Speed > 1f && p.Velocity.Z < 0)
                ),
                "every losing skater is down on the ice, or trudging to the bench or there with his head down"
            );
            Check(
                losers
                    .Where(p =>
                        Celebration
                            .Pose(w, p)
                            .Is(c => string.Equals(c.Clip, "fall", StringComparison.Ordinal))
                    )
                    .Take(Math.Min(2, losers.Count) + 1)
                    .Count() == Math.Min(2, losers.Count),
                "the two losers nearest the pile drop to the ice"
            );
            Check(
                w.Players.Where(p => p.OnIce && p.IsGoalie && p.Side != winner)
                    .All(g => Celebration.Slumps(w, g)),
                "the losing goalie slumps"
            );
            Check(
                w.Match.Phase == MatchPhase.Stopped && !w.Puck.Live,
                "the final waits for the celebration"
            );
            List<MatchEvent> ev = RunFor(w, 2);
            Check(
                w.Match.Phase == MatchPhase.Final && ev.TrueForAll(e => e is not MatchEnded),
                "then the match is final, with no second MatchEnd"
            );
        }

        /// <summary>Entry point: a whistle stops the clock and leads to a faceoff at the given dot.</summary>
        [Test]
        public void WhistleAndDot()
        {
            World w = Make();
            Check(!MatchFlow.Whistle(w, RinkSpec.CentreDot), "no whistle outside play");
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play);
            _ = RunFor(w, 1);
            Vector3 dot = MatchFlow.NearestDot(new Vector3(19, 0, 8));
            Check(
                dot == new Vector3(20.7f, 0, 6.7f),
                "the nearest dot to the +x corner is the end-zone dot there"
            );
            Check(
                MatchFlow.Whistle(w, dot) && w.Match.Phase == MatchPhase.Stopped && !w.Puck.Live,
                "a whistle stops play and kills the puck"
            );
            float clock = w.Match.Clock;
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff, 5);
            Check(
                Same(w.Match.Clock, clock)
                    && w.Puck.Position == dot
                    && MatchFlow.FaceoffDot(w) == dot,
                "the clock waits and the puck goes to the dot"
            );
            bool inside = w.Players.All(p =>
                MathF.Abs(p.Position.X) <= RinkSpec.GoalLineX
                && MathF.Abs(p.Position.Z) < RinkSpec.HalfWidth
            );
            Check(inside, "an end-zone lineup stays inside the rink");
            List<Player> centre =
            [
                .. w.Players.Where(p => p.Data.Position == PlayingPosition.C && p.OnIce),
            ];
            Check(
                centre.TrueForAll(p => Vector3.Distance(p.Position, dot) < 1),
                "both centres face off at the dot"
            );
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 5);
            Check(w.Puck.Live && w.Puck.Position == dot, "the puck drops at the dot");
        }

        /// <summary>A goalie holding the puck brings a whistle and an end-zone faceoff.</summary>
        [Test]
        public void FrozenPuck()
        {
            World w = Make();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play);
            Player g = w.OnIce(Side.Away).First(p => p.IsGoalie);
            w.Puck.Carrier = g.Id;
            List<MatchEvent> ev = RunFor(
                w,
                MatchTuning.FreezeSeconds + 0.1f,
                x => x.Puck.Position = g.Position
            );
            Check(
                ev.Exists(e => e is WhistleBlown) && w.Match.Phase == MatchPhase.Stopped,
                "a frozen puck brings a whistle"
            );
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff, 5);
            Check(
                MathF.Abs(MatchFlow.FaceoffDot(w).X) > 20 && w.Puck.Carrier < 0,
                "the faceoff is at an end-zone dot with the puck loose"
            );
        }

        /// <summary>With the referee dropping the puck, the faceoff waits for the referee.</summary>
        [Test]
        public void RefereeDrop()
        {
            World w = Make();
            MatchFlow.SetRefereeDrops(w, referee: true);
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff, 10);
            _ = RunFor(w, MatchTuning.FaceoffSeconds * 3);
            Check(
                w.Match.Phase == MatchPhase.Faceoff && !w.Puck.Live,
                "the faceoff waits for the referee"
            );
            Check(
                MatchFlow.Drop(w) && !w.Puck.Live,
                "Drop opens the draw with the puck still dead"
            );
            Check(!MatchFlow.Drop(w), "a second Drop does nothing");
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 1);
            Check(
                w.Puck.Live && w.Match.Phase == MatchPhase.Play,
                "the draw puts the puck in play"
            );
        }
    }
}
