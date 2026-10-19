using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.DressingRoom.Dev;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.DressingRoom
{
    /// <summary>
    /// The coach's post-game reaction (W127): the right situation picked from crafted game sheets (a hat
    /// trick, a comeback, a blown lead, a shutout, an overtime winner, a heavy loss, a hot goalie, a lost
    /// or won fight, penalty trouble, nothing) with the player named and the coach's face; the temper's
    /// thresholds; the line in the dressing room over the placeholder, the same every time, and gone with
    /// the rule off; the played sheet surviving the save; and the tracker keeping leads, penalties, fights
    /// and the overtime and shootout deciders.
    /// </summary>
    public sealed class PostGameReactionTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-reaction-{Environment.ProcessId}-{Guid.NewGuid():N}"
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

        private static MatchStatLine Line(
            Side side,
            string name,
            PlayingPosition position = PlayingPosition.C
        )
        {
            return new MatchStatLine
            {
                Club = side == Side.Home ? "a" : "b",
                Side = side,
                Name = name,
                Position = position,
                Played = true,
            };
        }

        /// <summary>A game of "a" (home) against "b": both clubs dressed with three skaters and a goalie,
        /// the lines changed by <paramref name="shape"/>.</summary>
        private static (LastGame Game, MatchStatSheet Sheet) Crafted(
            int home,
            int away,
            Action<MatchStatSheet, List<MatchStatLine>> shape,
            ResultKind kind = ResultKind.Regulation,
            string user = "a"
        )
        {
            List<MatchStatLine> lines =
            [
                Line(Side.Home, "Lindqvist"),
                Line(Side.Home, "Berg", PlayingPosition.D),
                Line(Side.Home, "Forsberg", PlayingPosition.LW),
                Line(Side.Home, "Lundqvist", PlayingPosition.G),
                Line(Side.Away, "Ovechkin"),
                Line(Side.Away, "Chara", PlayingPosition.D),
                Line(Side.Away, "Selanne", PlayingPosition.RW),
                Line(Side.Away, "Hasek", PlayingPosition.G),
            ];
            MatchStatSheet sheet = new()
            {
                Home = "a",
                Away = "b",
                HomeGoals = home,
                AwayGoals = away,
                Lines = lines,
            };
            shape(sheet, lines);
            return (
                new LastGame("a", "b", new FixtureResult(home, away, kind), Playoff: false, user),
                sheet
            );
        }

        private static MatchStatLine Named(MatchStatSheet sheet, string name)
        {
            return sheet.Lines.First(l => string.Equals(l.Name, name, StringComparison.Ordinal));
        }

        private void Expect(
            (LastGame Game, MatchStatSheet Sheet) g,
            ReactionKind kind,
            string player,
            CoachMood mood,
            string what,
            CoachTemper temper = CoachTemper.Normal
        )
        {
            Maybe<Reaction> picked = PostGameReaction.Pick(g.Game, g.Sheet, temper);
            Check(
                picked.Is(r =>
                    r.Kind == kind
                    && string.Equals(r.Player, player, StringComparison.Ordinal)
                    && r.Mood == mood
                ),
                $"{what}: {kind} naming {player}, {mood} (got {picked.Select(r => $"{r.Kind} {r.Player} {r.Mood}").Or("nothing")})"
            );
        }

        private void ExpectNone(
            (LastGame Game, MatchStatSheet Sheet) g,
            string what,
            CoachTemper temper
        )
        {
            Maybe<Reaction> picked = PostGameReaction.Pick(g.Game, g.Sheet, temper);
            Check(
                !picked.HasValue,
                $"{what}: no reaction (got {picked.Select(r => $"{r.Kind} {r.Player}").Or("nothing")})"
            );
        }

        [Test]
        public void GoodNights()
        {
            Expect(
                Crafted(4, 2, (s, _) => Named(s, "Lindqvist").Goals = 3),
                ReactionKind.HatTrick,
                "Lindqvist",
                CoachMood.Pleased,
                "a hat trick in a win"
            );
            Expect(
                Crafted(3, 5, (s, _) => Named(s, "Forsberg").Goals = 3),
                ReactionKind.HatTrick,
                "Forsberg",
                CoachMood.Neutral,
                "a hat trick in a loss"
            );
            Expect(
                Crafted(2, 3, (s, _) => Named(s, "Ovechkin").Goals = 3, user: "b"),
                ReactionKind.HatTrick,
                "Ovechkin",
                CoachMood.Pleased,
                "the human's club away: its own hat trick"
            );
            Expect(
                Crafted(
                    2,
                    0,
                    (s, _) =>
                    {
                        Named(s, "Lundqvist").Saves = 28;
                        Named(s, "Hasek").Saves = 30;
                    }
                ),
                ReactionKind.Shutout,
                "Lundqvist",
                CoachMood.Pleased,
                "a shutout names our goalie"
            );
        }

        [Test]
        public void LeadsAndExtraTime()
        {
            Expect(
                Crafted(
                    3,
                    4,
                    (s, _) =>
                    {
                        s.HomeMaxLead = 2;
                        Named(s, "Berg").PlusMinus = -2;
                        Named(s, "Lindqvist").PlusMinus = 1;
                    }
                ),
                ReactionKind.BlownLead,
                "Berg",
                CoachMood.Angry,
                "a two-goal lead blown names the worst plus/minus"
            );
            Expect(
                Crafted(
                    4,
                    3,
                    (s, _) =>
                    {
                        s.AwayMaxLead = 2;
                        Named(s, "Forsberg").Goals = 1;
                        Named(s, "Forsberg").Assists = 2;
                        Named(s, "Lindqvist").Goals = 2;
                    }
                ),
                ReactionKind.Comeback,
                "Forsberg",
                CoachMood.Pleased,
                "a win from two down names the top scorer"
            );
            Expect(
                Crafted(3, 2, (s, _) => Named(s, "Berg").Decider = true, ResultKind.Overtime),
                ReactionKind.Decider,
                "Berg",
                CoachMood.Pleased,
                "an overtime winner"
            );
            Expect(
                Crafted(3, 2, (s, _) => Named(s, "Forsberg").Decider = true, ResultKind.Shootout),
                ReactionKind.Decider,
                "Forsberg",
                CoachMood.Pleased,
                "a shootout winner"
            );
        }

        [Test]
        public void LossesAndFights()
        {
            Expect(
                Crafted(0, 5, (s, _) => Named(s, "Forsberg").PlusMinus = -4),
                ReactionKind.HeavyLoss,
                "Forsberg",
                CoachMood.Angry,
                "a five-goal loss"
            );
            Expect(
                Crafted(1, 2, (s, _) => Named(s, "Lundqvist").Saves = 38),
                ReactionKind.HotGoalie,
                "Lundqvist",
                CoachMood.Neutral,
                "38 saves in a one-goal loss"
            );
            Expect(
                Crafted(2, 3, (s, _) => Named(s, "Berg").FightsLost = 1),
                ReactionKind.LostFight,
                "Berg",
                CoachMood.Angry,
                "a lost fight in a loss"
            );
            Expect(
                Crafted(
                    2,
                    3,
                    (s, _) =>
                    {
                        Named(s, "Lindqvist").Penalties = 1;
                        Named(s, "Berg").Penalties = 3;
                        Named(s, "Forsberg").Penalties = 1;
                    }
                ),
                ReactionKind.PenaltyTrouble,
                "Berg",
                CoachMood.Angry,
                "five penalties name the most boxed player"
            );
            Expect(
                Crafted(2, 1, (s, _) => Named(s, "Forsberg").FightsWon = 1),
                ReactionKind.WonFight,
                "Forsberg",
                CoachMood.Pleased,
                "a won fight"
            );
            ExpectNone(
                Crafted(2, 1, (s, _) => Named(s, "Ovechkin").Goals = 3),
                "the other club's hat trick and nothing of ours",
                CoachTemper.Normal
            );
            Expect(
                Crafted(
                    1,
                    4,
                    (s, _) =>
                    {
                        s.HomeMaxLead = 1;
                        Named(s, "Lindqvist").Goals = 3;
                        Named(s, "Berg").FightsLost = 1;
                    }
                ),
                ReactionKind.HatTrick,
                "Lindqvist",
                CoachMood.Neutral,
                "a hat trick beats a lost fight"
            );
        }

        [Test]
        public void TemperSetsTheThresholds()
        {
            (LastGame, MatchStatSheet) blown = Crafted(2, 3, (s, _) => s.HomeMaxLead = 2);
            Expect(
                blown,
                ReactionKind.BlownLead,
                "Lindqvist",
                CoachMood.Angry,
                "normal: two up is a lead blown"
            );
            ExpectNone(blown, "mild: two up is not enough", CoachTemper.Mild);

            (LastGame, MatchStatSheet) loss = Crafted(1, 4, (_, _) => { });
            ExpectNone(loss, "normal: a three-goal loss is no heavy loss", CoachTemper.Normal);
            Expect(
                loss,
                ReactionKind.HeavyLoss,
                "Lindqvist",
                CoachMood.Angry,
                "strong: a three-goal loss is",
                CoachTemper.Strong
            );

            (LastGame, MatchStatSheet) saves = Crafted(
                2,
                1,
                (s, _) => Named(s, "Lundqvist").Saves = 32
            );
            ExpectNone(saves, "normal: 32 saves is an ordinary night", CoachTemper.Normal);
            Expect(
                saves,
                ReactionKind.HotGoalie,
                "Lundqvist",
                CoachMood.Pleased,
                "strong: 32 saves in a win",
                CoachTemper.Strong
            );
            Check(
                RuleSet.Shl.CoachReaction
                    && RuleSet.Shl.CoachTemper == CoachTemper.Normal
                    && RuleSet.Nhl.CoachReaction
                    && RuleSet.Nhl.CoachTemper == CoachTemper.Strong
                    && !RuleSet.Arcade.CoachReaction,
                "SHL reacts at normal temper, NHL strong, Arcade not at all"
            );
        }

        [Test]
        public void VariantsAreFixedPerGame()
        {
            HashSet<int> seen = [];
            for (int g = 0; g < 40; g++)
            {
                string key = SeasonStats.GameKey(g);
                int v = PostGameReaction.Variant(7, key, ReactionKind.HatTrick);
                Check(
                    v is >= 0 and < PostGameReaction.Variants
                        && v == PostGameReaction.Variant(7, key, ReactionKind.HatTrick),
                    string.Create(CultureInfo.InvariantCulture, $"game {key} always gets line {v}")
                );
                _ = seen.Add(v);
            }

            Check(seen.Count == PostGameReaction.Variants, "every line comes up over a season");
            foreach (ReactionKind kind in Enum.GetValues<ReactionKind>())
            {
                for (int v = 0; v < PostGameReaction.Variants; v++)
                {
                    string line = Text.Format(PostGameReaction.Key(kind, v), "Zetterberg", 4);
                    Check(
                        line.Contains("Zetterberg", StringComparison.Ordinal),
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{kind} line {v} names the player: {line}"
                        )
                    );
                }
            }
        }

        private SeasonState Season(out IReadOnlyList<ClubData> clubs)
        {
            clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[1].Id,
                seed: 11,
                path: Path.Combine(folder, "season.json")
            );
            for (int i = 0; i < 3; i++)
            {
                _ = app.Season.SimCurrentGame();
            }

            return season;
        }

        [Test]
        public void InTheDressingRoom()
        {
            SeasonState season = Season(out IReadOnlyList<ClubData> clubs);
            Check(StagedReaction.Stage(season, clubs, "hattrick"), "the hat trick is staged");
            string star = season
                .Stats.LastPlayed.Select(s => s.Lines.First(l => l.Goals == 3).Name)
                .Or("");
            DressingRoomContext context = DressingRoomContext.Of(season, clubs, RuleSet.Shl);
            DressingRoomReport report = DressingRoomSources.Build(context);
            Check(
                star.Length > 0
                    && report.Coach.Is(c =>
                        c.Priority == PostGameReaction.Priority
                        && c.Mood == CoachMood.Pleased
                        && c.Text.Contains(star, StringComparison.Ordinal)
                    ),
                $"the coach names the hat-trick scorer {star}: {report.Coach.Select(c => c.Text).Or("")}"
            );
            Check(
                DressingRoomSources
                    .Build(context)
                    .Coach.Is(c =>
                        report.Coach.Is(d =>
                            string.Equals(c.Text, d.Text, StringComparison.Ordinal)
                        )
                    ),
                "the same game always gets the same line"
            );

            DressingRoomReport off = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl with { CoachReaction = false })
            );
            Check(
                off.Coach.Is(c => c.Priority == 0 && c.Mood == CoachMood.Pleased),
                "with reactions off the placeholder win line stays, pleased"
            );

            SeasonSave.Write(season, Path.Combine(folder, "reaction.json"));
            Check(
                SeasonSave
                    .Read(Path.Combine(folder, "reaction.json"))
                    .Is(s =>
                        string.Equals(
                            s.Stats.LastPlayedKey,
                            season.Stats.LastPlayedKey,
                            StringComparison.Ordinal
                        ) && s.Stats.LastPlayed.Is(p => p.Lines.Any(l => l.Goals == 3))
                    ),
                "the played sheet survives the save"
            );
        }

        [Test]
        public void TrackerKeepsTheStory()
        {
            World w = new WorldBuilder().App(app).NotBegun().Build();
            MatchStatTracker tracker = new();
            void Step(Action<World> act)
            {
                w.Events.Clear();
                act(w);
                tracker.Observe(w);
                w.Time += Game.Step;
            }

            List<Player> home = [.. w.OnIce(Side.Home).Where(p => !p.IsGoalie)];
            List<Player> away = [.. w.OnIce(Side.Away).Where(p => !p.IsGoalie)];
            MatchStatLine L(Player p)
            {
                return tracker.Sheet.Lines.First(l =>
                    l.Number == p.Data.Number && l.Side == p.Side
                );
            }

            void Goal(Player p)
            {
                Step(x =>
                {
                    x.Puck.LastTouch = p.Id;
                    x.Match.Score[(int)p.Side]++;
                    x.Events.Publish(new GoalScored(x.Time, p.Side, p.Id, default, 0));
                });
            }

            Step(_ => { });
            Goal(home[0]);
            Goal(home[0]);
            Goal(away[0]);
            Goal(away[0]);
            Goal(away[1]);
            Step(x =>
            {
                x.Events.Publish(new PenaltyCalled(x.Time, Side.Home, home[1].Id, default, 120));
                x.Events.Publish(new FightEnded(x.Time, away[1].Id, home[1].Id, default));
            });
            Check(
                tracker.Sheet.HomeMaxLead == 2 && tracker.Sheet.AwayMaxLead == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the leads: home 2 up, away 1 up (got {tracker.Sheet.HomeMaxLead}, {tracker.Sheet.AwayMaxLead})"
                )
            );
            Check(
                L(home[1]) is { Penalties: 1, FightsLost: 1, FightsWon: 0 }
                    && L(away[1]).FightsWon == 1,
                "the penalty and the fight count on the fighters' lines"
            );

            Goal(home[2]); // 3-3
            Step(x => x.Match.Period = x.Setup.Periods + 1);
            Goal(home[2]);
            Check(L(home[2]).Decider && !L(home[0]).Decider, "the overtime goal decides it");
        }

        [Test]
        public void ShootoutDecider()
        {
            World w = new WorldBuilder().App(app).NotBegun().Build();
            MatchStatTracker tracker = new();
            void Step(Action<World> act)
            {
                w.Events.Clear();
                act(w);
                tracker.Observe(w);
                w.Time += Game.Step;
            }

            List<Player> away = [.. w.OnIce(Side.Away).Where(p => !p.IsGoalie)];
            Step(x => x.Match.Phase = MatchPhase.Shootout);
            foreach (Player p in away.Take(2))
            {
                Step(x =>
                {
                    x.Match.Score[(int)Side.Away]++;
                    x.Events.Publish(new GoalScored(x.Time, Side.Away, p.Id, default, 0));
                });
            }

            w.Match.Score[(int)Side.Away] = 3;
            w.Match.Score[(int)Side.Home] = 2;
            MatchStatSheet sheet = tracker.Finish();
            MatchStatLine Line(Player p)
            {
                return sheet.Lines.First(l => l.Number == p.Data.Number && l.Side == Side.Away);
            }

            Check(
                Line(away[1]).Decider && !Line(away[0]).Decider && Line(away[1]).Goals == 0,
                "the winning club's last shootout goal decides it, on nobody's goal tally"
            );
        }

        [Test]
        public void SimmedGamesUseTheirPaperSheet()
        {
            SeasonState season = Season(out IReadOnlyList<ClubData> clubs);
            DressingRoomContext context = DressingRoomContext.Of(season, clubs, RuleSet.Shl);
            Check(
                context.Sheet().Is(s => s.Lines.Count > 0)
                    && context.Last.Is(l =>
                        context
                            .Sheet()
                            .Is(s => s.HomeGoals + s.AwayGoals == l.GoalsFor + l.GoalsAgainst)
                    ),
                "a simmed last game reads the paper sheet of its score"
            );
        }
    }
}
