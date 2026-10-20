using System.Numerics;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Benches;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Match
{
    /// <summary>
    /// The goal review (W79, W81, W80): under SHL and NHL rules a goal is held under review before it counts, and
    /// nothing reacts to it until the review confirms it; an attacker in the crease leaning on the goalie as it goes
    /// in takes it off the board for goalie interference. In Arcade a goal counts at once. Under NHL rules the club
    /// scored on may challenge once a game: a challenge that holds disallows the goal, a failed one costs a minor.
    /// </summary>
    public sealed class GoalReviewTests : CheckedTest
    {
        private static List<MatchEvent> Run(
            World world,
            ISystem[] systems,
            float seconds,
            Action<World> each
        )
        {
            List<MatchEvent> events = [];
            for (int i = 0; i < seconds / Game.Step; i++)
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

        private static List<MatchEvent> Run(World world, ISystem[] systems, float seconds)
        {
            return Run(world, systems, seconds, _ => { });
        }

        /// <summary>A match in play after the opening drop, with the referee calling penalties.</summary>
        private static (World World, ISystem[] Systems) InPlay(RuleSet rules)
        {
            World w = new WorldBuilder().Rules(rules).Build();
            ISystem[] systems = [new PuckPhysics(), new MatchRules(), new PenaltyRules()];
            for (int i = 0; i < 10 / Game.Step && w.Match.Phase != MatchPhase.Play; i++)
            {
                _ = Run(w, systems, Game.Step);
            }

            return (w, systems);
        }

        /// <summary>The net <paramref name="scorer"/> attacks: -1 or +1.</summary>
        private static int Attacks(World w, Side scorer)
        {
            return -FaceoffFormation.DefendedEnd(w.Match, scorer);
        }

        /// <summary>Clears every skater away to centre ice, sets the goalie at his far post and, for a screen, an attacker
        /// in the crease: leaning on the goalie (<paramref name="contact"/>) or a stride off him. Returns a hold that keeps them there.</summary>
        private static Action<World> SetUp(World w, Side scorer, bool screen, bool contact)
        {
            int end = Attacks(w, scorer);
            Side defender = scorer == Side.Home ? Side.Away : Side.Home;
            Player goalie = w.OnIce(defender).First(p => p.IsGoalie);
            foreach (Player p in w.Players.Where(p => p.OnIce && !p.IsGoalie))
            {
                p.Position = new Vector3(0, 0, p.Position.Z * 0.3f);
                p.Velocity = Vector3.Zero;
            }

            Vector3 at = new(end * (RinkSpec.GoalLineX - 0.4f), 0, -0.45f);
            Player s = w.OnIce(scorer).Where(p => !p.IsGoalie).Skip(1).First();
            Vector3 screenAt = contact
                ? new Vector3(end * (RinkSpec.GoalLineX - 0.5f), 0, -1.05f)
                : new Vector3(end * (RinkSpec.GoalLineX - 0.6f), 0, -1.45f);
            return world =>
            {
                goalie.Position = at;
                goalie.Velocity = Vector3.Zero;
                if (screen)
                {
                    s.Position = screenAt;
                    s.Velocity = Vector3.Zero;
                }
            };
        }

        /// <summary>A skater of <paramref name="scorer"/> puts the puck in at the near post.</summary>
        private static Player Shoot(World w, Side scorer)
        {
            int end = Attacks(w, scorer);
            Player shooter = w.OnIce(scorer).First(p => !p.IsGoalie);
            Puck pk = w.Puck;
            pk.Carrier = -1;
            pk.Position = new Vector3(end * (RinkSpec.GoalLineX - 0.3f), 0, 0.6f);
            pk.Velocity = new Vector3(end * 12f, 0, 0);
            pk.LastTouch = shooter.Id;
            return shooter;
        }

        private static int Count<T>(List<MatchEvent> events)
            where T : MatchEvent
        {
            return events.OfType<T>().Count();
        }

        [Test]
        public void RulesPerPreset()
        {
            Check(
                RuleSet.Shl.GoalReview && RuleSet.Nhl.GoalReview && !RuleSet.Arcade.GoalReview,
                "goal review: on in SHL and NHL, off in Arcade"
            );
            Check(
                !RuleSet.Shl.GoalChallenge
                    && RuleSet.Nhl.GoalChallenge
                    && !RuleSet.Arcade.GoalChallenge,
                "the coach's challenge on goals: NHL only"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.GoalReview), StringComparison.Ordinal)
                )
                    && RuleRows.All.Any(r =>
                        string.Equals(
                            r.Key,
                            nameof(RuleSet.GoalChallenge),
                            StringComparison.Ordinal
                        )
                    ),
                "both are rows on the rules page"
            );
        }

        /// <summary>A clean goal is held under review, nothing counts it, then the review confirms it.</summary>
        [Test]
        public void CleanGoalConfirmed()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Shl);
            Action<World> hold = SetUp(w, Side.Home, screen: false, contact: false);
            Player shooter = Shoot(w, Side.Home);
            List<MatchEvent> ev = Run(w, systems, 0.5f, hold);
            Check(
                Count<GoalUnderReview>(ev) == 1
                    && Count<GoalScored>(ev) == 0
                    && w.Match.Score[0] == 0
                    && GoalReview.UnderReview(w)
                    && w.Match.Phase == MatchPhase.Stopped
                    && !w.Puck.Live,
                "the puck in: GOAL UNDER REVIEW, play stopped and the score untouched"
            );
            ev = Run(w, systems, GoalReview.QuickSeconds + 0.2f, hold);
            GoalScored goal =
                ev.OfType<GoalScored>().FirstOrDefault() ?? new(0, Side.Away, -1, default, 0);
            Check(
                Count<GoalScored>(ev) == 1
                    && goal.Side == Side.Home
                    && goal.ScorerId == shooter.Id
                    && goal.ReviewSeconds >= GoalReview.QuickSeconds - 0.05f
                    && Count<GoalDisallowed>(ev) == 0
                    && w.Match.Score[0] == 1
                    && w.Match.Phase == MatchPhase.Goal
                    && !GoalReview.UnderReview(w),
                "a clean goal is confirmed after a short review and counts"
            );
        }

        /// <summary>An attacker leaning on the goalie in his crease as the puck goes in: no goal, under SHL and NHL rules.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void InterferenceDisallowed(RulePreset preset)
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Of(preset));
            Action<World> hold = SetUp(w, Side.Home, screen: true, contact: true);
            _ = Run(w, systems, 0.6f, hold); // the screen settles on the goalie first
            _ = Shoot(w, Side.Home);
            List<MatchEvent> ev = Run(w, systems, GoalReview.LongSeconds + 0.5f, hold);
            GoalDisallowed off =
                ev.OfType<GoalDisallowed>().FirstOrDefault()
                ?? new(0, Side.Away, -1, default, ReviewCall.Offside, Challenged: true);
            Check(
                Count<GoalUnderReview>(ev) == 1
                    && Count<GoalScored>(ev) == 0
                    && off.Side == Side.Home
                    && off.Call == ReviewCall.GoalieInterference
                    && !off.Challenged
                    && w.Match.Score[0] == 0,
                $"{preset}: goalie interference takes the goal off the board"
            );
            int end = Attacks(w, Side.Home);
            Vector3 dot = MatchFlow.FaceoffDot(w);
            Check(
                MathF.Abs(dot.X - (end * OffsideRule.NeutralDotX)) < 0.5f,
                $"{preset}: the faceoff is at the neutral-zone dot outside that zone"
            );
        }

        /// <summary>In Arcade there is no review: the goal counts the step it goes in, contact or not.</summary>
        [Test]
        public void ArcadeCountsAtOnce()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Arcade);
            Action<World> hold = SetUp(w, Side.Home, screen: true, contact: true);
            _ = Run(w, systems, 0.6f, hold);
            _ = Shoot(w, Side.Home);
            List<MatchEvent> ev = Run(w, systems, 0.3f, hold);
            Check(
                Count<GoalUnderReview>(ev) == 0
                    && Count<GoalScored>(ev) == 1
                    && Count<GoalDisallowed>(ev) == 0
                    && w.Match.Score[0] == 1,
                "Arcade: no review, the goal counts at once"
            );
        }

        /// <summary>The goal's consumers react to the confirmed goal only: a held goal has no horn, no cheering bench, no GOAL! banner.</summary>
        [Test]
        public void ConsumersWaitForConfirmation()
        {
            (World w, _) = InPlay(RuleSet.Shl);
            Player shooter = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            GoalScored goal = new(w.Time, Side.Home, shooter.Id, new Vector3(26.8f, 0, 0), 20);
            GoalUnderReview held = new(w.Time, goal);
            Check(
                !SoundCues.For(held).Any(c => c.Sound is Sfx.Horn or Sfx.Goal),
                "no horn or goal song while the goal is under review"
            );
            Check(
                SoundCues.For(goal).Any(c => c.Sound == Sfx.Horn),
                "the confirmed goal sounds the horn"
            );
            Check(
                BenchCue.For(held, Side.Home).Count == 0
                    && BenchCue.For(held, Side.Away).Count == 0,
                "neither bench reacts to a goal under review"
            );
            HudMessages hud = new();
            w.Events.Publish(held);
            hud.Update(w);
            Check(
                hud.Current.Is(m =>
                    string.Equals(
                        m.Title,
                        Text.Get(Text.Keys.GoalUnderReview),
                        StringComparison.Ordinal
                    )
                )
                    && hud.Goals.Count == 0,
                "the HUD shows GOAL UNDER REVIEW and tallies no goal"
            );
            w.Events.Clear();
            w.Events.Publish(goal with { ReviewSeconds = 1.5f });
            hud.Update(w);
            Check(
                hud.Current.Is(m =>
                    string.Equals(m.Title, Text.Get(Text.Keys.Goal), StringComparison.Ordinal)
                )
                    && hud.Goals.Count == 1,
                "the confirmed goal replaces it with GOAL! and is tallied"
            );
            w.Events.Clear();
            w.Events.Publish(
                new GoalDisallowed(
                    w.Time,
                    Side.Home,
                    shooter.Id,
                    default,
                    ReviewCall.GoalieInterference,
                    Challenged: false
                )
            );
            hud.Update(w);
            Check(
                hud.Current.Is(m =>
                    string.Equals(m.Title, Text.Get(Text.Keys.NoGoal), StringComparison.Ordinal)
                    && string.Equals(
                        m.Detail,
                        Text.Get(Text.Keys.GoalieInterference),
                        StringComparison.Ordinal
                    )
                ),
                "a disallowed goal shows NO GOAL with goalie interference"
            );
        }

        /// <summary>Start skips the review: the verdict comes at once.</summary>
        [Test]
        public void StartSkips()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Shl);
            Action<World> hold = SetUp(w, Side.Home, screen: false, contact: false);
            _ = Shoot(w, Side.Home);
            _ = Run(w, systems, 0.2f, hold);
            GoalReview.Skip(w);
            List<MatchEvent> ev = Run(w, systems, Game.Step * 2, hold);
            Check(
                Count<GoalScored>(ev) == 1 && w.Match.Score[0] == 1,
                "a skipped review gives its verdict at once"
            );
        }

        /// <summary>NHL: the human coach of the club scored on is asked; pass challenges for goalie interference, which holds with an attacker in the crease.</summary>
        [Test]
        public void ChallengeHolds()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Nhl);
            Player human = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            human.Slot = 0;
            Action<World> hold = SetUp(w, Side.Home, screen: true, contact: false);
            _ = Run(w, systems, 0.3f, hold);
            _ = Shoot(w, Side.Home);
            List<MatchEvent> ev = Run(w, systems, GoalReview.LongSeconds + 0.3f, hold);
            Check(
                Count<GoalDisallowed>(ev) == 0
                    && Count<GoalScored>(ev) == 0
                    && GoalReview.Asking(w).Is(s => s == Side.Away),
                "an attacker in the crease off the goalie: the review lets it stand, and the away coach is asked"
            );
            ev = Run(
                w,
                systems,
                GoalReview.ChallengeSeconds + 0.3f,
                world =>
                {
                    hold(world);
                    human.Command = human.Command with { Pass = true };
                }
            );
            Check(
                ev.OfType<CoachChallenged>()
                    .Any(c => c.Side == Side.Away && c.Call == ReviewCall.GoalieInterference)
                    && ev.OfType<GoalDisallowed>()
                        .Any(d => d.Challenged && d.Call == ReviewCall.GoalieInterference)
                    && Count<GoalScored>(ev) == 0
                    && Count<PenaltyCalled>(ev) == 0
                    && w.Match.Score[0] == 0
                    && GoalReview.ChallengeUsed(w, Side.Away),
                "the challenge holds: no goal, no minor, and the challenge is used"
            );
        }

        /// <summary>NHL: a challenge for offside with a clean entry fails: the goal stands and the club takes a delay-of-game minor.</summary>
        [Test]
        public void FailedChallengeCostsMinor()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Nhl);
            Player human = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            human.Slot = 0;
            Action<World> hold = SetUp(w, Side.Home, screen: false, contact: false);
            _ = Shoot(w, Side.Home);
            _ = Run(w, systems, GoalReview.QuickSeconds + 0.3f, hold);
            Check(GoalReview.Asking(w).Is(s => s == Side.Away), "the away coach is asked");
            List<MatchEvent> ev = Run(
                w,
                systems,
                GoalReview.ChallengeSeconds + 0.5f,
                world =>
                {
                    hold(world);
                    human.Command = human.Command with { Shoot = true };
                }
            );
            Check(
                ev.OfType<CoachChallenged>().Any(c => c.Call == ReviewCall.Offside)
                    && Count<GoalScored>(ev) == 1
                    && Count<GoalDisallowed>(ev) == 0
                    && w.Match.Score[0] == 1,
                "the offside challenge fails: the goal stands"
            );
            Check(
                ev.OfType<PenaltyCalled>().Any(p => p.Side == Side.Away)
                    && w.Match.Penalties.Any(p => p.Side == Side.Away)
                    && w.Match.Phase == MatchPhase.Goal,
                "a failed challenge costs the away club a minor; the goal's pause carries on to the centre faceoff"
            );
        }

        /// <summary>NHL: an offside entry the linesman missed makes an offside challenge hold.</summary>
        [Test]
        public void OffsideChallengeHolds()
        {
            (World w, ISystem[] systems) = InPlay(RuleSet.Nhl);
            Player human = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            human.Slot = 0;
            Action<World> hold = SetUp(w, Side.Home, screen: false, contact: false);
            int end = Attacks(w, Side.Home);
            OffsideState offside = w.Offside.Get(() => new OffsideState());
            offside.Zone = end;
            offside.MissedEnd = end; // the home club came in a skate offside and play went on
            Check(OffsideRule.MissedOffside(w, Side.Home), "the missed entry is on record");
            _ = Shoot(w, Side.Home);
            _ = Run(w, systems, GoalReview.QuickSeconds + 0.3f, hold);
            List<MatchEvent> ev = Run(
                w,
                systems,
                GoalReview.ChallengeSeconds + 0.3f,
                world =>
                {
                    hold(world);
                    human.Command = human.Command with { Shoot = true };
                }
            );
            Check(
                ev.OfType<GoalDisallowed>().Any(d => d.Challenged && d.Call == ReviewCall.Offside)
                    && w.Match.Score[0] == 0,
                "the offside challenge holds: no goal"
            );
        }

        /// <summary>Under the challenge rule the linesman misses an offside by a skate's length; a clear one he still calls.</summary>
        [Test]
        public void LinesmanMissesCloseOffside()
        {
            foreach (
                (RuleSet rules, float past, bool missed) in new[]
                {
                    (RuleSet.Nhl, 0.2f, true),
                    (RuleSet.Nhl, 2f, false),
                    (RuleSet.Shl, 0.2f, false),
                }
            )
            {
                World w = new WorldBuilder().Rules(rules).Build();
                ISystem[] systems = [new PuckPhysics(), new MatchRules(), new OffsideRule()];
                for (int i = 0; i < 10 / Game.Step && w.Match.Phase != MatchPhase.Play; i++)
                {
                    _ = Run(w, systems, Game.Step);
                }

                int end = Attacks(w, Side.Home);
                List<Player> home = [.. w.OnIce(Side.Home).Where(p => !p.IsGoalie)];
                foreach (Player p in w.Players.Where(p => p.OnIce && !p.IsGoalie))
                {
                    p.Position = new Vector3(-end * 3f, 0, p.Position.Z);
                    p.Velocity = Vector3.Zero;
                }

                float limit =
                    RinkSpec.BlueLineX + OffsideRule.LineHalfWidth + OffsideRule.SkateReach;
                Player early = home[1];
                Player carrier = home[0];
                void Each(World world)
                {
                    early.Position = new Vector3(end * (limit + past), 0, 4f);
                    early.Velocity = Vector3.Zero;
                }

                carrier.Position = new Vector3(end * (RinkSpec.BlueLineX - 1f), 0, -2f);
                w.Puck.Carrier = -1;
                w.Puck.LastTouch = carrier.Id;
                w.Puck.Position = carrier.Position;
                w.Puck.Velocity = new Vector3(end * 8f, 0, 0); // a dump-in
                List<MatchEvent> ev = Run(w, systems, 0.6f, Each);
                w.Puck.LastTouch = early.Id; // he plays it in the zone
                ev.AddRange(Run(w, systems, 0.3f, Each));
                bool called = Count<OffsideCalled>(ev) > 0;
                Check(
                    called != missed && OffsideRule.MissedOffside(w, Side.Home) == missed,
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{rules.Preset} {past} m past: {(missed ? "missed, on record for a challenge" : "called")}"
                    )
                );
            }
        }
    }
}
