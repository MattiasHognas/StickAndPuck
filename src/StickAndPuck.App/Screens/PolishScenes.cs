using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.DressingRoom.Dev;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Screens
{
    /// <summary>The saves behind the headless captures of the polished screens (SAP_SCREEN=stats, table, sim,
    /// trophy, arcadetrophy and bestruns). Each writes its own test save next to season.json, so the
    /// player's season and arcade saves are left alone.</summary>
    internal static class PolishScenes
    {
        /// <summary>Opens one of the season screens on its test save.</summary>
        public static void Open(AppState app, string screen, IReadOnlyList<ClubData> clubs)
        {
            switch (screen)
            {
                case "leaders" or "stats":
                    Season(app, clubs, "stats-test.json", 12);
                    app.Screens.Push(
                        new SeasonStatsScreen(app).Showing(
                            Env.Get("SAP_STATS_VIEW") is { Length: > 0 } view ? view : "POINTS"
                        )
                    );
                    break;
                case "table":
                    Season(app, clubs, "table-test.json", 20);
                    app.Screens.Push(new StandingsScreen(app));
                    break;
                case "sim":
                    Season(app, clubs, "sim-test.json", 5);
                    app.Screens.SimSeasonGame();
                    break;
                case "dressingroom" or "coachtalk":
                    Season(app, clubs, "dressingroom-test.json", 6);
                    app.Screens.SimSeasonGame(); // a season result: the dressing room under its score
                    if (app.Screens.Top.Is(t => t is SimResultScreen))
                    {
                        app.Screens.Pop(); // continue past the score into the dressing room
                    }

                    if (
                        Env.Get("SAP_NEWS") is { Length: > 0 } news
                        && app.Season.Current.TryGet(out SeasonState room)
                    )
                    {
                        _ = NewsDesk.Force(room, clubs, NewsDeck.Default, news.ToLowerInvariant()); // a news card in play, whatever the dice said
                    }

                    if (
                        Env.Get("SAP_REACTION") is { Length: > 0 } reaction
                        && app.Season.Current.TryGet(out SeasonState staged)
                        && !StagedReaction.Stage(staged, clubs, reaction.ToLowerInvariant())
                    )
                    {
                        throw new InvalidOperationException(
                            $"SAP_REACTION={reaction}: use {StagedReaction.Kinds}"
                        );
                    }

                    if (string.Equals(screen, "coachtalk", StringComparison.Ordinal))
                    {
                        app.Screens.Push(new CoachTalkScreen(app)); // PLAY NEXT GAME: the pre-game talk
                    }

                    break;
                case "trophy":
                    Champions(app, clubs);
                    app.Screens.Push(new BracketScreen(app));
                    app.Screens.Push(TrophyScreen.Season(app));
                    break;
                case "arcadetrophy":
                    app.Screens.Push(TrophyScreen.Arcade(app, ArcadeRuns(app, clubs)));
                    break;
                case "bestruns":
                    _ = ArcadeRuns(app, clubs);
                    app.Screens.Push(new ArcadeBoardScreen(app));
                    break;
                default:
                    break;
            }
        }

        /// <summary>A fresh test season with a few rounds settled on paper (the rest of each round follows).</summary>
        public static void Season(
            AppState app,
            IReadOnlyList<ClubData> clubs,
            string name,
            int games
        )
        {
            string path = TestPath(SeasonSave.DefaultPath, name);
            SeasonState season = app.Season.StartNew(clubs, Club(clubs), Seed(), path);
            season.Rules = RulePicks.Choice(app).Resolve(); // as the first played game would store them
            int settled = 0;
            while (settled < games && app.Season.SimCurrentGame().HasValue)
            {
                settled++; // one at a time, as "sim this game" does
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: test season in {path}, {app.Season.Current.Select(s => s.Games.Count(g => g.Played)).Or(0)} games played"
                )
            );
        }

        /// <summary>A fresh test season settled on paper to the end of the playoffs, so there is a champion.</summary>
        public static void Champions(AppState app, IReadOnlyList<ClubData> clubs)
        {
            Season(app, clubs, "trophy-test.json", int.MaxValue);
            Console.WriteLine(
                $"SAP: screens: champions {(app.Playoffs.Current.TryGet(out PlayoffState playoffs) ? playoffs.Champion.Or("") : "")}"
            );
        }

        /// <summary>A test arcade save with a few finished runs on its board: one lost early, one lost late and,
        /// last, a three-lives run won, which stays open for the trophy screen.</summary>
        public static ArcadeRun ArcadeRuns(AppState app, IReadOnlyList<ClubData> clubs)
        {
            string path = TestPath(ArcadeSave.DefaultPath, "arcade-test.json");
            ArcadeSave.Delete(path);
            string club = Club(clubs);
            List<string> others =
            [
                .. clubs
                    .Where(c => !string.Equals(c.Id, club, StringComparison.Ordinal))
                    .Select(c => c.Id),
            ];
            _ = Run(app, clubs, others[2 % others.Count], ArcadeRule.SuddenDeath, path, 3);
            _ = Run(app, clubs, others[5 % others.Count], ArcadeRule.ThreeLives, path, 9);
            ArcadeRun run = Run(app, clubs, club, ArcadeRule.ThreeLives, path, int.MaxValue);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: test arcade board in {path}, {run.Records.Count} runs, this one place {run.Place}"
                )
            );
            return run;
        }

        /// <summary>Plays a run on paper: wins until <paramref name="wins"/>, then losses until it is over.</summary>
        private static ArcadeRun Run(
            AppState app,
            IReadOnlyList<ClubData> clubs,
            string club,
            ArcadeRule rule,
            string path,
            int wins
        )
        {
            ArcadeRun run = app.Arcade.StartNew(clubs, club, rule, Seed(), path);
            int game = 0;
            while (!run.IsOver && app.Arcade.NextSetup(InputDevice.None) is NextMatch.Play)
            {
                bool win = run.Stage < wins && game != 3; // one loss on the way
                int mine = win ? 3 + (game % 3) : 1,
                    theirs = win ? game % 3 : 2 + (game % 2);
                _ = app.Arcade.Finish(
                    run.NextAtHome
                        ? new FixtureResult(mine, theirs, ResultKind.Regulation)
                        : new FixtureResult(theirs, mine, ResultKind.Regulation)
                );
                game++;
            }
            return run;
        }

        private static string TestPath(string beside, string name)
        {
            return Path.Combine(Path.GetDirectoryName(beside), name);
        }

        private static string Club(IReadOnlyList<ClubData> clubs)
        {
            return Env.Get("SAP_CLUB") is { Length: > 0 } c
                ? c
                : Scenes.DefaultSetup(clubs).Home.Id;
        }

        private static int Seed()
        {
            return int.TryParse(
                Env.Get("SAP_SEED"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int s
            )
                ? s
                : 33;
        }
    }
}
