using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Penalties
{
    /// <summary>
    /// The penalty shot: which carriers are on a clean breakaway, which calls give a penalty shot instead of a minor
    /// under each preset, and the shot itself played from centre against the goalie, with play resuming at centre
    /// after a goal or a miss.
    /// </summary>
    public sealed class PenaltyShotTests : CheckedTest
    {
        private static World Make(RuleSet rules, int seed = 1)
        {
            return new WorldBuilder()
                .Rules(rules)
                .Seed(seed)
                .Setup(s => s with { PeriodSeconds = 600 })
                .Then(w => _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 15))
                .Build();
        }

        private static List<MatchEvent> Run(World world, Func<World, bool> until, float limit)
        {
            return Run(world, until, _ => { }, limit);
        }

        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            Action<World> each,
            float limit
        )
        {
            using GoalieSystem goalie = new() { Dice = Seeds.Dice(3) };
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(3)),
                new LinesSystem(),
                new SkatingSystem(),
                new PuckHandlingSystem(Seeds.Dice(3)),
                goalie,
                new PuckPhysics(),
                new MatchRules(),
                new PenaltyRules(),
            ];
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
                each(world);
            }
            return events;
        }

        private static Player Skater(World w, Side side, PlayingPosition position)
        {
            return w.OnIce(side).First(p => p.Data.Position == position);
        }

        /// <summary>The away centre with the puck <paramref name="at"/> metres into his attacking half, every home
        /// skater <paramref name="behind"/> metres or more behind him, the home centre the nearest.</summary>
        private static (Player Carrier, Player Chaser) Breakaway(
            World w,
            float at,
            float behind = 1.2f
        )
        {
            int attack = PuckHandling.AttackEnd(w, Side.Away);
            Player carrier = Skater(w, Side.Away, PlayingPosition.C);
            carrier.Position = new Vector3(attack * at, 0, 0);
            carrier.Heading = attack > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            Player chaser = Skater(w, Side.Home, PlayingPosition.C);
            int i = 0;
            foreach (Player p in w.OnIce(Side.Home).Where(p => !p.IsGoalie))
            {
                float back = p == chaser ? behind : behind + 3 + i++;
                p.Position = new Vector3(
                    attack * (at - back),
                    0,
                    p == chaser ? 0.3f : (i - 2) * 3f
                );
                p.Velocity = Vector3.Zero;
            }

            w.Puck.Carrier = carrier.Id;
            w.Puck.LastTouch = carrier.Id;
            w.Puck.Position = PuckHandling.Blade(carrier);
            return (carrier, chaser);
        }

        [Test]
        public void BreakawayDetection()
        {
            World w = Make(RuleSet.Shl);
            int attack = PuckHandling.AttackEnd(w, Side.Away);
            (Player carrier, Player chaser) = Breakaway(w, 10);
            Check(
                PenaltyShot.Breakaway(w, carrier),
                "past every defender in the attacking half: clean"
            );

            chaser.Position = new Vector3(attack * 10.3f, 0, 2);
            Check(
                PenaltyShot.Breakaway(w, carrier),
                "a defender level with him (0.3 m nearer the goal) is not in the way"
            );

            chaser.Position = new Vector3(attack * 14, 0, 1);
            Check(
                !PenaltyShot.Breakaway(w, carrier),
                "a defender between him and the goal: no breakaway"
            );

            _ = Breakaway(w, -3);
            Check(!PenaltyShot.Breakaway(w, carrier), "still in his own half: no breakaway");

            _ = Breakaway(w, 27.5f);
            Check(!PenaltyShot.Breakaway(w, carrier), "behind the goal line: no breakaway");

            Player goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
            _ = Breakaway(w, 20);
            goalie.Position = new Vector3(attack * 25, 0, 0);
            Check(PenaltyShot.Breakaway(w, carrier), "only the goalie to beat: clean");
            Check(!PenaltyShot.Breakaway(w, goalie), "a goalie is never on a breakaway");
        }

        [Test]
        public void FoulFromBehindPerPreset()
        {
            foreach (
                (RuleSet rules, bool shot) in new[]
                {
                    (RuleSet.Shl, true),
                    (RuleSet.Nhl, true),
                    (RuleSet.Arcade, false),
                }
            )
            {
                string name = rules.Name;
                World w = Make(rules);
                (Player carrier, Player chaser) = Breakaway(w, 8);
                List<MatchEvent> events = [];
                bool awarded = PenaltyShot.Award(w, chaser.Id, Infraction.Tripping);
                events.AddRange(w.Events.Current);
                Check(
                    awarded == shot && PenaltyShot.Running(w) == shot,
                    $"{name}: a trip from behind on a breakaway {(shot ? "is" : "is not")} a penalty shot"
                );
                if (shot)
                {
                    Check(
                        w.Match.Phase == MatchPhase.Stopped
                            && events.OfType<PenaltyShotAwarded>().SingleOrDefault() is { } e
                            && e.ShooterId == carrier.Id
                            && e.OffenderId == chaser.Id
                            && e.Side == Side.Away
                            && w.Match.Penalties.Count == 0
                            && PenaltyCalls.Delayed(w).Count == 0,
                        $"{name}: the whistle goes at once, the fouled skater shoots and nobody goes to the box"
                    );
                }
            }

            World nhl = Make(RuleSet.Nhl);
            (_, Player hooker) = Breakaway(nhl, 8);
            Check(
                PenaltyShot.Awarded(nhl, hooker.Id, Infraction.Hooking).HasValue
                    && PenaltyShot.Awarded(nhl, hooker.Id, Infraction.Slashing).HasValue,
                "a hook or a slash from behind gives it too"
            );
            Check(
                !PenaltyShot.Awarded(nhl, hooker.Id, Infraction.Interference).HasValue
                    && !PenaltyShot
                        .Awarded(
                            nhl,
                            hooker.Id,
                            Infraction.Tripping,
                            Severity.MajorAndGameMisconduct
                        )
                        .HasValue,
                "other infractions and more than a minor stay calls"
            );
            World off = new WorldBuilder()
                .Rules(RuleSet.Shl with { PenaltyShot = false })
                .Then(w => _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 15))
                .Build();
            (_, Player tripper) = Breakaway(off, 8);
            Check(
                !PenaltyShot.Awarded(off, tripper.Id, Infraction.Tripping).HasValue,
                "the rule switched off: a trip on a breakaway stays a minor"
            );

            World notClean = Make(RuleSet.Shl);
            (Player c2, Player d2) = Breakaway(notClean, 8);
            Player other = Skater(notClean, Side.Home, PlayingPosition.D);
            other.Position =
                c2.Position + new Vector3(PuckHandling.AttackEnd(notClean, Side.Away) * 5, 0, 1);
            Check(
                !PenaltyShot.Award(notClean, d2.Id, Infraction.Tripping)
                    && notClean.Match.Phase == MatchPhase.Play,
                "a defender still between him and the goal: no penalty shot, play goes on"
            );

            World front = Make(RuleSet.Shl);
            (Player c3, _) = Breakaway(front, 8);
            Player d3 = Skater(front, Side.Home, PlayingPosition.D);
            int attack = PuckHandling.AttackEnd(front, Side.Away);
            d3.Position = c3.Position + new Vector3(attack * 0.4f, 0, 0);
            Check(
                !PenaltyShot.FromBehind(front, d3, c3),
                "a defender in front of the carrier does not foul him from behind"
            );
        }

        [Test]
        public void ShotPlayedAndPlayResumesAtCentre()
        {
            // an empty net: the shot scores
            World goal = Make(RuleSet.Shl);
            int skaters = goal.Players.Count(p => p.OnIce && !p.IsGoalie);
            (Player shooter, Player chaser) = Breakaway(goal, 8);
            goal.OnIce(Side.Home).First(p => p.IsGoalie).OnIce = false;
            _ = PenaltyShot.Award(goal, chaser.Id, Infraction.Tripping);
            bool alone = true,
                clockStill = true;
            float clock = goal.Match.Clock;
            List<MatchEvent> ev = [];
            ev.AddRange(
                Run(
                    goal,
                    x => x.Match.Phase == MatchPhase.Faceoff,
                    x =>
                    {
                        if (PenaltyShot.Running(x) && x.Puck.Live)
                        {
                            alone &=
                                x.Players.Where(p => p.OnIce && !p.IsGoalie).Take(2).Count() == 1
                                && shooter.OnIce;
                        }

                        clockStill &= MathF.Abs(x.Match.Clock - clock) < 1e-6f;
                    },
                    30
                )
            );
            Check(
                ev.OfType<GoalScored>().Any(g => g.Side == Side.Away && g.ScorerId == shooter.Id)
                    && goal.Match.Score[(int)Side.Away] == 1,
                "the shooter scores on the penalty shot and it counts"
            );
            Check(alone, "the shooter is alone on the ice while he shoots");
            Check(clockStill, "the clock stands still through the penalty shot");
            Check(
                goal.Match.Phase == MatchPhase.Faceoff
                    && MatchFlow.FaceoffDot(goal) == RinkSpec.CentreDot
                    && !PenaltyShot.Running(goal)
                    && goal.Players.Where(p => p.OnIce && !p.IsGoalie).Take(skaters + 1).Count()
                        == skaters,
                "after the goal play resumes with a faceoff at centre, the skaters back on"
            );

            // with the goalie in: every seed ends at a centre faceoff, goal or not, and repeats exactly
            int misses = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                string first = PlayShot(seed, out bool scored, out bool centre);
                string again = PlayShot(seed, out _, out _);
                misses += scored ? 0 : 1;
                Check(
                    centre,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"seed {seed}: the penalty shot ({(scored ? "goal" : "miss")}) ends at a faceoff at centre"
                    )
                );
                Check(
                    string.Equals(first, again, StringComparison.Ordinal),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"seed {seed}: the shot repeats exactly"
                    )
                );
            }

            Check(
                misses > 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the goalie stops some of the shots ({misses} of 4)"
                )
            );
        }

        /// <summary>Plays one penalty shot with the goalie in; returns the shooter's path and the result.</summary>
        private static string PlayShot(int seed, out bool scored, out bool centre)
        {
            World w = Make(RuleSet.Shl, seed);
            (Player shooter, Player chaser) = Breakaway(w, 8);
            _ = PenaltyShot.Award(w, chaser.Id, Infraction.Tripping);
            System.Text.StringBuilder path = new();
            _ = Run(
                w,
                x => x.Match.Phase == MatchPhase.Faceoff,
                x =>
                    path.Append(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{shooter.Position.X:R},{shooter.Position.Z:R};{x.Puck.Position.X:R};"
                        )
                    ),
                30
            );
            scored = w.Match.Score[(int)Side.Away] == 1;
            centre =
                w.Match.Phase == MatchPhase.Faceoff
                && MatchFlow.FaceoffDot(w) == RinkSpec.CentreDot
                && !PenaltyShot.Running(w);
            return path.ToString();
        }
    }
}
