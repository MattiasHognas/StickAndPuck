using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Match
{
    /// <summary>The faceoff rules under each preset: a violation waves the centre out for a winger (SHL, NHL; a plain
    /// false start in Arcade), a second violation is a delay-of-game minor in the NHL and a false start in the SHL,
    /// AI centres sometimes jump the drop, smart dots choose the dot against the offending club, and an intentional
    /// offside moves the draw to the offender's own end. In period 1 home defends -x.</summary>
    public sealed class FaceoffRulesTests : CheckedTest
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            World,
            ISystem[]
        > Sets = [];

        private static ISystem[] Systems(World world)
        {
            return Sets.GetValue(
                world,
                _ => [new PuckPhysics(), new MatchRules(), new OffsideRule()]
            );
        }

        /// <summary>Runs physics and the rules step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            Action<World> each,
            float limit = 5
        )
        {
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                each(world);
                foreach (ISystem s in Systems(world))
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static List<MatchEvent> Run(World world, Func<World, bool> until, float limit = 5)
        {
            return Run(world, until, _ => { }, limit);
        }

        private static DrawState Draw(World world)
        {
            return MatchFlow.State(world).Draw;
        }

        private static void Press(World world, bool down)
        {
            if (world.Controlled(0).TryGet(out Player p))
            {
                p.Command = p.Command with { Shoot = down };
            }
        }

        /// <summary>A world with the human on the home centre, run up to the opening faceoff.</summary>
        private World AtFaceoff(RuleSet rules)
        {
            World w = new WorldBuilder().Rules(rules).Human().Build();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff);
            _ = Run(w, _ => false, 0.1f);
            Check(
                w.Match.Phase == MatchPhase.Faceoff && w.Controlled(0).Is(IsCentre),
                $"{rules.Name}: the human takes the opening draw with the centre"
            );
            return w;
        }

        /// <summary>One fresh early press: down for a step, then up again.</summary>
        private static List<MatchEvent> EarlyPress(World w)
        {
            bool pressed = false;
            List<MatchEvent> ev = Run(
                w,
                _ => pressed,
                x =>
                {
                    Press(x, down: true);
                    pressed = true;
                },
                1
            );
            ev.AddRange(Run(w, _ => false, x => Press(x, down: false), 0.05f));
            return ev;
        }

        private static bool IsCentre(Player p)
        {
            return p.Data.Position == PlayingPosition.C;
        }

        private static bool IsWing(Player p)
        {
            return p.Data.Position is PlayingPosition.LW or PlayingPosition.RW;
        }

        /// <summary>The rule switches each preset starts with.</summary>
        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl
                    is {
                        FaceoffViolations: true,
                        FaceoffViolationMinor: false,
                        SmartDots: true,
                        IntentionalOffside: true
                    },
                "SHL: violations wave the centre out, no minor, smart dots, intentional offside"
            );
            Check(
                RuleSet.Nhl
                    is {
                        FaceoffViolations: true,
                        FaceoffViolationMinor: true,
                        SmartDots: true,
                        IntentionalOffside: true
                    },
                "NHL: as the SHL with a minor on the second violation"
            );
            Check(
                RuleSet.Arcade
                    is {
                        FaceoffViolations: false,
                        FaceoffViolationMinor: false,
                        SmartDots: false,
                        IntentionalOffside: false
                    },
                "Arcade: plain faceoffs"
            );
            RuleRow minor = RuleRows.All.First(r =>
                string.Equals(
                    r.Key,
                    nameof(RuleSet.FaceoffViolationMinor),
                    StringComparison.Ordinal
                )
            );
            RuleSet custom = RuleChoice.Of(RulePreset.Shl).With(minor, "On").Resolve();
            Check(
                custom is { FaceoffViolationMinor: true, Custom: true }
                    && RuleRows.Shown.Contains(minor),
                "the minor is a rule on the rules page that can be switched on for the SHL"
            );
        }

        /// <summary>SHL and NHL: an early press waves the centre out and a winger takes the draw, with the human on him.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void ViolationWingerTakesDraw(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = AtFaceoff(rules);
            Player centre = w.Controlled(0).Or(w.Players[0]);
            List<MatchEvent> ev = EarlyPress(w);
            List<FaceoffViolated> v = [.. ev.OfType<FaceoffViolated>()];
            Check(
                v.Count == 1 && v[0].Side == Side.Home && v[0].PlayerId == centre.Id,
                $"{rules.Name}: the early press is a violation by the home centre"
            );
            Check(
                Draw(w).Kicked.Contains(centre.Id) && !Draw(w).FalseStart.IsSet(),
                $"{rules.Name}: the centre is waved out, no false start"
            );
            Check(
                FaceoffDraw.Centre(w, MatchFlow.FaceoffDot(w), Side.Home).Is(IsWing)
                    && w.Controlled(0).Is(IsWing),
                $"{rules.Name}: a winger takes the draw and the human is moved onto him"
            );
            Check(
                v.Count == 1
                    && w.Controlled(0).Is(p => p.Id == v[0].TakerId)
                    && v[0].TakerId != centre.Id,
                $"{rules.Name}: the violation names the winger who takes the draw"
            );
            Check(
                w.Match.Phase == MatchPhase.Faceoff && !Draw(w).Dropped,
                $"{rules.Name}: the faceoff waits for a new drop"
            );
            float dropAt = -1;
            _ = Run(
                w,
                x => x.Match.Phase == MatchPhase.Play,
                x =>
                {
                    if (Draw(x).Dropped && dropAt < 0)
                    {
                        dropAt = x.Time;
                    }

                    Press(x, Draw(x).Dropped && x.Time - dropAt > 0.05f);
                },
                3
            );
            Check(
                Draw(w).Winner == SideOrNone.Home,
                $"{rules.Name}: the winger wins the draw with a timed press"
            );
        }

        /// <summary>Arcade: an early press is the old false start; nobody is waved out and the other club wins at the drop.</summary>
        [Test]
        public void ArcadeFalseStart()
        {
            World w = AtFaceoff(RuleSet.Arcade);
            List<MatchEvent> ev = EarlyPress(w);
            ev.AddRange(Run(w, x => x.Match.Phase == MatchPhase.Play));
            Check(
                !ev.OfType<FaceoffViolated>().Any() && Draw(w).Kicked.Count == 0,
                "Arcade: no violation, nobody waved out"
            );
            Check(
                Draw(w).Winner == SideOrNone.Away,
                "Arcade: the false start hands the draw to the visitors"
            );
        }

        /// <summary>The same club's second violation: a false start in the SHL, a delay-of-game minor and the draw in its own end in the NHL.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void SecondViolation(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = AtFaceoff(rules);
            List<MatchEvent> ev = EarlyPress(w);
            Player wing = w.Controlled(0).Or(w.Players[0]);
            ev.AddRange(EarlyPress(w));
            List<FaceoffViolated> v = [.. ev.OfType<FaceoffViolated>()];
            Check(
                v.Count == 2 && v[1].PlayerId == wing.Id,
                $"{rules.Name}: two violations, the second by the winger"
            );
            List<PenaltyCalled> called = [.. ev.OfType<PenaltyCalled>()];
            if (rules.FaceoffViolationMinor)
            {
                Check(
                    called.Count == 1
                        && called[0].PlayerId == wing.Id
                        && w.Match.Penalties.Any(p =>
                            p.PlayerId == wing.Id
                            && string.Equals(
                                p.Infraction,
                                Infraction.DelayOfGame,
                                StringComparison.Ordinal
                            )
                        ),
                    "NHL: the second violation is a delay-of-game minor on the winger"
                );
                Vector3 own = FaceoffSpots.EndDot(w.Match, Side.Home, 0);
                Check(
                    w.Match.Phase == MatchPhase.Stopped && MatchFlow.FaceoffDot(w) == own,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"NHL: play stops for a faceoff in home's own end ({MatchFlow.FaceoffDot(w)})"
                    )
                );
            }
            else
            {
                ev.AddRange(Run(w, x => x.Match.Phase == MatchPhase.Play));
                Check(
                    called.Count == 0 && Draw(w).Winner == SideOrNone.Away,
                    "SHL: no penalty; the second violation hands the draw to the visitors"
                );
            }
        }

        /// <summary>AI centres jump the drop now and then where violations are called, never in Arcade.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void AiViolations(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = new WorldBuilder().Rules(rules).Build();
            int violations = 0;
            const int faceoffs = 300;
            for (int i = 0; i < faceoffs; i++)
            {
                MatchFlow.SetUpFaceoff(w, RinkSpec.CentreDot);
                violations += Run(w, x => x.Match.Phase != MatchPhase.Faceoff)
                    .OfType<FaceoffViolated>()
                    .Count();
                // a minor after a second violation: let the box clear the stoppage and carry on
                _ = Run(w, x => x.Match.Phase is not MatchPhase.Stopped);
            }

            string seen = string.Create(
                CultureInfo.InvariantCulture,
                $"{rules.Name}: {violations} AI violations in {faceoffs} faceoffs"
            );
            Check(
                rules.FaceoffViolations ? violations is > 0 and < faceoffs / 5 : violations == 0,
                seen
            );
        }

        /// <summary>Smart dots: the dot is chosen against the club that stopped play; Arcade takes the nearest dot.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void SmartDots(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = new WorldBuilder().Rules(rules).Build();
            SideOrNone home = SideOrNone.Home;
            (Vector3 At, Vector3 Smart)[] cases =
            [
                (new Vector3(-12, 0, 0.5f), new Vector3(-20.7f, 0, 6.7f)), // own zone: stays in
                (new Vector3(12, 0, -0.5f), new Vector3(6.1f, 0, -6.7f)), // attacking zone: comes out
                (new Vector3(4, 0, 1), new Vector3(-6.1f, 0, 6.7f)), // neutral zone: nearer its own goal
            ];
            foreach ((Vector3 at, Vector3 smart) in cases)
            {
                Vector3 dot = FaceoffSpots.After(w, home, at);
                Vector3 want = rules.SmartDots ? smart : MatchFlow.NearestDot(at);
                Check(
                    dot == want,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{rules.Name}: a home stoppage at {at} faces off at {want} (got {dot})"
                    )
                );
            }
            Check(
                FaceoffSpots.After(w, SideOrNone.None, new Vector3(-12, 0, 0.5f))
                    == MatchFlow.NearestDot(new Vector3(-12, 0, 0.5f)),
                $"{rules.Name}: with nobody to blame the nearest dot"
            );
        }

        /// <summary>A home goalie freezing the puck near his blue line: the draw stays in his end under smart dots, the nearest (neutral) dot in Arcade.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void GoalieFreeze(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = new WorldBuilder().Rules(rules).Build();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play);
            Player g = w.OnIce(Side.Home).First(p => p.IsGoalie);
            Vector3 at = new(-12, 0, 0.5f);
            _ = Run(
                w,
                x => x.Match.Phase != MatchPhase.Play,
                x =>
                {
                    g.Position = at;
                    g.Velocity = Vector3.Zero;
                    x.Puck.Carrier = g.Id;
                    x.Puck.LastTouch = g.Id;
                    x.Puck.Position = at;
                    x.Puck.Velocity = Vector3.Zero;
                },
                3
            );
            Vector3 want = rules.SmartDots
                ? new Vector3(-20.7f, 0, 6.7f)
                : new Vector3(-6.1f, 0, 6.7f);
            Check(
                w.Match.Phase == MatchPhase.Stopped && MatchFlow.FaceoffDot(w) == want,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rules.Name}: the freeze faces off at {want} (got {MatchFlow.FaceoffDot(w)})"
                )
            );
        }

        /// <summary>A pass to a winger hanging deep in the zone: intentional offside, the draw in the offender's own end (SHL, NHL);
        /// with the switch off the usual neutral-zone dot; no offside at all in Arcade.</summary>
        [Test]
        [Arguments(RulePreset.Shl, true)]
        [Arguments(RulePreset.Nhl, true)]
        [Arguments(RulePreset.Shl, false)]
        [Arguments(RulePreset.Arcade, true)]
        public void IntentionalOffside(RulePreset preset, bool intentionalRule)
        {
            RuleSet rules = RuleSet.Of(preset) with
            {
                IntentionalOffside = RuleSet.Of(preset).IntentionalOffside && intentionalRule,
            };
            World w = new WorldBuilder()
                .Rules(rules)
                .Setup(s => s with { PeriodSeconds = 600 })
                .Build();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 10);
            float[] z = [-11, -9.5f, 9.5f, 11, 11];
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int own = FaceoffFormation.DefendedEnd(w.Match, side);
                int i = 0;
                foreach (Player p in w.OnIce(side).Where(p => !p.IsGoalie))
                {
                    p.Position = new Vector3(own * 3, 0, z[i++]);
                    p.Velocity = Vector3.Zero;
                }
            }
            Player c = w.OnIce(Side.Home).First(IsCentre);
            Player rw = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.RW);
            rw.Position = new Vector3(19, 0, 4);
            Puck pk = w.Puck;
            pk.Carrier = -1;
            pk.LastTouch = c.Id;
            pk.Position = new Vector3(2, 0, 4);
            pk.Velocity = new Vector3(16, 0, 0);
            List<MatchEvent> ev = Run(w, x => x.Match.Phase != MatchPhase.Play, 3);
            int calls = ev.OfType<OffsideCalled>().Count();
            if (!rules.Offside)
            {
                Check(calls == 0, $"{rules.Name}: no offside is called");
                return;
            }

            Vector3 want = rules.IntentionalOffside
                ? new Vector3(-20.7f, 0, 6.7f)
                : new Vector3(6.1f, 0, 6.7f);
            Check(
                calls == 1 && MatchFlow.FaceoffDot(w) == want,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rules.Name} (intentional offside {rules.IntentionalOffside}): the pass to the hanging winger faces off at {want} (got {MatchFlow.FaceoffDot(w)}, {calls} calls)"
                )
            );
        }
    }
}
