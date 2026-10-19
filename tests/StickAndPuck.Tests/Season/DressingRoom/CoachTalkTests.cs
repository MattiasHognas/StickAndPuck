using System.Globalization;
using System.Text.Json;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Momentum;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Season.DressingRoom
{
    /// <summary>
    /// The coach's pre-game talk (W125): the rule presets, each tone's effect on the match setup (penalty
    /// calls, hits, pass accuracy and the momentum head start, only for the club that heard it, scaled by
    /// the talk strength and gone with the rule off), the dressing room's PLAY NEXT GAME opening the talk
    /// and the picked tone riding on the started match's setup, the coach's opening line, and the replay
    /// keeping the talk so it plays the same match.
    /// </summary>
    public sealed class CoachTalkTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-coachtalk-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl is { PreGameTalk: true, TalkStrength: TalkStrength.Normal }
                    && RuleSet.Nhl is { PreGameTalk: true, TalkStrength: TalkStrength.Normal }
                    && RuleSet.Arcade is { PreGameTalk: false },
                "SHL and NHL give the pre-game talk at normal strength, Arcade none"
            );
            Check(
                RuleRows.Shown.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.PreGameTalk), StringComparison.Ordinal)
                    && string.Equals(r.Group, K.RuleGroupSeason, StringComparison.Ordinal)
                )
                    && RuleRows.Shown.Any(r =>
                        string.Equals(r.Key, nameof(RuleSet.TalkStrength), StringComparison.Ordinal)
                    ),
                "the talk and its strength are rows in the rules page's season group"
            );
            Check(
                Near(RuleSet.Shl.TalkScale, 1f)
                    && Near((RuleSet.Shl with { TalkStrength = TalkStrength.Mild }).TalkScale, 0.5f)
                    && Near(
                        (RuleSet.Shl with { TalkStrength = TalkStrength.Strong }).TalkScale,
                        1.5f
                    )
                    && Near((RuleSet.Shl with { PreGameTalk = false }).TalkScale, 0f),
                "the strength scales the talk by a half, one or one and a half; off is none"
            );
        }

        [Test]
        public void EachToneOnTheSetup()
        {
            MatchSetup none = WorldBuilder.DefaultSetup.WithRules(RuleSet.Shl);
            MatchSetup calm = Talk(none, TalkTone.Calm),
                fire = Talk(none, TalkTone.FireUp),
                discipline = Talk(none, TalkTone.Discipline);

            Check(
                Near(CoachTalk.PenaltyScale(none, Side.Home), 1f)
                    && Near(CoachTalk.HitScale(none, Side.Home), 1f)
                    && CoachTalk.PassIq(none, Side.Home) == 0
                    && Near(CoachTalk.MomentumStart(none, Side.Home), 0f),
                "without a talk nothing changes"
            );
            Check(
                CoachTalk.PenaltyScale(calm, Side.Home) < 1f
                    && CoachTalk.PassIq(calm, Side.Home) == CoachTalk.CalmPassIq
                    && Near(CoachTalk.HitScale(calm, Side.Home), 1f)
                    && Near(CoachTalk.MomentumStart(calm, Side.Home), 0f),
                S(
                    $"calm: steadier passes (+{CoachTalk.PassIq(calm, Side.Home)} sense) and fewer calls (x{CoachTalk.PenaltyScale(calm, Side.Home):0.00})"
                )
            );
            Check(
                Near(CoachTalk.MomentumStart(fire, Side.Home), CoachTalk.FireUpMomentum)
                    && CoachTalk.HitScale(fire, Side.Home) > 1f
                    && CoachTalk.PenaltyScale(fire, Side.Home) > 1f
                    && CoachTalk.PassIq(fire, Side.Home) == 0,
                S(
                    $"fire up: a head start ({CoachTalk.MomentumStart(fire, Side.Home):0.00}), more hits (x{CoachTalk.HitScale(fire, Side.Home):0.00}) and more calls (x{CoachTalk.PenaltyScale(fire, Side.Home):0.00})"
                )
            );
            Check(
                CoachTalk.PenaltyScale(discipline, Side.Home)
                    < CoachTalk.PenaltyScale(calm, Side.Home)
                    && CoachTalk.HitScale(discipline, Side.Home) < 1f
                    && Near(CoachTalk.MomentumStart(discipline, Side.Home), 0f),
                S(
                    $"discipline: the fewest calls (x{CoachTalk.PenaltyScale(discipline, Side.Home):0.00}) and a little less aggression (x{CoachTalk.HitScale(discipline, Side.Home):0.00})"
                )
            );
            Check(
                new[] { calm, fire, discipline }.All(s =>
                    Near(CoachTalk.PenaltyScale(s, Side.Away), 1f)
                    && Near(CoachTalk.HitScale(s, Side.Away), 1f)
                    && CoachTalk.PassIq(s, Side.Away) == 0
                    && Near(CoachTalk.MomentumStart(s, Side.Away), 0f)
                ),
                "only the club that heard the talk plays it"
            );

            MatchSetup strong = fire with
            {
                Rules = RuleSet.Shl with { TalkStrength = TalkStrength.Strong },
            };
            MatchSetup mild = fire with
            {
                Rules = RuleSet.Shl with { TalkStrength = TalkStrength.Mild },
            };
            MatchSetup off = fire with { Rules = RuleSet.Shl with { PreGameTalk = false } };
            Check(
                CoachTalk.HitScale(strong, Side.Home) > CoachTalk.HitScale(fire, Side.Home)
                    && CoachTalk.HitScale(mild, Side.Home) < CoachTalk.HitScale(fire, Side.Home)
                    && CoachTalk.HitScale(mild, Side.Home) > 1f,
                "a stronger talk counts more, a mild one less"
            );
            Check(
                Near(CoachTalk.HitScale(off, Side.Home), 1f)
                    && Near(CoachTalk.PenaltyScale(off, Side.Home), 1f)
                    && Near(CoachTalk.MomentumStart(off, Side.Home), 0f),
                "with the rule off a talk on the setup does nothing"
            );

            World world = new WorldBuilder().Setup(_ => fire).Build();
            _ = MomentumMeter.State(world);
            Check(
                Math.Abs(MomentumMeter.Of(world, Side.Home) - CoachTalk.FireUpMomentum) < 0.001f
                    && Near(MomentumMeter.Of(world, Side.Away), 0f),
                S(
                    $"the fired-up club starts the match with a boost ({MomentumMeter.Of(world, Side.Home):0.00})"
                )
            );
        }

        [Test]
        public void CalmSteadiesThePasses()
        {
            // the passer's sense sets the spread: the calm talk's added sense narrows it
            Random a = new(7),
                b = new(7);
            float plain = 0f,
                steady = 0f;
            System.Numerics.Vector3 from = new(0, 0, 0),
                to = new(10, 0, 0);
            for (int i = 0; i < 400; i++)
            {
                plain += MathF.Abs(PuckHandling.PassVelocity(from, to, 12f, 40, a).Z);
                steady += MathF.Abs(
                    PuckHandling.PassVelocity(from, to, 12f, 40 + CoachTalk.CalmPassIq, b).Z
                );
            }

            Check(
                steady < plain * 0.9f,
                S($"calm passes stray less ({steady / 400:0.000} against {plain / 400:0.000} m/s)")
            );
        }

        [Test]
        public void DressingRoomOpensTheTalk()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 5,
                path: Path.Combine(folder, "season.json")
            );
            app.Screens.Push(new DressingRoomScreen(app));
            Pick(app);
            Check(
                app.Screens.Top.Is(t => t is CoachTalkScreen),
                "PLAY NEXT GAME in the dressing room opens the pre-game talk"
            );
            CoachTalkScreen talk = app.Screens.Top.TryGet(out Screen top)
                ? (CoachTalkScreen)top
                : throw new InvalidOperationException("no talk screen");
            Check(
                talk.Items().Count == 3
                    && CoachTalkScreen.Tones.SequenceEqual([
                        TalkTone.Calm,
                        TalkTone.FireUp,
                        TalkTone.Discipline,
                    ]),
                "the talk offers three tones: calm, fire up, discipline"
            );
            Check(
                CoachTalkScreen.Mood(TalkTone.Discipline) == CoachMood.Angry
                    && CoachTalkScreen.Mood(TalkTone.FireUp) == CoachMood.Pleased
                    && CoachTalkScreen.Mood(TalkTone.Calm) == CoachMood.Neutral,
                "the coach's face follows the tone: shouting for discipline, a grin for firing up"
            );

            Fixture game = season.NextGame.TryGet(out Fixture g)
                ? g
                : throw new InvalidOperationException("expected a next game");
            string opponent = string.Equals(game.Home, season.UserClub, StringComparison.Ordinal)
                ? game.Away
                : game.Home;
            DressingRoomContext context = DressingRoomContext.Of(season, clubs, RuleSet.Shl);
            IReadOnlyList<DressingRoomEntry> said = PreGameTalkSources.Build(context).All;
            Check(
                said.Count >= 1
                    && said[0].Text.Contains(context.Name(opponent), StringComparison.Ordinal),
                $"the coach opens with tonight's opponent ({(said.Count > 0 ? said[0].Text : "nothing")})"
            );

            talk.Items()[1].Pick(); // FIRE THEM UP
            Side user = string.Equals(game.Home, season.UserClub, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Check(
                app.Screens.Started.TryGet(out MatchSetup started)
                    && started.Talk == new CoachTalk(user, TalkTone.FireUp),
                "the picked tone goes on the started match's setup for the human's club"
            );
            Check(
                app.Screens.TakeNextWorld().Is(w => w.Setup.Talk.HasValue),
                "the match plays with the talk"
            );
        }

        [Test]
        public void TalkOffPlaysStraightAway()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 5,
                path: Path.Combine(folder, "season.json")
            );
            season.Rules = RuleSet.Shl with { PreGameTalk = false };
            app.Screens.Push(new DressingRoomScreen(app));
            Pick(app);
            Check(
                !app.Screens.Top.HasValue && app.Screens.Started.Is(s => !s.Talk.HasValue),
                "with the talk off PLAY NEXT GAME starts the game without one"
            );
        }

        [Test]
        public void ReplayKeepsTheTalk()
        {
            MatchSetup setup = Talk(
                WorldBuilder.DefaultSetup.WithRules(RuleSet.Shl),
                TalkTone.Calm
            );
            string json = JsonSerializer.Serialize(
                ReplaySetup.Of(setup),
                ReplayJsonContext.Default.ReplaySetup
            );
            ReplaySetup read =
                JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplaySetup)
                ?? throw new InvalidOperationException("replay setup did not read");
            Check(
                read.ToSetup(WorldBuilder.Clubs).Talk == setup.Talk,
                S(
                    $"the replay keeps the talk ({json[json.IndexOf("\"talk\"", StringComparison.Ordinal)..]})"
                )
            );
            ReplaySetup old =
                JsonSerializer.Deserialize(
                    json.Replace("\"talk\":", "\"unused\":", StringComparison.Ordinal),
                    ReplayJsonContext.Default.ReplaySetup
                ) ?? throw new InvalidOperationException("old replay setup did not read");
            Check(!old.Talk.HasValue, "an older replay without a talk reads with none");

            // the talk is part of the match: the same talk replays the same match, another plays a different one
            const int Steps = 6000;
            List<ulong> calm = Play(setup, Steps),
                again = Play(read.ToSetup(WorldBuilder.Clubs), Steps),
                plain = Play(setup with { Talk = Maybe.None<CoachTalk>() }, Steps);
            Check(
                calm.SequenceEqual(again),
                S($"the replayed setup agrees on every one of {Steps} steps")
            );
            Check(
                calm.Zip(plain).Any(p => p.First != p.Second),
                "without the talk the match plays differently"
            );
        }

        private static void Pick(AppState app)
        {
            // PLAY NEXT GAME on the dressing room on top
            DressingRoomScreen room = app.Screens.Top.TryGet(out Screen top)
                ? (DressingRoomScreen)top
                : throw new InvalidOperationException("no dressing room");
            room.Items()[0].Pick();
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.0001f;
        }

        private static MatchSetup Talk(MatchSetup setup, TalkTone tone)
        {
            return setup with { Talk = new CoachTalk(Side.Home, tone) };
        }

        private static List<ulong> Play(MatchSetup setup, int steps)
        {
            const int Seed = 41;
            World world = new WorldBuilder()
                .Setup(_ =>
                    setup with
                    {
                        PeriodSeconds = 120,
                        Inputs =
                        [
                            new InputSlot(InputDevice.None, Side.Home),
                            new InputSlot(InputDevice.None, Side.Away),
                        ],
                    }
                )
                .Seed(Seed)
                .Build();
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(Seed, "positioning", PositioningSystem.Fallback)),
                new DefenceSystem(Seeds.Dice(Seed, "defence", DefenceSystem.Fallback)),
                new SpecialTeamsSystem(
                    Seeds.Dice(Seed, "specialteams", SpecialTeamsSystem.Fallback)
                ),
                new TacticsSystem(Seeds.Dice(Seed, "tactics", TacticsSystem.Fallback)),
                new DecisionSystem(Seeds.Dice(Seed, "decisions", DecisionSystem.Fallback)),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(Seed, "checking", CheckingSystem.Fallback)),
                new PuckHandlingSystem(
                    Seeds.Dice(Seed, "puckhandling", PuckHandlingSystem.Fallback)
                ),
                new GoalieSystem { Dice = Seeds.Dice(Seed, "goalie", GoalieSystem.Fallback) },
                new PuckPhysics(),
                new MatchRules(),
                new OffsideRule(),
                new IcingRule(),
                new OvertimeRules(Seeds.Dice(Seed, "overtime", OvertimeRules.Fallback)),
                new PenaltyRules(),
            ];
            foreach (
                ISystem s in systems.Where(s =>
                    s is not (PuckPhysics or GoalieSystem or PenaltyRules)
                )
            )
            {
                s.Start(world);
            }

            List<ulong> sums = [with(steps)];
            for (int i = 0; i < steps; i++)
            {
                Game.StepWorld(world, systems);
                sums.Add(WorldChecksum.Of(world));
            }

            foreach (IDisposable s in systems.OfType<IDisposable>())
            {
                s.Dispose();
            }

            return sums;
        }

        private static string S(FormattableString text)
        {
            return text.ToString(CultureInfo.InvariantCulture);
        }
    }
}
