using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Intermission;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Input;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Intermission
{
    /// <summary>
    /// The trailing talk (W126): it opens in the Zamboni break only for a human club behind after period 1
    /// or 2; the stick and shoot answer it; each answer changes the next period's tactics measurably
    /// (push: shorter shifts, more skaters in on the forecheck, riskier passes; stay patient: no pinching,
    /// fewer intercepted passes); and a recorded match with an answer replays step for step.
    /// </summary>
    public sealed class TrailingTalkTests : CheckedTest
    {
        /// <summary>How many seeded stretches each answer's comparison plays.</summary>
        private const int SeedCount = 3;

        /// <summary>SHL and NHL talk (NHL stronger), Arcade does not; the rules page has both rows in the season group.</summary>
        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl.IntermissionTalk
                    && RuleSet.Shl.IntermissionTalkStrength == TalkStrength.Normal,
                "SHL: the talk at normal strength"
            );
            Check(
                RuleSet.Nhl.IntermissionTalk
                    && RuleSet.Nhl.IntermissionTalkStrength == TalkStrength.Strong,
                "NHL: the talk at strong strength"
            );
            Check(!RuleSet.Arcade.IntermissionTalk, "Arcade: no talk");
            Check(
                RuleSet.Arcade.IntermissionTalkScale < 0.01f
                    && MathF.Abs(RuleSet.Shl.IntermissionTalkScale - 1) < 0.01f
                    && RuleSet.Nhl.IntermissionTalkScale > 1
                    && (
                        RuleSet.Shl with
                        {
                            IntermissionTalkStrength = TalkStrength.Mild,
                        }
                    ).IntermissionTalkScale < 1,
                "the scale is 0 off, below 1 mild, 1 normal, above 1 strong"
            );
            foreach (
                string key in new[]
                {
                    nameof(RuleSet.IntermissionTalk),
                    nameof(RuleSet.IntermissionTalkStrength),
                }
            )
            {
                RuleRow row = RuleRows.All.Single(r =>
                    string.Equals(r.Key, key, StringComparison.Ordinal)
                );
                Check(
                    row.Plays
                        && string.Equals(
                            row.Group,
                            Text.Keys.RuleGroupSeason,
                            StringComparison.Ordinal
                        ),
                    $"{key} is a season rule the game plays"
                );
            }
        }

        /// <summary>The talk opens after period 1 and 2 for the human club when it trails, and not when it is
        /// level or ahead, for an AI club, after the last period, before an overtime, or with the rule off.</summary>
        [Test]
        public void OpensOnlyWhenTrailing()
        {
            Check(
                Opens(home: 0, away: 1).Is(s => s == Side.Home),
                "home a goal down after P1: talk"
            );
            Check(!Opens(home: 1, away: 1).HasValue, "level after P1: no talk");
            Check(!Opens(home: 2, away: 1).HasValue, "ahead after P1: no talk");
            Check(
                !Opens(
                    home: 0,
                    away: 1,
                    rules: RuleSet.Shl with
                    {
                        IntermissionTalk = false,
                    }
                ).HasValue,
                "the rule off: no talk"
            );
            Check(
                !Opens(home: 1, away: 0, human: false).HasValue,
                "the AI club a goal down: no talk"
            );

            World world = Make(RuleSet.Shl, human: true);
            world.Match.Score[1] = 1;
            Check(
                TrailingTalk.Trailing(world, 2, intoOvertime: false).Is(s => s == Side.Home),
                "behind after P2: talk"
            );
            Check(
                !TrailingTalk.Trailing(world, 3, intoOvertime: false).HasValue,
                "after P3: no talk"
            );
            Check(
                !TrailingTalk.Trailing(world, 1, intoOvertime: true).HasValue,
                "before an overtime: no talk"
            );

            // played through: a goal down in both breaks, the talk opens in each, for the next period
            World played = Make(RuleSet.Shl, human: true);
            IntermissionSystem system = new(() => false);
            List<int> asked = [];
            Run(
                played,
                [new MatchRules(), new OvertimeRules(Seeds.Dice(1)), system],
                w => w.Match.Period == 3 && w.Match.Phase == MatchPhase.Play,
                400f,
                w =>
                {
                    if (w.Match.Phase == MatchPhase.Play)
                    {
                        w.Match.Score[1] = w.Match.Period; // the visitors score once a period
                    }

                    if (
                        TrailingTalk.Asking(w).HasValue
                        && w.Talk.TryGet(out TalkState s)
                        && !asked.Contains(s.NextPeriod)
                    )
                    {
                        asked.Add(s.NextPeriod);
                    }
                }
            );
            Check(
                asked.SequenceEqual([2, 3]),
                $"the talk opened before periods 2 and 3 ({string.Join(',', asked)})"
            );
        }

        /// <summary>The stick picks and shoot answers, after the guard; the answer holds for the next period only.</summary>
        [Test]
        public void StickAndShootAnswer()
        {
            World world = Make(RuleSet.Shl, human: true);
            Player me = world.Players.First(p => p.Slot == 0);
            IntermissionSystem system = new(() => false);
            ISystem[] systems = [new MatchRules(), new OvertimeRules(Seeds.Dice(1)), system];
            Run(
                world,
                systems,
                w => TrailingTalk.Asking(w).HasValue,
                60f,
                w =>
                {
                    if (w.Match.Phase == MatchPhase.Play)
                    {
                        w.Match.Score[1] = 1;
                    }
                }
            );
            Check(TrailingTalk.Asking(world).HasValue, "the talk is open in the first break");
            me.Command = me.Command with { Shoot = true };
            Run(world, systems, _ => false, 0.1f);
            Check(
                TrailingTalk.Asking(world).HasValue,
                "shoot already held when the talk opened does not answer it"
            );
            me.Command = new PlayerCommand { Move = new Vector2(1, 0) };
            Run(world, systems, _ => false, 0.6f);
            Check(
                world.Talk.TryGet(out TalkState s) && s.Highlight == TalkChoice.Patient,
                "the stick right picks stay patient"
            );
            me.Command = me.Command with { Shoot = true };
            Run(world, systems, _ => false, Game.Step);
            Check(!TrailingTalk.Asking(world).HasValue, "shoot answers it");
            Check(
                TrailingTalk.Choice(world, Side.Home) == TalkChoice.None,
                "not in force during the break"
            );
            me.Command = new PlayerCommand();
            Run(world, systems, w => w.Match.Period == 2 && w.Match.Phase == MatchPhase.Play, 120f);
            Check(
                TrailingTalk.Choice(world, Side.Home) == TalkChoice.Patient
                    && TrailingTalk.Choice(world, Side.Away) == TalkChoice.None,
                "the home club stays patient in period 2"
            );
            Run(world, systems, w => w.Match.Period == 3 && w.Match.Phase == MatchPhase.Play, 200f);
            Check(
                TrailingTalk.Choice(world, Side.Home) == TalkChoice.None,
                "and plays as usual in period 3"
            );
        }

        /// <summary>Each answer's knobs: push forechecks aggressively, changes lines earlier and risks passes;
        /// stay patient drops an aggressive forecheck and weighs a lost pass heavier; strength scales them.</summary>
        [Test]
        public void AnswersChangeTheKnobs()
        {
            World world = Make(RuleSet.Shl, human: true);
            ClubTactics own = world.Setup.Home.Tactics;
            TrailingTalk.Answer(world, Side.Home, TalkChoice.Push, world.Match.Period);
            Check(
                TacticsState.Of(world, Side.Home).Forecheck == Forecheck.Aggressive
                    && TacticsState.Of(world, Side.Away) == world.Setup.Away.Tactics,
                "push: the home club forechecks 2-1-2, the visitors as their file says"
            );
            float push = TrailingTalk.ShiftsEarlier(world, Side.Home);
            Check(
                push > 0.1f && TrailingTalk.TurnoverWeight(world, Side.Home) < 1f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"push: lines change {push:F2} earlier, a lost pass weighs {TrailingTalk.TurnoverWeight(world, Side.Home):F2}"
                )
            );
            TrailingTalk.Answer(world, Side.Home, TalkChoice.Patient, world.Match.Period);
            Check(
                TacticsState.Of(world, Side.Home).Forecheck
                    == (own.Forecheck == Forecheck.Aggressive ? Forecheck.Balanced : own.Forecheck)
                    && TrailingTalk.ShiftsEarlier(world, Side.Home) == 0
                    && TrailingTalk.TurnoverWeight(world, Side.Home) > 2f,
                "patient: no aggressive forecheck, usual shifts, a lost pass weighs far more"
            );

            World strong = Make(RuleSet.Nhl, human: true);
            TrailingTalk.Answer(strong, Side.Home, TalkChoice.Push, strong.Match.Period);
            Check(
                TrailingTalk.ShiftsEarlier(strong, Side.Home) > push,
                "strong talk shortens the shifts more"
            );
        }

        /// <summary>Played out with the same seed, a pushing club changes lines more often and sends more skaters
        /// in on the forecheck than with no talk, and a patient one has fewer passes picked off.</summary>
        [Test]
        public void AnswersChangeThePeriod()
        {
            Tally none = PlayPeriod(TalkChoice.None);
            Tally push = PlayPeriod(TalkChoice.Push);
            Tally patient = PlayPeriod(TalkChoice.Patient);
            Console.WriteLine($"      none: {none}");
            Console.WriteLine($"      push: {push}");
            Console.WriteLine($"      patient: {patient}");
            Check(
                push.Changes > none.Changes,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"push: more line changes ({push.Changes} against {none.Changes})"
                )
            );
            Check(
                push.Forecheckers > none.Forecheckers + 0.2f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"push: more skaters in on the forecheck ({push.Forecheckers:F2} against {none.Forecheckers:F2})"
                )
            );
            Check(
                patient.InterceptedShare < none.InterceptedShare,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"patient: a smaller share of passes picked off ({patient.InterceptedShare:F2} against {none.InterceptedShare:F2})"
                )
            );
        }

        /// <summary>A match recorded with the keyboard answering the talk (stick right, space) writes the frames to
        /// its replay file, and playing that file's frames back gives the same answer and the same world
        /// checksum at every step; without the frames the period plays differently.</summary>
        [Test]
        [NotInParallel]
        public void ReplayWithAnAnswerVerifies()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"sap-talk-{Environment.ProcessId}.sapreplay"
                )
            );
            const int Steps = 3600;
            try
            {
                Dictionary<long, InputFrame> keys = new()
                {
                    [100] = Frame([(int)KeyboardKey.Right], []),
                    [120] = Frame(
                        [(int)KeyboardKey.Right, (int)KeyboardKey.Space],
                        [(int)KeyboardKey.Space]
                    ),
                    [122] = Frame([(int)KeyboardKey.Right], []),
                    [130] = InputFrame.Empty,
                };
                DeviceInput.Play(t =>
                    keys.TryGetValue(t, out InputFrame f) ? f : Maybe.None<InputFrame>()
                );
                ReplayRecorder recorder = ReplayRecorder.Begin(
                    new ReplayHeader { Seed = 7 },
                    InputFrame.Empty,
                    path
                );
                (List<ulong> recorded, TalkChoice answered) = PlayAnswered(Steps, recorder);
                DeviceInput.Reset();
                ReplayFile file = ReplayFile.Read(path);
                Check(
                    file.Inputs.Values.Any(f => f.Pressed.Contains((int)KeyboardKey.Space)),
                    "the replay file holds the answering press"
                );
                Check(
                    answered == TalkChoice.Patient,
                    $"the recorded match stays patient ({answered})"
                );

                DeviceInput.Play(t =>
                    file.Inputs.TryGetValue(t, out InputFrame f) ? f : Maybe.None<InputFrame>()
                );
                (List<ulong> replayed, TalkChoice again) = PlayAnswered(
                    Steps,
                    Maybe.None<ReplayRecorder>()
                );
                DeviceInput.Reset();
                int same = recorded.Zip(replayed).TakeWhile(p => p.First == p.Second).Count();
                Check(
                    again == TalkChoice.Patient && same == Steps,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the replay answers the same and agrees at every one of {Steps} steps ({same})"
                    )
                );
                Check(
                    file.Checks.All(c => c.Key >= Steps || replayed[(int)c.Key] == c.Value),
                    "and matches every checksum the file wrote"
                );

                (List<ulong> plain, TalkChoice none) = PlayAnswered(
                    Steps,
                    Maybe.None<ReplayRecorder>()
                );
                Check(
                    none == TalkChoice.None && plain.Zip(recorded).Any(p => p.First != p.Second),
                    "unanswered, the match plays differently"
                );
            }
            finally
            {
                DeviceInput.Reset();
                File.Delete(path);
            }
        }

        private static InputFrame Frame(int[] held, int[] pressed)
        {
            return new InputFrame(
                held,
                pressed,
                0,
                [PadFrame.None, PadFrame.None, PadFrame.None, PadFrame.None],
                ""
            );
        }

        /// <summary>Plays the talk scene's opening: the first break, home a goal down with the keyboard on the
        /// centre, into the second period, through every system the answer reaches.</summary>
        private static (List<ulong> Sums, TalkChoice Answer) PlayAnswered(
            int steps,
            Maybe<ReplayRecorder> recorder
        )
        {
            World world = new WorldBuilder()
                .Human()
                .Seed(7)
                .Setup(s =>
                    s with
                    {
                        PeriodSeconds = 60,
                        Rules = RuleSet.Shl with { Intermission = IntermissionLength.Quick },
                    }
                )
                .Then(Break)
                .Build();
            List<ISystem> systems =
            [
                new InputSystem(),
                .. Systems(world, 7),
                new IntermissionSystem(() => false),
            ];
            List<ulong> sums = [with(steps)];
            TalkChoice answer = TalkChoice.None;
            for (int i = 0; i < steps; i++)
            {
                Game.StepWorld(world, systems);
                ulong sum = WorldChecksum.Of(world);
                sums.Add(sum);
                if (recorder.TryGet(out ReplayRecorder r))
                {
                    r.AfterStep(world, sum);
                }

                if (
                    world.Match.Period == 2
                    && TrailingTalk.Choice(world, Side.Home) != TalkChoice.None
                )
                {
                    answer = TrailingTalk.Choice(world, Side.Home);
                }
            }

            if (recorder.TryGet(out ReplayRecorder done))
            {
                done.Finish(world);
            }

            return (sums, answer);
        }

        /// <summary>Puts a world in the first intermission with the visitors a goal up, as the talk scene does.</summary>
        private static void Break(World world)
        {
            world.Match.Phase = MatchPhase.Intermission;
            world.Match.Clock = 0;
            world.Match.Score[1] = 1;
            world.Puck.Live = false;
            MatchFlow.State(world).Timer = 4f;
        }

        /// <summary>Plays <see cref="SeedCount"/> ten-minute stretches of an all-AI first period, the home club on a
        /// balanced forecheck with the answer in force, and sums the home club's tallies.</summary>
        private static Tally PlayPeriod(TalkChoice choice)
        {
            int changes = 0,
                passes = 0,
                intercepted = 0;
            float breakout = 0,
                deep = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                World world = new WorldBuilder()
                    .Setup(s =>
                        s with
                        {
                            Home = s.Home with { Tactics = new ClubTactics() },
                            Away = s.Away with { Tactics = new ClubTactics() },
                            PeriodSeconds = 20 * 60,
                            Inputs =
                            [
                                new InputSlot(InputDevice.None, Side.Home),
                                new InputSlot(InputDevice.None, Side.Away),
                            ],
                        }
                    )
                    .Seed(20 + seed)
                    .Build();
                TrailingTalk.Answer(world, Side.Home, choice, 1);
                List<ISystem> systems = Systems(world, 20 + seed);
                for (int i = 0; i < 10 * 60 * 60; i++)
                {
                    Game.StepWorld(world, systems);
                    changes += world
                        .Events.Current.OfType<LineChanged>()
                        .Count(e => e.Side == Side.Home);
                }

                DecisionTally d = DecisionState.Tally(world, Side.Home);
                TacticsTally t = TacticsState.Tally(world, Side.Home);
                passes += d.Passes;
                intercepted += d.Intercepted;
                breakout += t.BreakoutSeconds;
                deep += t.DeepSkaterSeconds;
            }

            return new Tally(
                changes,
                breakout <= 0 ? 0f : deep / breakout,
                passes,
                passes == 0 ? 0f : (float)intercepted / passes
            );
        }

        /// <summary>The match systems the talk reaches, seeded, started.</summary>
        private static List<ISystem> Systems(World world, int seed)
        {
            List<ISystem> systems =
            [
                new PositioningSystem(Seeds.Dice(seed, "positioning", PositioningSystem.Fallback)),
                new DefenceSystem(Seeds.Dice(seed, "defence", DefenceSystem.Fallback)),
                new SpecialTeamsSystem(
                    Seeds.Dice(seed, "specialteams", SpecialTeamsSystem.Fallback)
                ),
                new TacticsSystem(Seeds.Dice(seed, "tactics", TacticsSystem.Fallback)),
                new DecisionSystem(Seeds.Dice(seed, "decisions", DecisionSystem.Fallback)),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(seed, "checking", CheckingSystem.Fallback)),
                new PuckHandlingSystem(
                    Seeds.Dice(seed, "puckhandling", PuckHandlingSystem.Fallback)
                ),
                new LinesSystem(),
                new GoalieSystem { Dice = Seeds.Dice(seed, "goalie", GoalieSystem.Fallback) },
                new PuckPhysics(),
                new MatchRules(),
                new OffsideRule(),
                new IcingRule(),
                new OvertimeRules(Seeds.Dice(seed, "overtime", OvertimeRules.Fallback)),
            ];
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return systems;
        }

        private static Maybe<Side> Opens(int home, int away, bool human = true)
        {
            return Opens(home, away, RuleSet.Shl, human);
        }

        private static Maybe<Side> Opens(int home, int away, RuleSet rules, bool human = true)
        {
            World world = Make(rules, human);
            IntermissionSystem system = new(() => false);
            Maybe<Side> asked = Maybe.None<Side>();
            Run(
                world,
                [new MatchRules(), new OvertimeRules(Seeds.Dice(1)), system],
                w => w.Match.Period == 2,
                120f,
                w =>
                {
                    if (w.Match.Phase == MatchPhase.Play)
                    {
                        w.Match.Score[0] = home;
                        w.Match.Score[1] = away;
                    }

                    if (TrailingTalk.Asking(w).TryGet(out Side s))
                    {
                        asked = s;
                    }
                }
            );
            return asked;
        }

        /// <summary>A match of 3-second periods by the given rules, the keyboard on the home centre or nobody.</summary>
        private static World Make(RuleSet rules, bool human)
        {
            WorldBuilder builder = new WorldBuilder().Setup(s =>
                s with
                {
                    PeriodSeconds = 3,
                    Overtime = OvertimeRule.None,
                    Rules = rules,
                }
            );
            return (human ? builder.Human() : builder).Build();
        }

        private static void Run(
            World world,
            IReadOnlyList<ISystem> systems,
            Func<World, bool> until,
            float limit
        )
        {
            Run(world, systems, until, limit, _ => { });
        }

        private static void Run(
            World world,
            IReadOnlyList<ISystem> systems,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
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

        private sealed record Tally(
            int Changes,
            float Forecheckers,
            int Passes,
            float InterceptedShare
        )
        {
            public override string ToString()
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Changes} line changes, {Forecheckers:F2} forecheckers, {Passes} passes, {InterceptedShare:P0} picked off"
                );
            }
        }
    }
}
