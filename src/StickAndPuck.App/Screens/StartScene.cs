using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs.Dev;

namespace StickAndPuck.App.Screens
{
    /// <summary>SAP_SCENE=start, the game's default: the start screen over the rink, and from there seasons,
    /// other modes, settings, pause, results and standings. For screenshots SAP_SCREEN opens one
    /// screen straight away: settings, modes, teams, pause (a match, paused), results (a made-up
    /// 4-3 overtime win) standings (the saved season), bracket (the playoff test save, its regular
    /// season settled on paper) or, from the test saves (<see cref="PolishScenes"/>), stats (the
    /// season stats page; leaders too, SAP_STATS_VIEW picks the list), sim (a game simmed from the
    /// table), trophy (the SHL champion), arcadetrophy (an arcade run won) or bestruns (the board). SAP_SCREEN_KEYS scripts the presses
    /// (<see cref="MenuScript"/>), SAP_PERIOD=seconds shortens periods and SAP_HUMAN=0 lets the AI
    /// play, so a headless run can go start, match, results and back.</summary>
    public sealed class StartScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "start";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            string screen = Env.Get("SAP_SCREEN") is { Length: > 0 } s
                ? s.ToLowerInvariant()
                : "start";
            World world = app.Screens.Begin(clubs, new StartScreen(app));
            switch (screen)
            {
                case "start":
                    break;
                case "settings":
                    app.Screens.Push(new SettingsScreen(app));
                    break;
                case "modes":
                    app.Screens.Push(new ModeScreen(app));
                    break;
                case "teams":
                    ModeScreen.Choose(app, new ExhibitionMode());
                    break;
                case "standings":
                    if (!app.Season.Continue(clubs).HasValue)
                    {
                        _ = app.Season.StartNew(clubs, Scenes.DefaultSetup(clubs).Home.Id, 1);
                    }

                    app.Screens.Push(new StandingsScreen(app));
                    break;
                case "bracket":
                    PlayoffScenes.OpenTestSeason(app, clubs);
                    _ = app.Playoffs.Open();
                    app.Screens.Push(new BracketScreen(app));
                    break;
                case "results":
                    app.Screens.PlayMode(new ExhibitionMode(), []);
                    MatchSetup setup = Taken(app).Setup;
                    app.Screens.ShowResults(
                        new MatchResult(
                            setup,
                            4,
                            3,
                            31,
                            24,
                            "OT",
                            SampleGoals(setup),
                            app.Screens.Playing.TryGet(out MatchPlan plan)
                                ? plan
                                : new MatchPlan(new ExhibitionMode(), [])
                        )
                    );
                    world = Taken(app);
                    break;
                case "pause":
                    app.Screens.PlayMode(new ExhibitionMode(), []);
                    world = Taken(app);
                    world.Match.Phase = MatchPhase.Play;
                    app.Screens.Push(new PauseScreen(app));
                    break;
                case "leaders"
                or "stats"
                or "table"
                or "sim"
                or "dressingroom"
                or "trophy"
                or "arcadetrophy"
                or "bestruns":
                    PolishScenes.Open(app, screen, clubs);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"SAP_SCREEN={screen}: use start, settings, modes, teams, pause, results, standings, table, stats, sim, dressingroom, trophy, arcadetrophy, bestruns or bracket"
                    );
            }
            Console.WriteLine($"SAP: screens: {screen}");
            return world;
        }

        /// <summary>The world a screen just asked for; the scenes only take one after starting a match or showing results.</summary>
        private static World Taken(AppState app)
        {
            return app.Screens.TakeNextWorld().TryGet(out World next)
                ? next
                : throw new InvalidOperationException("the screens built no world");
        }

        private static List<GoalLine> SampleGoals(MatchSetup setup)
        {
            static string Shooter(ClubData club, int n)
            {
                PlayerData p = club
                    .Roster.Where(r => r.Position != PlayingPosition.G)
                    .Skip(n)
                    .First();
                return string.Create(CultureInfo.InvariantCulture, $"#{p.Number} {p.LastName}");
            }

            // The longest name on the home roster and an overtime goal, so the capture shows both fit
            PlayerData longest = setup
                .Home.Roster.Where(r => r.Position != PlayingPosition.G)
                .MaxBy(r => r.LastName.Length);
            return
            [
                new GoalLine(Side.Home, "1", "4:12", Shooter(setup.Home, 0)),
                new GoalLine(Side.Away, "1", "15:40", Shooter(setup.Away, 2)),
                new GoalLine(
                    Side.Home,
                    "2",
                    "1:09",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"#{longest.Number} {longest.LastName}"
                    )
                ),
                new GoalLine(Side.Away, "2", "8:03", Shooter(setup.Away, 0)),
                new GoalLine(Side.Away, "3", "11:37", Shooter(setup.Away, 4)),
                new GoalLine(Side.Home, "3", "18:51", Shooter(setup.Home, 3)),
                new GoalLine(Side.Home, "OT", "2:27", Shooter(setup.Home, 1)),
            ];
        }
    }
}
