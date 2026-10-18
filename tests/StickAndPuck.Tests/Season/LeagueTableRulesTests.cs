using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Playoffs.Dev;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// The league table rules per preset: the points a game gives (3-2-1-0 SHL and Arcade, 2-2-1-0 NHL),
    /// the head-to-head tiebreak (NHL counting regulation wins first) and the playoff home ice order
    /// (alternating SHL and Arcade, 2-2-1-1-1 NHL), kept in the season save.
    /// </summary>
    public sealed class LeagueTableRulesTests : CheckedTest
    {
        /// <summary>The game state the test plays in, fresh for each test.</summary>
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-leaguetable-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        [After(Test)]
        public void EndSessions()
        {
            app.Season.End();
            app.Playoffs.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl
                    is {
                        TablePoints: TablePoints.ThreeTwoOne,
                        Tiebreak: TableTiebreak.GoalDifference,
                        PlayoffHomeIce: PlayoffHomeIce.Alternating
                    },
                "SHL: 3-2-1-0, goal difference before head-to-head, alternating home ice"
            );
            Check(
                RuleSet.Arcade
                    is {
                        TablePoints: TablePoints.ThreeTwoOne,
                        Tiebreak: TableTiebreak.GoalDifference,
                        PlayoffHomeIce: PlayoffHomeIce.Alternating
                    },
                "Arcade: the SHL table rules"
            );
            Check(
                RuleSet.Nhl
                    is {
                        TablePoints: TablePoints.TwoTwoOne,
                        Tiebreak: TableTiebreak.RegulationWins,
                        PlayoffHomeIce: PlayoffHomeIce.TwoTwoOneOneOne
                    },
                "NHL: 2-2-1-0, regulation wins first, 2-2-1-1-1 home ice"
            );
            RuleSet changed = (
                RuleChoice.Of(RulePreset.Shl) with
                {
                    Overrides = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["TablePoints"] = "TwoTwoOne",
                        ["Tiebreak"] = "HeadToHead",
                        ["PlayoffHomeIce"] = "TwoTwoOneOneOne",
                    },
                }
            ).Resolve();
            Check(
                changed
                    is {
                        Custom: true,
                        TablePoints: TablePoints.TwoTwoOne,
                        Tiebreak: TableTiebreak.HeadToHead,
                        PlayoffHomeIce: PlayoffHomeIce.TwoTwoOneOneOne
                    },
                "each table rule is a setting that overrides the preset"
            );
        }

        [Test]
        public void PointsPerPreset()
        {
            static string Points(FixtureResult r, RuleSet rules)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{r.PointsFor(home: true, rules.TablePoints)}-{r.PointsFor(home: false, rules.TablePoints)}"
                );
            }

            FixtureResult regulation = new(3, 1, ResultKind.Regulation),
                overtime = new(2, 3, ResultKind.Overtime),
                shootout = new(4, 3, ResultKind.Shootout);
            foreach (RuleSet shl in new[] { RuleSet.Shl, RuleSet.Arcade })
            {
                Check(
                    Same(Points(regulation, shl), "3-0")
                        && Same(Points(overtime, shl), "1-2")
                        && Same(Points(shootout, shl), "2-1"),
                    $"{shl.Name}: 3 for a regulation win, 2 and 1 after regulation"
                );
            }

            Check(
                Same(Points(regulation, RuleSet.Nhl), "2-0")
                    && Same(Points(overtime, RuleSet.Nhl), "1-2")
                    && Same(Points(shootout, RuleSet.Nhl), "2-1"),
                "NHL: 2 for any win, 1 for a loss after regulation"
            );

            List<Fixture> games = [new(0, 0, "a", "b", regulation), new(1, 0, "c", "d", shootout)];
            Check(
                Same(
                    Order(Standings.Compute(["a", "b", "c", "d"], games, RuleSet.Shl)),
                    "a3,c2,d1,b0"
                )
                    && Same(
                        Order(Standings.Compute(["a", "b", "c", "d"], games, RuleSet.Nhl)),
                        "a2,c2,d1,b0"
                    ),
                "the table adds up the preset's points"
            );
        }

        [Test]
        public void HeadToHead()
        {
            // a and b both take 3 points from 3-3 goals; b beat a, so b is ahead though a sorts first by id
            List<Fixture> games =
            [
                new(0, 0, "b", "a", new FixtureResult(2, 1, ResultKind.Regulation)),
                new(1, 0, "a", "x", new FixtureResult(2, 1, ResultKind.Regulation)),
                new(2, 1, "y", "b", new FixtureResult(2, 1, ResultKind.Regulation)),
            ];
            string[] ids = ["a", "b", "x", "y"];
            Check(
                Same(Order(Standings.Compute(ids, games, RuleSet.Shl)), "y3,b3,a3,x0"),
                "SHL: level on points, goal difference and goals, the game between them decides"
            );

            // a beat b but b has the better goal difference; both on 3 points
            List<Fixture> level =
            [
                new(0, 0, "a", "b", new FixtureResult(1, 0, ResultKind.Regulation)),
                new(1, 0, "b", "x", new FixtureResult(5, 0, ResultKind.Regulation)),
                new(2, 1, "y", "a", new FixtureResult(4, 0, ResultKind.Regulation)),
                new(3, 1, "x", "y", new FixtureResult(1, 0, ResultKind.Shootout)),
            ];
            RuleSet headFirst = RuleSet.Shl with { Tiebreak = TableTiebreak.HeadToHead };
            Check(
                Same(Order(Standings.Compute(ids, level, RuleSet.Shl)), "y4,b3,a3,x2")
                    && Same(Order(Standings.Compute(ids, level, headFirst)), "y4,a3,b3,x2"),
                "goal difference first puts b ahead; head-to-head first puts a, who beat b, ahead"
            );
        }

        [Test]
        public void RegulationWinsFirst()
        {
            // p wins twice after regulation by big margins, q twice in regulation by one goal: 4 NHL points each
            List<Fixture> games =
            [
                new(0, 0, "p", "r", new FixtureResult(5, 1, ResultKind.Shootout)),
                new(1, 0, "q", "s", new FixtureResult(1, 0, ResultKind.Regulation)),
                new(2, 1, "s", "p", new FixtureResult(1, 5, ResultKind.Overtime)),
                new(3, 1, "r", "q", new FixtureResult(0, 1, ResultKind.Regulation)),
            ];
            string[] ids = ["p", "q", "r", "s"];
            Check(
                Same(Order(Standings.Compute(ids, games, RuleSet.Nhl)), "q4,p4,r1,s1"),
                "NHL: level on points, regulation wins come before goal difference"
            );
            Check(
                Same(
                    Order(
                        Standings.Compute(
                            ids,
                            games,
                            RuleSet.Nhl with
                            {
                                Tiebreak = TableTiebreak.GoalDifference,
                            }
                        )
                    ),
                    "p4,q4,r1,s1"
                ),
                "with the SHL tiebreak goal difference decides instead"
            );
            Check(
                Same(Order(Standings.Compute(ids, games, RuleSet.Shl)), "q6,p4,r1,s1"),
                "SHL: 3 points for a regulation win puts q clear"
            );
        }

        [Test]
        public void PlayoffHomeIceOrder()
        {
            static PlayoffSeries Bo7(PlayoffHomeIce homeIce)
            {
                return new()
                {
                    HighSeed = 1,
                    LowSeed = 8,
                    High = "a",
                    Low = "b",
                    BestOf = 7,
                    HomeIce = homeIce,
                };
            }

            Check(
                Enumerable
                    .Range(0, 7)
                    .Select(Bo7(PlayoffHomeIce.Alternating).HighHosts)
                    .SequenceEqual([true, false, true, false, true, false, true]),
                "alternating: the higher seed hosts games 1, 3, 5 and 7"
            );
            PlayoffSeries nhl = Bo7(PlayoffHomeIce.TwoTwoOneOneOne);
            Check(
                Enumerable
                    .Range(0, 7)
                    .Select(nhl.HighHosts)
                    .SequenceEqual([true, true, false, false, true, false, true]),
                "2-2-1-1-1: the higher seed hosts games 1, 2, 5 and 7"
            );
            List<string> hosts = [];
            foreach (string winner in new[] { "a", "b", "a", "b", "a", "b", "b" })
            {
                hosts.Add(nhl.NextHome);
                bool homeWins = Same(nhl.NextHome, winner);
                nhl.Games.Add(
                    new PlayoffGame(
                        nhl.NextHome,
                        nhl.NextAway,
                        homeWins
                            ? new FixtureResult(3, 1, ResultKind.Regulation)
                            : new FixtureResult(1, 3, ResultKind.Regulation)
                    )
                );
            }
            Check(
                Same(string.Concat(hosts), "aabbaba") && nhl.Winner == "b",
                $"a 2-2-1-1-1 series is hosted a a b b a b a ({string.Concat(hosts)})"
            );
            PlayoffSeries bo3 = new()
            {
                High = "a",
                Low = "b",
                BestOf = 3,
                HomeIce = PlayoffHomeIce.TwoTwoOneOneOne,
            };
            Check(
                !bo3.HighHosts(0) && bo3.HighHosts(1) && bo3.HighHosts(2),
                "the best of three play-in keeps the lower seed at home in game 1"
            );
        }

        [Test]
        public void SeasonKeepsTheTableRules()
        {
            foreach (RuleSet rules in new[] { RuleSet.Shl, RuleSet.Arcade, RuleSet.Nhl })
            {
                IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
                string path = Path.Combine(folder, rules.Name + ".json");
                SeasonState season = app.Season.StartNew(clubs, clubs[0].Id, 11, path);
                season.Rules = rules;
                _ = PlayoffScenes.SettleSeason(app);
                int win = rules.TablePoints == TablePoints.ThreeTwoOne ? 3 : 2;
                Check(
                    season.Table.Any(r => r.OvertimeWins > 0)
                        && season.Table.All(r =>
                            r.Points == (win * r.Wins) + (2 * r.OvertimeWins) + r.OvertimeLosses
                        ),
                    $"{rules.Name}: every line of the season's table counts its preset's points for a regulation win"
                );
                PlayoffState playoffs = app.Playoffs.Open().TryGet(out PlayoffState p)
                    ? p
                    : throw new InvalidOperationException("expected playoffs");
                SeasonSave.Write(season, path);
                PlayoffState read =
                    SeasonSave.Read(path).TryGet(out SeasonState again)
                    && again.Playoffs.TryGet(out PlayoffState ap)
                        ? ap
                        : throw new InvalidOperationException("expected saved playoffs");
                Check(
                    playoffs.HomeIce == rules.PlayoffHomeIce
                        && read.HomeIce == rules.PlayoffHomeIce
                        && read.Rounds.SelectMany(r => r.Series)
                            .All(s => s.HomeIce == rules.PlayoffHomeIce),
                    $"{rules.Name}: the playoffs take {rules.PlayoffHomeIce} home ice and keep it in the save"
                );
                app.Playoffs.End();
                app.Season.End();
            }
        }

        private static string Order(IReadOnlyList<StandingRow> table)
        {
            return string.Join(
                ',',
                table.Select(r =>
                    string.Create(CultureInfo.InvariantCulture, $"{r.ClubId}{r.Points}")
                )
            );
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
