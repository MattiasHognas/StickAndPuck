using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Playoffs.Dev;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Playoffs
{
    /// <summary>
    /// The SHL playoffs: seeding, the series and home order, reseeding, the human's series against the
    /// simulated ones, a champion at the end, recording a playoff game at MatchEnd and continuing the
    /// playoffs after a restart, with a save in a temporary folder. Formerly SAP_SCENE=playofftest.
    /// </summary>
    public sealed class PlayoffTests : CheckedTest
    {
        /// <summary>The game state the test plays in, fresh for each test.</summary>
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-playofftest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private string SavePath => Path.Combine(folder, "season.json");

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

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }

        /// <summary>Gets the next match's setup, failing loudly when the run is over.</summary>
        private static MatchSetup Must(NextMatch next)
        {
            return next is NextMatch.Play play
                ? play.Setup
                : throw new InvalidOperationException("expected a match to play");
        }

        private static FixtureResult Win(bool home)
        {
            return home
                ? new FixtureResult(3, 1, ResultKind.Regulation)
                : new FixtureResult(1, 3, ResultKind.Regulation);
        }

        /// <summary>A finished regular season with every game settled on paper, saved at the path.</summary>
        private SeasonState FinishedSeason(IReadOnlyList<ClubData> clubs, string path, int seed)
        {
            _ = app.Season.StartNew(clubs, clubs[0].Id, seed, path);
            _ = PlayoffScenes.SettleSeason(app);
            return Must(app.Season.Current);
        }

        [Test]
        public void Series()
        {
            PlayoffSeries bo3 = new()
            {
                HighSeed = 7,
                LowSeed = 10,
                High = "a",
                Low = "b",
                BestOf = 3,
            };
            Check(
                Same(bo3.NextHome, "b")
                    && !bo3.HighHosts(0)
                    && bo3.HighHosts(1)
                    && bo3.HighHosts(2),
                "play-in: the lower seed hosts game 1, the higher seed games 2 and 3"
            );
            PlayoffSeries bo7 = new()
            {
                HighSeed = 1,
                LowSeed = 8,
                High = "a",
                Low = "b",
                BestOf = 7,
            };
            Check(
                Enumerable
                    .Range(0, 7)
                    .Select(bo7.HighHosts)
                    .SequenceEqual([true, false, true, false, true, false, true]),
                "best of seven: the higher seed hosts games 1, 3, 5 and 7"
            );
            bool open = true;
            foreach (string winner in new[] { "a", "b", "a", "b", "a", "b", "b" })
            {
                open &= !bo7.IsOver;
                bo7.Games.Add(
                    new PlayoffGame(bo7.NextHome, bo7.NextAway, Win(Same(bo7.NextHome, winner)))
                );
            }
            Check(open, "a best of seven stays open through 3-3");
            Check(
                bo7.IsOver && bo7.Winner == "b" && bo7.LowWins == 4,
                "four wins take a best of seven"
            );
        }

        [Test]
        public void Seeding()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = FinishedSeason(clubs, SavePath, 2026);
            List<string> table = [.. season.Table.Select(r => r.ClubId)];
            PlayoffState p = PlayoffState.New(season);
            Check(
                p.Seeds.SequenceEqual(table, StringComparer.Ordinal),
                "the seeds are the final table"
            );
            PlayoffRound playIn = p.Rounds.Single();
            Check(
                playIn is { BestOf: 3, Series.Count: 2 },
                "the play-in is two best of three series"
            );
            Check(
                playIn.Series[0] is { HighSeed: 7, LowSeed: 10 }
                    && playIn.Series[1] is { HighSeed: 8, LowSeed: 9 },
                "the play-in is 7 v 10 and 8 v 9"
            );
            Check(
                Same(playIn.Series[0].High, table[6]) && Same(playIn.Series[0].Low, table[9]),
                "the series hold the clubs placed 7th and 10th"
            );
            Check(
                !p
                    .Rounds.SelectMany(r => r.Series)
                    .Any(s => s.Involves(table[10]) || s.Involves(table[13])),
                "places 11 to 14 miss the playoffs"
            );

            // play the whole bracket on paper with the human out of it
            p.UserClub = table[^1];
            _ = p.SimulateUntilUser(clubs, PlayoffRules.Shl);
            PlayoffRound qf = p.Rounds[1];
            List<int> direct = [.. Enumerable.Range(1, PlayoffState.Direct)];
            List<int> survivors =
            [
                .. p.Rounds[0].Series.Select(s => p.SeedOf(Must(s.Winner))).Order(),
            ];
            Check(
                qf.Series.Select(s => s.HighSeed).SequenceEqual([1, 2, 3, 4]),
                "the quarter-finals hold seeds 1 to 4 at home"
            );
            Check(
                qf.Series[0].LowSeed == survivors[1]
                    && qf.Series[1].LowSeed == survivors[0]
                    && qf.Series[2].LowSeed == 6
                    && qf.Series[3].LowSeed == 5,
                $"reseeded: 1 meets the worst play-in winner, 2 the other, 3 v 6, 4 v 5 ({string.Join(',', qf.Series.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.HighSeed}v{s.LowSeed}")))})"
            );
            Check(
                qf.Series.SelectMany(s => new[] { s.HighSeed, s.LowSeed })
                    .Order()
                    .SequenceEqual(direct.Concat(survivors)),
                "the top six and both play-in winners make the quarter-finals"
            );
            VerifyPlayoffSeeds(clubs, season, table, p);
        }

        private void VerifyPlayoffSeeds(
            IReadOnlyList<ClubData> clubs,
            SeasonState season,
            List<string> table,
            PlayoffState p
        )
        {
            Check(
                p.Rounds.Count == 4
                    && p.Rounds[2].Series.Count == 2
                    && p.Rounds[3].Series.Count == 1,
                "quarter-finals, semi-finals and a final follow"
            );
            IList<PlayoffSeries> semis = p.Rounds[2].Series;
            List<int> semiSeeds =
            [
                .. p.Rounds[1].Series.Select(s => p.SeedOf(Must(s.Winner))).Order(),
            ];
            Check(
                semis[0].HighSeed == semiSeeds[0]
                    && semis[0].LowSeed == semiSeeds[3]
                    && semis[1].HighSeed == semiSeeds[1]
                    && semis[1].LowSeed == semiSeeds[2],
                "the semi-finals are reseeded: best left v worst left"
            );
            Check(
                p.IsOver && p.Champion.HasValue && p.Champion == p.Rounds[3].Series[0].Winner,
                $"the final's winner is champion ({p.Champion.Or("none")})"
            );
            List<PlayoffSeries> all = [.. p.Rounds.SelectMany(r => r.Series)];
            Check(
                all.TrueForAll(s =>
                    s.IsOver
                    && s.Games.Count <= s.BestOf
                    && Math.Max(s.HighWins, s.LowWins) == s.WinsNeeded
                ),
                "every series ends on its deciding win"
            );
            Check(
                all.SelectMany(s => s.Games)
                    .All(g =>
                        g.Result.Kind != ResultKind.Shootout
                        && g.Result.HomeGoals != g.Result.AwayGoals
                    ),
                "no playoff game is a tie or a shootout"
            );
            PlayoffState again = PlayoffState.New(season);
            again.UserClub = table[^1];
            _ = again.SimulateUntilUser(clubs, PlayoffRules.Shl);
            Check(again.Champion == p.Champion, "the same season gives the same champion");
        }

        [Test]
        public void TopSeedRun()
        {
            SeasonState season = FinishedSeason(WorldBuilder.Clubs, SavePath, 77);
            List<string> table = [.. season.Table.Select(r => r.ClubId)];
            season.UserClub = table[0];
            Check(
                app.Playoffs.NextSetup() is NextMatch.Play first
                    && Same(first.Setup.Home.Id, table[0]),
                "the top seed skips the play-in and opens the quarter-finals at home"
            );
            PlayoffState p = Must(app.Playoffs.Current);
            Check(
                p.Rounds.Count == 2
                    && p.Rounds[0].IsOver
                    && p.Rounds[1].Series.All(s => s.Games.Count == 0),
                "the play-in is settled before the human's first game"
            );

            // the human wins the quarter-final in five, the other series keep pace
            bool pace = true;
            foreach (bool userWins in new[] { true, false, true, true, true })
            {
                PlayoffSeries s = Must(app.Playoffs.Playing);
                bool userHome = Same(s.NextHome, p.UserClub);
                _ = app.Playoffs.Finish(Win(userWins == userHome));
                pace &=
                    p.Rounds[1].Series.All(o => o.IsOver || o.Games.Count == s.Games.Count)
                    || p.Rounds.Count > 2;
                _ = app.Playoffs.NextSetup();
            }
            Check(pace, "the other quarter-finals are played level with the human's series");
            Check(
                p.Rounds.Count == 3
                    && p.Rounds[1].IsOver
                    && p.Rounds[1].Series[0].Winner == p.UserClub,
                "after the human wins 4-1 the round is finished and the semi-finals drawn"
            );
            Check(
                app.Playoffs.Playing.Is(semi => semi.HighSeed == 1),
                "the human's semi-final is next, at home as the top seed"
            );

            // the human is swept in the semi-final; the rest is played to a champion
            for (int i = 0; i < 4 && app.Playoffs.Playing.TryGet(out PlayoffSeries s); i++)
            {
                _ = app.Playoffs.Finish(Win(!Same(s.NextHome, p.UserClub)));
                _ = app.Playoffs.NextSetup();
            }
            Check(
                p.IsOver && !p.UserAlive && !app.Playoffs.Playing.HasValue,
                $"once the human is out the playoffs play to a champion ({p.Champion.Or("none")})"
            );
            Check(
                !app.Playoffs.Finish(Win(home: true))
                    && app.Playoffs.NextSetup() is not NextMatch.Play,
                "nothing is left to play after the final"
            );
        }

        [Test]
        public void MatchEndAndRestart()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string path = SavePath;
            SeasonState season = FinishedSeason(clubs, path, 5);
            season.UserClub = season.Table[7].ClubId;
            MatchSetup setup = Must(app.Playoffs.NextSetup());
            PlayoffSeries series = Must(app.Playoffs.Playing);
            Check(
                series is { HighSeed: 8, LowSeed: 9 } && Same(setup.Away.Id, season.UserClub),
                "the 8th seed opens the play-in away at the 9th"
            );
            Check(
                setup.Inputs[0].Device == InputDevice.KeyboardMouse,
                "keyboard and mouse play the human's club"
            );
            Check(
                setup.OvertimeFormat == OvertimeFormat.ShlPlayoffs
                    && OvertimeFormat.Of(setup)
                        is { Skaters: 5, UntilGoal: true, HasShootout: false },
                "a playoff game carries the SHL playoff overtime: 5 on 5 until a goal, no shootout"
            );
            MatchSetup seasonGame = season.SetupFor(
                new Fixture(0, 0, setup.Home.Id, setup.Away.Id),
                clubs
            );
            Check(
                !seasonGame.OvertimeFormat.HasValue
                    && OvertimeFormat.Of(seasonGame) == OvertimeFormat.Shl,
                "a season game keeps 3 on 3 and the shootout"
            );
            app.Playoffs.Rules = app.Playoffs.Rules with
            {
                Name = "test league",
                Overtime = OvertimeFormat.Shl with { Skaters = 4 },
            };
            Check(
                Must(app.Playoffs.NextSetup()).OvertimeFormat.Is(o => o.Skaters == 4),
                "a mode with its own playoff rules puts them on the game"
            );
            app.Playoffs.Rules = PlayoffRules.Shl;
            _ = app.Playoffs.NextSetup();
            Check(
                !app.Playoffs.Finish(new FixtureResult(2, 2, ResultKind.Regulation)),
                "a tie is not a playoff result"
            );

            // the match goes to overtime and the home side wins 3-2
            World world = new(setup, new Settings()) { App = app };
            PlayoffSystem system = new();
            AutosaveSystem autosave = new();
            world.Match.Period = 4;
            world.Match.Score[0] = 3;
            world.Match.Score[1] = 2;
            world.Match.Phase = MatchPhase.Final;
            system.Update(world, Game.Step);
            Check(series.Games.Count == 0, "nothing is recorded before MatchEnd");
            world.Events.Publish(new MatchWon(world.Time, Side.Home));
            system.Update(world, Game.Step);
            VerifyRestartedPlayoff(clubs, path, season, series, world, autosave);
        }

        private void VerifyRestartedPlayoff(
            IReadOnlyList<ClubData> clubs,
            string path,
            SeasonState season,
            PlayoffSeries series,
            World world,
            AutosaveSystem autosave
        )
        {
            autosave.Update(world, Game.Step);
            Check(
                series.Games.Count == 1
                    && series.Games[0].Result == new FixtureResult(3, 2, ResultKind.Overtime),
                "MatchEnd records 3-2 in overtime"
            );
            Check(
                Must(season.Playoffs).Rounds[0].Series[0].Games.Count == 1,
                "the other play-in series has played its game 1 too"
            );
            Check(!app.Season.Unsaved, "the autosave ran after the playoff game");

            string before = File.ReadAllText(path);
            app.Season.End();
            app.Playoffs.End();
            SeasonState resumed = Must(app.Season.Continue(clubs, path));
            IReadOnlyList<string> seeds = Must(season.Playoffs).Seeds;
            Check(
                resumed.Playoffs.Is(p =>
                    p.Rounds.Count == 1
                    && p.Rounds[0].Series.Sum(s => s.Games.Count) == 2
                    && p.Seeds.SequenceEqual(seeds, StringComparer.Ordinal)
                ),
                "continuing after a restart reads the playoffs back"
            );
            NextMatch next = app.Playoffs.NextSetup();
            Check(
                next is NextMatch.Play n
                    && app.Playoffs.Playing.Is(x => x.Games.Count == 1 && x.HighSeed == 8)
                    && Same(n.Setup.Home.Id, resumed.UserClub),
                "after the restart game 2 of the play-in is next, at the human's rink"
            );
            Check(Same(File.ReadAllText(path), before), "continuing does not rewrite the save");
        }

        [Test]
        public void MissedPlayoffs()
        {
            SeasonState season = FinishedSeason(WorldBuilder.Clubs, SavePath, 11);
            season.UserClub = season.Table[11].ClubId;
            Check(
                app.Playoffs.NextSetup() is not NextMatch.Play
                    && app.Playoffs.Current.Is(x => x.IsOver && !x.UserQualified),
                "a club placed 12th has no playoff game and the playoffs play out at once"
            );
            app.Season.End();
            Check(!app.Playoffs.Open().HasValue, "outside season mode there are no playoffs");
        }
    }
}
