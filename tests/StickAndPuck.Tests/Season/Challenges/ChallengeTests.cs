using System.Globalization;
using System.Text.Json.Nodes;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Season.Challenges
{
    /// <summary>
    /// Coach's challenges (W134): the rule and its presets, when a challenge is set and which one the season
    /// seed picks (beat the rival only with a rival among the three games), progress, being met or missed
    /// and the morale reward or penalty, the challenges in the save (and an older save without them), and
    /// the lines in the dressing room.
    /// </summary>
    public sealed class ChallengeTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-challengetest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private static IReadOnlyList<ClubData> Clubs => WorldBuilder.Clubs;

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static SeasonState NewSeason(int seed, RuleSet rules, string club = "")
        {
            SeasonState season = SeasonState.New(Clubs, club.Length > 0 ? club : Clubs[0].Id, seed);
            season.Rules = rules;
            return season;
        }

        /// <summary>Records the human's next game with a score from its side, then lets the desk settle and set challenges.</summary>
        private static void Play(SeasonState season, int scored, int conceded, bool desk = true)
        {
            Fixture next = season.NextGame.TryGet(out Fixture f)
                ? f
                : throw new InvalidOperationException("no next game");
            bool home = string.Equals(next.Home, season.UserClub, StringComparison.Ordinal);
            season.Record(
                next.Number,
                new FixtureResult(
                    home ? scored : conceded,
                    home ? conceded : scored,
                    ResultKind.Regulation
                )
            );
            if (desk)
            {
                season.Morale[season.UserClub] = TeamMorale.Neutral; // so a reward or penalty shows unclamped
                ChallengeDesk.AfterGame(season);
            }
        }

        private static DressingRoomReport Room(SeasonState season, RuleSet rules)
        {
            DressingRoomReport report = new();
            new ChallengeNews().Fill(DressingRoomContext.Of(season, Clubs, rules), report);
            return report;
        }

        [Test]
        public void PresetsRowsAndStrings()
        {
            Check(
                RuleSet.Shl.Challenges
                    && RuleSet.Nhl.Challenges
                    && !RuleSet.Arcade.Challenges
                    && RuleSet.Shl.ChallengeGap == 8
                    && RuleSet.Nhl.ChallengeGap == 8
                    && RuleSet.Arcade.ChallengeGap == 0,
                "SHL and NHL set a challenge every eight games, Arcade none"
            );
            Check(
                (RuleSet.Shl with { ChallengeFrequency = ChallengeFrequency.Rare }).ChallengeGap
                    == 12
                    && (
                        RuleSet.Shl with
                        {
                            ChallengeFrequency = ChallengeFrequency.Often,
                        }
                    ).ChallengeGap == 5,
                "rare waits twelve games, often five"
            );
            Check(
                new[] { nameof(RuleSet.Challenges), nameof(RuleSet.ChallengeFrequency) }.All(key =>
                    RuleRows.All.Any(r =>
                        string.Equals(r.Key, key, StringComparison.Ordinal)
                        && string.Equals(r.Group, K.RuleGroupSeason, StringComparison.Ordinal)
                    )
                ),
                "both challenge rules are on the rules page's season group"
            );
            IReadOnlyDictionary<string, string> en = Text.Read(Text.FileOf(Language.English)),
                sv = Text.Read(Text.FileOf(Language.Swedish));
            string[] keys =
            [
                K.RuleChallenges,
                K.RuleChallengeFrequency,
                K.ChallengeWinTwo,
                K.ChallengeShutout,
                K.ChallengeGoals,
                K.ChallengeRival,
                K.ChallengeNew,
                K.ChallengeProgress,
                K.ChallengeGameWon,
                K.ChallengeGameLost,
                K.ChallengeGameConceded,
                K.ChallengeGameScored,
                K.ChallengeMet,
                K.ChallengeMissed,
                K.CoachChallengeWinTwo,
                K.CoachChallengeShutout,
                K.CoachChallengeGoals,
                K.CoachChallengeRival,
                K.CoachChallengeMet,
                K.CoachChallengeMissed,
            ];
            Check(
                keys.All(k => en.ContainsKey(k) && sv.ContainsKey(k)),
                "every challenge string is in English and Swedish"
            );
        }

        [Test]
        public void SetAfterTheGapForTheNextThreeGames()
        {
            SeasonState season = NewSeason(41, RuleSet.Shl);
            for (int i = 0; i < 7; i++)
            {
                Play(season, 2, 1);
            }

            Check(season.Challenges.Count == 0, "no challenge before eight games");
            List<int> next = [.. ChallengeDesk.Upcoming(season).Select(f => f.Number)];
            Play(season, 2, 1);
            Check(
                ChallengeDesk.Open(season).TryGet(out SeasonChallenge c)
                    && c.Issued == 8
                    && c.Games.Count == ChallengeDesk.Window
                    && c.Games.SequenceEqual([
                        .. ChallengeDesk.Upcoming(season).Select(f => f.Number),
                    ])
                    && !c.Games.SequenceEqual(next),
                "after the eighth game the coach sets one for the human's next three"
            );

            SeasonState off = NewSeason(41, RuleSet.Shl with { Challenges = false });
            for (int i = 0; i < 20; i++)
            {
                Play(off, 2, 1);
            }

            Check(off.Challenges.Count == 0, "with challenges off none is ever set");
        }

        [Test]
        public void TheSeasonSeedPicksTheChallenges()
        {
            static List<string> Story(int seed)
            {
                SeasonState season = NewSeason(
                    seed,
                    RuleSet.Shl with
                    {
                        ChallengeFrequency = ChallengeFrequency.Often,
                    }
                );
                QuickSimSeason(season);
                return
                [
                    .. season.Challenges.Select(c =>
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{c.Kind}@{c.Issued}:{c.Outcome}"
                        )
                    ),
                ];
            }

            List<string> a = Story(77),
                b = Story(77);
            Check(a.Count >= 4, $"a season sets several challenges ({a.Count})");
            Check(
                a.SequenceEqual(b, StringComparer.Ordinal),
                "the same seed picks the same challenges"
            );
            HashSet<ChallengeKind> kinds = [];
            bool differs = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                SeasonState season = NewSeason(
                    seed,
                    RuleSet.Shl with
                    {
                        ChallengeFrequency = ChallengeFrequency.Often,
                    },
                    "frolunda"
                );
                QuickSimSeason(season);
                kinds.UnionWith(season.Challenges.Select(c => c.Kind));
                differs |= !Story(seed).SequenceEqual(a, StringComparer.Ordinal);
                Check(
                    season.Challenges.All(c =>
                        c.Kind != ChallengeKind.BeatRival
                        || (
                            Rivals.Are(c.Rival, season.UserClub)
                            && c.Games.Any(n => season.Games[n].Involves(c.Rival))
                        )
                    ),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"seed {seed}: a beat-the-rival challenge always has the rival among its games"
                    )
                );
            }

            Check(differs, "other seeds pick other challenges");
            Check(
                kinds.SetEquals(Enum.GetValues<ChallengeKind>()),
                $"every kind comes up across seeds ({string.Join(", ", kinds)})"
            );
        }

        /// <summary>Plays the human's whole regular season on paper through the desk.</summary>
        private static void QuickSimSeason(SeasonState season)
        {
            Dictionary<string, ClubData> byId = Clubs.ToDictionary(
                c => c.Id,
                StringComparer.Ordinal
            );
            while (season.NextGame.TryGet(out Fixture f))
            {
                season.Record(
                    f.Number,
                    TeamMorale.Sim(season, byId[f.Home], byId[f.Away], season.GameSeed(f.Number))
                );
                ChallengeDesk.AfterGame(season);
            }
        }

        [Test]
        public void WinTwoIsAnnounced()
        {
            SeasonState season = NewSeason(5, RuleSet.Shl);
            SeasonChallenge c = ChallengeDesk
                .Force(season, ChallengeKind.WinTwo)
                .Or(new SeasonChallenge());
            string goal = Text.Get(K.ChallengeWinTwo);
            Check(
                c.Kind == ChallengeKind.WinTwo
                    && c.Target == 2
                    && c.Outcome == ChallengeOutcome.Open,
                "a forced win-two-of-three challenge is open"
            );
            DressingRoomReport fresh = Room(season, RuleSet.Shl);
            Check(
                fresh.News.Any(n =>
                    n.Highlight
                    && string.Equals(
                        n.Text,
                        Text.Format(K.ChallengeNew, goal),
                        StringComparison.Ordinal
                    )
                )
                    && fresh.Coach.Is(l =>
                        string.Equals(
                            l.Text,
                            Text.Get(K.CoachChallengeWinTwo),
                            StringComparison.Ordinal
                        )
                    ),
                "the dressing room announces it and the coach sets it"
            );
        }

        [Test]
        public void WinTwoProgressAndMet()
        {
            SeasonState season = NewSeason(5, RuleSet.Shl);
            SeasonChallenge c = ChallengeDesk
                .Force(season, ChallengeKind.WinTwo)
                .Or(new SeasonChallenge());
            string goal = Text.Get(K.ChallengeWinTwo);
            Play(season, 3, 1);
            ChallengeTally tally = ChallengeDesk.Tally(season, c);
            Check(
                tally.Played == 1 && tally.Wins == 1 && c.Outcome == ChallengeOutcome.Open,
                "one win in: still open"
            );
            Check(
                Room(season, RuleSet.Shl)
                    .News.Any(n =>
                        string.Equals(
                            n.Text,
                            Text.Format(K.ChallengeProgress, goal, 1, Text.Get(K.ChallengeGameWon)),
                            StringComparison.Ordinal
                        )
                    ),
                "the news shows its progress: 1 of 3, won"
            );

            Play(season, 2, 4);
            Check(
                Room(season, RuleSet.Shl)
                    .News.Any(n =>
                        string.Equals(
                            n.Text,
                            Text.Format(
                                K.ChallengeProgress,
                                goal,
                                2,
                                Text.Get(K.ChallengeGameLost)
                            ),
                            StringComparison.Ordinal
                        )
                    ),
                "2 of 3, lost"
            );
            Play(season, 4, 2);
            Check(
                c.Outcome == ChallengeOutcome.Met
                    && c.Settled == 3
                    && c.Morale == ChallengeDesk.Reward
                    && TeamMorale.Of(season, season.UserClub)
                        == TeamMorale.Neutral + ChallengeDesk.Reward,
                "two wins meet it and lift morale by the reward"
            );
            DressingRoomReport met = Room(season, RuleSet.Shl);
            Check(
                met.News.Any(n =>
                    n.Highlight
                    && string.Equals(
                        n.Text,
                        Text.Format(K.ChallengeMet, goal),
                        StringComparison.Ordinal
                    )
                )
                    && met.Coach.Is(l =>
                        string.Equals(
                            l.Text,
                            Text.Get(K.CoachChallengeMet),
                            StringComparison.Ordinal
                        )
                        && l.Priority < PostGameReaction.Priority
                    ),
                "the dressing room shows it met, the coach pleased below the reaction's priority"
            );
            Play(season, 1, 2, desk: false);
            Check(Room(season, RuleSet.Shl).News.Count == 0, "a game later the result is old news");
        }

        [Test]
        public void WinTwoMissed()
        {
            string goal = Text.Get(K.ChallengeWinTwo);
            SeasonState lost = NewSeason(5, RuleSet.Shl);
            SeasonChallenge l = ChallengeDesk
                .Force(lost, ChallengeKind.WinTwo)
                .Or(new SeasonChallenge());
            Play(lost, 1, 3);
            Check(l.Outcome == ChallengeOutcome.Open, "one loss: still possible");
            Play(lost, 2, 3);
            Check(
                l.Outcome == ChallengeOutcome.Missed
                    && l.Settled == 2
                    && TeamMorale.Of(lost, lost.UserClub)
                        == TeamMorale.Neutral + ChallengeDesk.Penalty,
                "two losses miss it at once with a small morale dip"
            );
            Check(
                Room(lost, RuleSet.Shl)
                    .News.Any(n =>
                        string.Equals(
                            n.Text,
                            Text.Format(K.ChallengeMissed, goal),
                            StringComparison.Ordinal
                        )
                    ),
                "the dressing room shows it missed"
            );
        }

        [Test]
        public void ShutoutGoalsAndRival()
        {
            SeasonState season = NewSeason(6, RuleSet.Shl);
            SeasonChallenge shutout = ChallengeDesk
                .Force(season, ChallengeKind.Shutout)
                .Or(new SeasonChallenge());
            Play(season, 1, 2);
            Check(
                Room(season, RuleSet.Shl)
                    .News.Any(n =>
                        string.Equals(
                            n.Text,
                            Text.Format(
                                K.ChallengeProgress,
                                Text.Get(K.ChallengeShutout),
                                1,
                                Text.Format(K.ChallengeGameConceded, 2)
                            ),
                            StringComparison.Ordinal
                        )
                    ),
                "a shutout challenge's progress shows the goals let in"
            );
            Play(season, 1, 0);
            Check(shutout.Outcome == ChallengeOutcome.Met, "a clean sheet meets it");

            SeasonState goals = NewSeason(6, RuleSet.Shl);
            SeasonChallenge g = ChallengeDesk
                .Force(goals, ChallengeKind.Goals)
                .Or(new SeasonChallenge());
            Play(goals, 3, 4);
            Play(goals, 3, 1);
            Check(
                g.Target == ChallengeDesk.GoalTarget
                    && g.Outcome == ChallengeOutcome.Open
                    && ChallengeDesk.Tally(goals, g).GoalsFor == 6,
                "six goals in two: still open"
            );
            Play(goals, 3, 2);
            Check(g.Outcome == ChallengeOutcome.Missed, "nine goals in three miss ten");
            SeasonState many = NewSeason(6, RuleSet.Shl);
            SeasonChallenge m = ChallengeDesk
                .Force(many, ChallengeKind.Goals)
                .Or(new SeasonChallenge());
            Play(many, 6, 1);
            Play(many, 4, 5);
            Check(
                m.Outcome == ChallengeOutcome.Met && m.Settled == 2,
                "ten goals in two meet it early"
            );
        }

        [Test]
        public void BeatTheRival()
        {
            SeasonState derby = NewSeason(9, RuleSet.Shl, "frolunda");
            while (!ChallengeDesk.RivalIn(derby, ChallengeDesk.Upcoming(derby)).HasValue)
            {
                Play(derby, 2, 1, desk: false);
            }

            derby.Rules = RuleSet.Shl with { Rivalries = false };
            Check(
                !ChallengeDesk.RivalIn(derby, ChallengeDesk.Upcoming(derby)).HasValue
                    && !ChallengeDesk.Force(derby, ChallengeKind.BeatRival).HasValue,
                "with rivalries off there is no rival to beat"
            );
            derby.Rules = RuleSet.Shl;
            SeasonChallenge r = ChallengeDesk
                .Force(derby, ChallengeKind.BeatRival)
                .Or(new SeasonChallenge());
            Check(
                r.Kind == ChallengeKind.BeatRival && Rivals.Are(r.Rival, "frolunda"),
                $"with a rival among the next three the coach can ask to beat it ({r.Rival})"
            );
            string name = Clubs
                .First(c => string.Equals(c.Id, r.Rival, StringComparison.Ordinal))
                .Name;
            Check(
                Room(derby, RuleSet.Shl)
                    .News.Any(n =>
                        string.Equals(
                            n.Text,
                            Text.Format(
                                K.ChallengeNew,
                                Text.Format(K.ChallengeRival, name.ToUpperInvariant())
                            ),
                            StringComparison.Ordinal
                        )
                    ),
                "the news names the rival"
            );
            while (derby.NextGame.TryGet(out Fixture next) && !next.Involves(r.Rival))
            {
                Play(derby, 5, 0);
            }

            Check(r.Outcome == ChallengeOutcome.Open, "wins against others do not meet it");
            Play(derby, 1, 2);
            Check(r.Outcome == ChallengeOutcome.Missed, "losing to the rival misses it");

            SeasonState late = NewSeason(9, RuleSet.Shl);
            while (ChallengeDesk.Upcoming(late).Count >= ChallengeDesk.Window)
            {
                Play(late, 2, 1, desk: false);
            }

            Check(
                !ChallengeDesk.Pick(late).HasValue
                    && !ChallengeDesk.Force(late, ChallengeKind.WinTwo).HasValue,
                "with fewer than three games left no challenge is set"
            );
        }

        [Test]
        public void MoraleOffGivesNoReward()
        {
            RuleSet rules = RuleSet.Shl with { Morale = false };
            SeasonState season = NewSeason(12, rules);
            SeasonChallenge c = ChallengeDesk
                .Force(season, ChallengeKind.Shutout)
                .Or(new SeasonChallenge());
            season.Morale.Clear();
            season.Record(
                season.NextGame.Or(new Fixture(0, 0, "", "")).Number,
                new FixtureResult(0, 0, ResultKind.Regulation)
            );
            Check(
                ChallengeDesk.Settle(season, c) == ChallengeOutcome.Met
                    && TeamMorale.Of(season, season.UserClub) == TeamMorale.Neutral,
                "met with morale off: settled, morale untouched"
            );
            Check(
                Room(season, RuleSet.Shl with { Challenges = false }).News.Count == 0,
                "with challenges off the dressing room shows none"
            );
        }

        [Test]
        public void ChallengesLiveInTheSave()
        {
            _ = Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "season.json");
            SeasonState season = app.Season.StartNew(Clubs, Clubs[0].Id, Maybe.Some(21), path);
            season.Rules = RuleSet.Shl;
            SeasonChallenge c = ChallengeDesk
                .Force(season, ChallengeKind.Goals)
                .Or(new SeasonChallenge());
            _ = app.Season.SimCurrentGame(); // plays one of its games and saves
            app.Season.End();
            Check(
                app.Season.Continue(Clubs, path).TryGet(out SeasonState read)
                    && read.Challenges.Count == 1
                    && read.Challenges[0].Kind == ChallengeKind.Goals
                    && read.Challenges[0].Target == c.Target
                    && read.Challenges[0].Games.SequenceEqual(c.Games)
                    && read.Challenges[0].Outcome == c.Outcome
                    && ChallengeDesk.Tally(read, read.Challenges[0]).Played == 1,
                "the challenge and its progress read back from the save"
            );

            JsonObject json = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            Check(
                json.Remove("challenges"),
                "the save holds the challenges, removed to make an older save"
            );
            File.WriteAllText(path, json.ToJsonString());
            app.Season.End();
            Check(
                SeasonSave.Read(path).Is(s => s.Challenges.Count == 0 && s.Games.Count > 0),
                "a save from before challenges reads with none"
            );
        }
    }
}
