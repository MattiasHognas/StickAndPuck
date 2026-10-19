using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Season.DressingRoom
{
    /// <summary>
    /// The dressing room between season games (W122): the rule presets, the screen after a season result
    /// (on in SHL and NHL, off goes to the table), the coach line from the last result, the report's
    /// priorities and the placeholder news from the season.
    /// </summary>
    public sealed class DressingRoomTests : CheckedTest
    {
        /// <summary>The game state the test plays in, fresh for each test.</summary>
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-dressingroom-{Environment.ProcessId}-{Guid.NewGuid():N}"
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
                RuleSet.Shl.DressingRoom == DressingRoomStyle.Full
                    && RuleSet.Nhl.DressingRoom == DressingRoomStyle.Full
                    && RuleSet.Arcade.DressingRoom == DressingRoomStyle.Off,
                "SHL and NHL show the full dressing room, Arcade none"
            );
        }

        [Test]
        public void AfterASeasonResult()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 5,
                path: Path.Combine(folder, "season.json")
            );
            Check(
                app.Screens.AfterSeasonGame() is DressingRoomScreen,
                "a season with a next game goes to the dressing room"
            );
            app.Screens.SimSeasonGame();
            Check(
                app.Screens.Screens.Count == 2
                    && app.Screens.Screens[0] is DressingRoomScreen
                    && app.Screens.Screens[1] is SimResultScreen,
                "a simmed game shows its score over the dressing room"
            );
            app.Screens.Pop();
            app.Screens.SimSeasonGame();
            Check(
                app.Screens.Screens.Count == 2 && app.Screens.Screens[0] is DressingRoomScreen,
                "simming from the dressing room keeps it under the next score"
            );

            season.Rules = RuleSet.Shl with { DressingRoom = DressingRoomStyle.Off };
            Check(
                app.Screens.AfterSeasonGame() is StandingsScreen,
                "with the dressing room off a result goes to the table"
            );
        }

        [Test]
        public void CoachLines()
        {
            static Maybe<LastGame> Game(int home, int away, ResultKind kind)
            {
                return new LastGame(
                    "a",
                    "b",
                    new FixtureResult(home, away, kind),
                    Playoff: false,
                    "a"
                );
            }

            Check(
                string.Equals(
                    LastResultCoach.LineFor(Maybe.None<LastGame>()),
                    K.CoachFirstGame,
                    StringComparison.Ordinal
                ),
                "no game yet: the first-game line"
            );
            (Maybe<LastGame> Game, string Key)[] cases =
            [
                (Game(5, 1, ResultKind.Regulation), K.CoachBigWin),
                (Game(2, 1, ResultKind.Regulation), K.CoachWin),
                (Game(3, 2, ResultKind.Shootout), K.CoachExtraWin),
                (Game(2, 3, ResultKind.Overtime), K.CoachExtraLoss),
                (Game(1, 3, ResultKind.Regulation), K.CoachLoss),
                (Game(0, 4, ResultKind.Regulation), K.CoachBigLoss),
                (
                    new LastGame(
                        "b",
                        "a",
                        new FixtureResult(0, 4, ResultKind.Regulation),
                        Playoff: false,
                        "a"
                    ),
                    K.CoachBigWin
                ),
            ];
            foreach ((Maybe<LastGame> game, string key) in cases)
            {
                Check(
                    string.Equals(LastResultCoach.LineFor(game), key, StringComparison.Ordinal),
                    $"the coach says {key} after {game}"
                );
            }
        }

        [Test]
        public void ReportPriorities()
        {
            DressingRoomReport report = new();
            report.Say(new DressingRoomEntry("placeholder"));
            report.Say(new DressingRoomEntry("hat trick", Priority: 10));
            report.Say(new DressingRoomEntry("later placeholder"));
            Check(
                report.Coach.Is(c => string.Equals(c.Text, "hat trick", StringComparison.Ordinal)),
                "a sharper coach line keeps its place over a placeholder"
            );
            report.AddNews(new DressingRoomEntry("table"));
            report.AddNews(new DressingRoomEntry("flu", Priority: 5));
            Check(
                report.News.Count == 2
                    && string.Equals(report.News[0].Text, "flu", StringComparison.Ordinal),
                "news shows the highest priority first"
            );
            Check(
                report.Injuries.Count == 0 && report.Board.Count == 0,
                "injuries and board start empty"
            );
        }

        [Test]
        public void SeasonNewsAndLastGame()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string user = clubs[2].Id;
            SeasonState season = app.Season.StartNew(
                clubs,
                user,
                seed: 9,
                path: Path.Combine(folder, "season.json")
            );
            DressingRoomContext before = DressingRoomContext.Of(season, clubs, RuleSet.Shl);
            Check(!before.Last.HasValue, "no last game before the first");
            for (int i = 0; i < 4; i++)
            {
                _ = app.Season.SimCurrentGame();
            }

            DressingRoomContext context = DressingRoomContext.Of(season, clubs, RuleSet.Shl);
            Fixture latest = season.PlayedBy(user).MaxBy(f => f.Number);
            Check(
                context.Last.Is(l =>
                    string.Equals(l.Home, latest.Home, StringComparison.Ordinal)
                    && string.Equals(l.Away, latest.Away, StringComparison.Ordinal)
                    && latest.Result == l.Result
                ),
                "the last game is the club's latest result"
            );
            DressingRoomReport report = DressingRoomSources.Build(context);
            int streak = SeasonNews.Streak(season, user);
            Check(
                report.Coach.HasValue
                    && report.News.Count == (Math.Abs(streak) >= SeasonNews.StreakNews ? 2 : 1),
                $"the coach speaks and the news has the table place and any streak ({streak.ToString(CultureInfo.InvariantCulture)})"
            );
        }
    }
}
