using StickAndPuck.Core;
using StickAndPuck.Season;

namespace StickAndPuck.Screens;

/// <summary>
/// SAP_SCENE=start, the game's default: the start screen over the rink, and from there seasons,
/// other modes, settings, pause, results and standings. For screenshots SAP_SCREEN opens one
/// screen straight away: settings, modes, teams, pause (a match, paused), results (a made-up
/// 4-3 overtime win) standings (the saved season), bracket (W16b's playoff test save, its regular
/// season settled on paper) or, from W33's test saves (<see cref="PolishScenes"/>), stats (the
/// season stats page; leaders too, SAP_STATS_VIEW picks the list), sim (a game simmed from the
/// table), trophy (the SHL champion), arcadetrophy (an arcade run won) or bestruns (the board). SAP_SCREEN_KEYS scripts the presses
/// (<see cref="MenuInput"/>), SAP_PERIOD=seconds shortens periods and SAP_HUMAN=0 lets the AI
/// play, so a headless run can go start, match, results and back.
/// </summary>
public sealed class StartScene : IScene
{
    /// <inheritdoc/>
    public string Name => "start";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        string screen = Environment.GetEnvironmentVariable("SAP_SCREEN") is { Length: > 0 } s ? s.ToLowerInvariant() : "start";
        var world = ScreenFlow.Begin(clubs, new StartScreen());
        switch (screen)
        {
            case "start":
                break;
            case "settings":
                ScreenFlow.Push(new SettingsScreen());
                break;
            case "modes":
                ScreenFlow.Push(new ModeScreen());
                break;
            case "teams":
                ModeScreen.Choose(new ExhibitionMode());
                break;
            case "standings":
                if (SeasonSession.Continue(clubs) == null) SeasonSession.StartNew(clubs, Scenes.DefaultSetup(clubs).Home.Id, 1);
                ScreenFlow.Push(new StandingsScreen());
                break;
            case "bracket":
                PlayoffScenes.OpenTestSeason(clubs);
                PlayoffSession.Open();
                ScreenFlow.Push(new BracketScreen());
                break;
            case "results":
                ScreenFlow.PlayMode(new ExhibitionMode(), []);
                var setup = ScreenFlow.TakeNextWorld()!.Setup;
                ScreenFlow.ShowResults(new MatchResult(setup, 4, 3, 31, 24, "OT", SampleGoals(setup), ScreenFlow.Playing ?? new MatchPlan(new ExhibitionMode(), [])));
                world = ScreenFlow.TakeNextWorld()!;
                break;
            case "pause":
                ScreenFlow.PlayMode(new ExhibitionMode(), []);
                world = ScreenFlow.TakeNextWorld()!;
                world.Match.Phase = MatchPhase.Play;
                ScreenFlow.Push(new PauseScreen());
                break;
            case "leaders" or "stats" or "sim" or "trophy" or "arcadetrophy" or "bestruns":
                PolishScenes.Open(screen, clubs);
                break;
            default:
                throw new InvalidOperationException($"SAP_SCREEN={screen}: use start, settings, modes, teams, pause, results, standings, stats, sim, trophy, arcadetrophy, bestruns or bracket");
        }
        Console.WriteLine($"SAP: screens: {screen}");
        return world;
    }

    static List<GoalLine> SampleGoals(MatchSetup setup)
    {
        string Shooter(ClubData club, int n)
        {
            var p = club.Roster.Where(r => r.Position != PlayingPosition.G).Skip(n).First();
            return $"#{p.Number} {p.LastName}";
        }

        // W33: the longest name on the home roster and an overtime goal, so the capture shows both fit
        var longest = setup.Home.Roster.Where(r => r.Position != PlayingPosition.G).MaxBy(r => r.LastName.Length)!;
        return
        [
            new GoalLine(Side.Home, "1", "4:12", Shooter(setup.Home, 0)),
            new GoalLine(Side.Away, "1", "15:40", Shooter(setup.Away, 2)),
            new GoalLine(Side.Home, "2", "1:09", $"#{longest.Number} {longest.LastName}"),
            new GoalLine(Side.Away, "2", "8:03", Shooter(setup.Away, 0)),
            new GoalLine(Side.Away, "3", "11:37", Shooter(setup.Away, 4)),
            new GoalLine(Side.Home, "3", "18:51", Shooter(setup.Home, 3)),
            new GoalLine(Side.Home, "OT", "2:27", Shooter(setup.Home, 1)),
        ];
    }
}
