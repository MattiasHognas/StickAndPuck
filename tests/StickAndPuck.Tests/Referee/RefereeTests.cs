using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Match.Dev;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Referee
{
    /// <summary>
    /// The referee, run with puck physics and the match rules over a whole match with the match demo's
    /// shots, so goals, saves and frozen pucks bring faceoffs all over the ice. Covers that the referee
    /// drops every faceoff and stays out of the play, and that ten faceoffs in a row drop at visibly
    /// different moments within the hold span, the same again for the same seed. Formerly
    /// SAP_SCENE=refereetest.
    /// </summary>
    public sealed class RefereeTests : CheckedTest
    {
        /// <summary>A 25-second match with no overtime, the starters on and the match flow begun.</summary>
        private static World Make()
        {
            return new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = 25, Overtime = OvertimeRule.None })
                .Build();
        }

        /// <summary>Runs physics, rules and the referee step by step until <paramref name="until"/> or the limit.</summary>
        private static void Run(World world, Func<World, bool> until, float limit)
        {
            Run(world, until, limit, _ => { });
        }

        private static void Run(
            World world,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
            Run(world, until, limit, each, new RefereeSystem());
        }

        private static void Run(
            World world,
            Func<World, bool> until,
            float limit,
            Action<World> each,
            RefereeSystem referee
        )
        {
            ISystem[] systems = [new PuckPhysics(), new MatchRules(), referee];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                each(world);
                world.Time += Game.Step;
                world.Tick++;
            }
        }

        private static float Distance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X - b.X, a.Z - b.Z).Length();
        }

        [Test]
        public void WaitsForReferee()
        {
            World w = Make();
            RefereeState r = RefereeState.Of(w);
            r.Position = new Vector3(-20, 0, 11);
            float dropAt = float.NaN;
            Run(
                w,
                x => MatchFlow.State(x).Draw.Dropped,
                20,
                x =>
                {
                    if (x.Events.Of<PuckDropped>().Any())
                    {
                        dropAt = x.Time;
                    }
                }
            );
            Check(MatchFlow.State(w).Draw.Dropped, "the opening faceoff is dropped");
            Check(
                dropAt > MatchTuning.IntroSeconds + MatchTuning.FaceoffSeconds,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the drop waits for the referee to skate from the corner ({dropAt:0.0} s)"
                )
            );
            Check(
                Distance(r.Position, RefereeState.DropSpot(RinkSpec.CentreDot))
                    <= RefereeTuning.Arrived,
                "the referee is at the centre dot when the puck drops"
            );
            Check(r.LateDrops == 0, "no drop needed the safety net");
        }

        [Test]
        public void FarDot()
        {
            World w = Make();
            Run(w, x => x.Match.Phase == MatchPhase.Play, 20);
            Vector3 dot = new(20.7f, 0, 6.7f);
            _ = MatchFlow.Whistle(w, dot);
            RefereeState r = RefereeState.Of(w);
            float start = w.Time,
                dropAt = float.NaN,
                setAt = float.NaN;
            Run(
                w,
                x => MatchFlow.State(x).Draw.Dropped,
                20,
                x =>
                {
                    if (float.IsNaN(setAt) && r.SetTime > 0)
                    {
                        setAt = x.Time;
                    }

                    if (x.Events.Of<PuckDropped>().Any())
                    {
                        dropAt = x.Time;
                    }
                }
            );
            Check(
                MatchFlow.State(w).Draw.Dropped && Distance(w.Puck.Position, dot) < 0.5f,
                "a whistle in the far end is followed by a drop at that dot"
            );
            Check(
                Distance(r.Position, RefereeState.DropSpot(dot)) <= RefereeTuning.Arrived,
                "the referee skated the length of the rink to that dot"
            );

            // Timed to when the referee is set at the dot, so the dice roll for the hold (0.5 to 1.8 s) never fails a seed;
            // with the shortest hold the puck still drops inside the 7 s this check always asked for
            Check(
                setAt - start < 7 - RefereeTuning.SetSeconds,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"and got there in good time (set {setAt - start:0.0} s after the whistle, dropped at {dropAt - start:0.0} s)"
                )
            );
        }

        /// <summary>Ten faceoffs in a row at centre ice with seeded dice: every hold lies inside the span, they differ visibly, and the same seed repeats them.</summary>
        [Test]
        public void DropSpread()
        {
            const int seed = 11;
            static List<float> Holds(World w)
            {
                RefereeSystem referee = new(Seeds.Dice(seed));
                RefereeState r = RefereeState.Of(w);
                List<float> holds = [];
                void Record(World x)
                {
                    if (x.Events.Of<PuckDropped>().Any())
                    {
                        holds.Add(r.LastHold);
                    }
                }
                Run(w, x => x.Match.Phase == MatchPhase.Play, 20, Record, referee);
                for (int i = 1; i < 10; i++)
                {
                    _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
                    Run(w, x => x.Match.Phase == MatchPhase.Play, 20, Record, referee);
                }
                return holds;
            }

            List<float> first = Holds(Make());
            List<float> again = Holds(Make());
            float min = first.Min(),
                max = first.Max();
            float smallestChange = first.Zip(first.Skip(1), (a, b) => MathF.Abs(a - b)).Min();
            string list = string.Join(
                ' ',
                first.Select(h => h.ToString("0.00", CultureInfo.InvariantCulture))
            );
            Check(first.Count == 10, $"ten faceoffs in a row were dropped ({first.Count})");
            Check(
                min >= RefereeTuning.SetSeconds - Game.Step
                    && max <= RefereeTuning.MaxSetSeconds + Game.Step,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"every hold lies within {RefereeTuning.SetSeconds:0.0} to {RefereeTuning.MaxSetSeconds:0.0} s (holds {list})"
                )
            );
            Check(
                smallestChange >= RefereeTuning.MinHoldChange - (2 * Game.Step)
                    && max - min >= 0.6f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"each drop comes at a visibly different moment from the one before (smallest change {smallestChange:0.00} s, spread {max - min:0.00} s)"
                )
            );
            Check(first.SequenceEqual(again), "the same seed drops at the same moments again");
        }

        [Test]
        public void WholeMatch()
        {
            World w = Make();
            w.Dev.Match.Set(new MatchDemo());
            RefereeState r = RefereeState.Of(w);
            int setups = 0,
                drops = 0,
                atDot = 0,
                playSteps = 0,
                nearPuck = 0,
                nearPlayer = 0;
            float closestPlayer = float.MaxValue,
                droppedAt = float.NegativeInfinity;
            MatchPhase last = w.Match.Phase;
            Run(
                w,
                x => x.Match.Phase == MatchPhase.Final,
                600,
                x =>
                {
                    if (x.Match.Phase == MatchPhase.Faceoff && last != MatchPhase.Faceoff)
                    {
                        setups++;
                    }

                    last = x.Match.Phase;
                    if (x.Events.Of<PuckDropped>().Any())
                    {
                        drops++;
                        droppedAt = x.Time;
                        if (
                            Distance(r.Position, RefereeState.DropSpot(x.Puck.Position))
                            <= RefereeTuning.Arrived
                        )
                        {
                            atDot++;
                        }
                    }
                    // the referee stands at the dot for the drop and needs a moment to back off
                    if (
                        x.Match.Phase != MatchPhase.Play
                        || x.Time - droppedAt < RefereeTuning.BackOffSeconds
                    )
                    {
                        return;
                    }

                    playSteps++;
                    if (Distance(r.Position, x.Puck.Position) < 1.5f)
                    {
                        nearPuck++;
                    }

                    float d = x
                        .Players.Where(p => p.OnIce)
                        .Min(p => Distance(r.Position, p.Position));
                    closestPlayer = MathF.Min(closestPlayer, d);
                    if (d < 1f)
                    {
                        nearPlayer++;
                    }
                }
            );
            VerifyRefereeMatchSummary(
                w,
                r,
                setups,
                drops,
                atDot,
                playSteps,
                nearPuck,
                nearPlayer,
                closestPlayer
            );
        }

        private void VerifyRefereeMatchSummary(
            World w,
            RefereeState r,
            int setups,
            int drops,
            int atDot,
            int playSteps,
            int nearPuck,
            int nearPlayer,
            float closestPlayer
        )
        {
            w.Dev.Match.Clear();
            Check(
                w.Match.Phase == MatchPhase.Final,
                "a whole match with the referee reaches the final"
            );
            Check(
                setups > 3 && drops == setups,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the referee dropped every faceoff ({drops} of {setups})"
                )
            );
            Check(
                atDot == drops,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"each from the drop spot beside the dot ({atDot} of {drops})"
                )
            );
            Check(
                r.LateDrops == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"none needed the safety net ({r.LateDrops})"
                )
            );
            Check(
                nearPuck <= playSteps / 100,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the referee stays off the puck in play ({nearPuck} of {playSteps} steps within 1.5 m)"
                )
            );
            // backing off between the wingers right after a drop passes about a metre from each
            Check(
                closestPlayer >= 0.7f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"and never bumps a player (closest {closestPlayer:0.0} m, {nearPlayer} of {playSteps} steps within 1 m)"
                )
            );
        }
    }
}
