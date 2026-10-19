using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.DressingRoom.Dev;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Screens
{
    /// <summary>The saves behind the headless captures of the polished screens (SAP_SCREEN=stats, table, sim,
    /// dressingroom, suspension, trade, fired, trophy, arcadetrophy and bestruns). Each writes its own test save next to season.json, so the
    /// player's season and arcade saves are left alone.</summary>
    internal static class PolishScenes
    {
        /// <summary>Opens one of the season screens on its test save.</summary>
        public static void Open(AppState app, string screen, IReadOnlyList<ClubData> clubs)
        {
            switch (screen)
            {
                case "leaders" or "stats":
                    Season(app, clubs, "stats-test.json", StatsGames());
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
                case "dressingroom" or "coachtalk" or "suspension":
                    Season(
                        app,
                        clubs,
                        string.Equals(screen, "suspension", StringComparison.Ordinal)
                            ? "suspension-test.json"
                            : "dressingroom-test.json",
                        6
                    );
                    if (string.Equals(screen, "suspension", StringComparison.Ordinal))
                    {
                        Suspend(app, clubs);
                    }

                    Challenge(app);
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
                case "trade":
                    TradeDeadline(app, clubs);
                    break;
                case "fired":
                    Fired(app, clubs);
                    app.Screens.Push(app.Screens.Overview()); // the fired screen
                    break;
                case "trophy":
                    Champions(app, clubs);
                    app.Screens.Push(new BracketScreen(app));
                    app.Screens.Push(TrophyScreen.Season(app));
                    break;
                case "playout" or "relegation":
                    OpenPlayout(app, screen, clubs);
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

        /// <summary>Gets the games the stats fixture settles: SAP_STATS_GAMES, else 12.</summary>
        private static int StatsGames()
        {
            return int.TryParse(
                Env.Get("SAP_STATS_GAMES"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int games
            )
                ? games
                : 12;
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
            RuleSet rules = RulePicks.Choice(app).Resolve(); // as the first played game would store them
            season.Rules = games == int.MaxValue ? rules with { HotSeat = HotSeat.Off } : rules; // a whole season on paper runs to its end, whatever the board thinks
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

        /// <summary>Suspends the human club's first centre for three games and its first right wing for one, as a
        /// second fight and a match penalty in its last game would (SAP_SCREEN=suspension); the game settled
        /// next serves one of them.</summary>
        private static void Suspend(AppState app, IReadOnlyList<ClubData> clubs)
        {
            if (!app.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            ClubData user = clubs.First(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            foreach (
                (PlayingPosition position, int games) in new[]
                {
                    (PlayingPosition.C, 3),
                    (PlayingPosition.RW, 2),
                }
            )
            {
                PlayerData p = user.Roster.First(x => x.Position == position);
                season.Stats.Absences.Add(
                    new Absence
                    {
                        Club = user.Id,
                        Number = p.Number,
                        Reason = AbsenceReason.Suspension,
                        Games = games,
                        GamesLeft = games,
                    }
                );
            }
        }

        /// <summary>A fresh test season settled on paper with "sim this game" up to the trade deadline (W135), then
        /// the screen after that result: the first offer.</summary>
        private static void TradeDeadline(AppState app, IReadOnlyList<ClubData> clubs)
        {
            Season(app, clubs, "trade-test.json", 0);
            while (
                app.Season.Current.Is(s => !s.Trades.Held) && app.Season.SimCurrentGame().HasValue
            )
            {
                // one game at a time, up to the deadline game where the offers come
            }

            app.Screens.Push(app.Screens.AfterSeasonGame());
        }

        /// <summary>Sets a coach's challenge (SAP_CHALLENGE=wintwo, shutout, goals or rival) before the game settled
        /// next, so the dressing room shows its first game's progress (W134).</summary>
        private static void Challenge(AppState app)
        {
            if (
                Env.Get("SAP_CHALLENGE") is not { Length: > 0 } name
                || !app.Season.Current.TryGet(out SeasonState season)
            )
            {
                return;
            }

            ChallengeKind kind = name.ToLowerInvariant() switch
            {
                "wintwo" => ChallengeKind.WinTwo,
                "shutout" => ChallengeKind.Shutout,
                "goals" => ChallengeKind.Goals,
                "rival" => ChallengeKind.BeatRival,
                _ => throw new InvalidOperationException(
                    $"SAP_CHALLENGE={name}: use wintwo, shutout, goals or rival"
                ),
            };
            if (!ChallengeDesk.Force(season, kind).HasValue)
            {
                Console.WriteLine($"SAP: screens: no {name} challenge fits the next three games");
            }
        }

        /// <summary>The playout (W76) screens: the bracket mid-playout, or the relegation screen after it.</summary>
        private static void OpenPlayout(AppState app, string screen, IReadOnlyList<ClubData> clubs)
        {
            bool relegation = string.Equals(screen, "relegation", StringComparison.Ordinal);
            Playout(app, clubs, relegation ? int.MaxValue : PlayoutGames());
            if (relegation)
            {
                Relegate(app);
            }

            app.Screens.Push(new BracketScreen(app));
            if (relegation)
            {
                app.Screens.Push(SeasonEnd.Screen(app));
            }
        }

        /// <summary>Gets the playout games the playout fixture settles: SAP_PLAYOUT_GAMES, else 3.</summary>
        private static int PlayoutGames()
        {
            return int.TryParse(
                Env.Get("SAP_PLAYOUT_GAMES"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int games
            )
                ? games
                : 3;
        }

        /// <summary>A fresh SHL test season settled on paper to the end of the regular season, the human's club
        /// then swapped to the 13th placed club, and that many of its playout games (W76) simmed.</summary>
        public static void Playout(AppState app, IReadOnlyList<ClubData> clubs, int games)
        {
            Season(app, clubs, "playout-test.json", 0);
            if (!app.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            while (!season.IsOver && app.Season.SimCurrentGame().HasValue)
            {
                // the regular season, one game at a time as "sim this game" does
            }

            season.UserClub = season.Table[PlayoffState.PlayoutHigh - 1].ClubId;
            int settled = 0;
            while (settled < games && app.Season.SimCurrentGame().HasValue)
            {
                settled++;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: playout test, {season.UserClub} in the playout, {settled} games simmed, relegated {season.Relegated.Or("not yet")}"
                )
            );
        }

        /// <summary>A fresh test season for the club the board expects most of (SAP_CLUB picks another), under the
        /// chosen rules with the hot seat at least lenient, losing every game until the board fires the human
        /// (SAP_SCREEN=fired).</summary>
        public static void Fired(AppState app, IReadOnlyList<ClubData> clubs)
        {
            string path = TestPath(SeasonSave.DefaultPath, "fired-test.json");
            string club = Env.Get("SAP_CLUB") is { Length: > 0 } c
                ? c
                : clubs.First(x => BoardExpectation.Rank(clubs, x.Id) == 1).Id;
            SeasonState season = app.Season.StartNew(clubs, club, Seed(), path);
            RuleSet rules = RulePicks.Choice(app).Resolve();
            season.Rules =
                rules.HotSeat == HotSeat.Off ? rules with { HotSeat = HotSeat.Lenient } : rules;
            while (!season.Fired && app.Season.NextSetup(InputDevice.None) is NextMatch.Play)
            {
                bool home = app.Season.Playing.Is(g =>
                    string.Equals(g.Home, club, StringComparison.Ordinal)
                );
                _ = app.Season.Finish(
                    home
                        ? new FixtureResult(1, 4, ResultKind.Regulation)
                        : new FixtureResult(4, 1, ResultKind.Regulation)
                );
            }

            SeasonSave.Write(season, path);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: test season in {path}, {club} fired {season.Fired} after {season.PlayedBy(club).Count()} games, place {BoardExpectation.Place(season)}"
                )
            );
        }

        /// <summary>Puts the human on the club that lost the playout, so the season ends on the relegation screen.</summary>
        private static void Relegate(AppState app)
        {
            if (
                app.Season.Current.TryGet(out SeasonState season)
                && season.Relegated.TryGet(out string down)
            )
            {
                season.UserClub = down;
                if (season.Playoffs.TryGet(out PlayoffState playoffs))
                {
                    playoffs.UserClub = down;
                }
            }
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
