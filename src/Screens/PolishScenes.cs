using StickAndPuck.Core;
using StickAndPuck.Season;

namespace StickAndPuck.Screens;

/// <summary>
/// W33: the saves behind the headless captures of the polished screens (SAP_SCREEN=stats, sim,
/// trophy, arcadetrophy and bestruns). Each writes its own test save next to season.json, so the
/// player's season and arcade saves are left alone.
/// </summary>
internal static class PolishScenes
{
    /// <summary>Opens one of W33's screens on its test save.</summary>
    /// <param name="screen">stats (or leaders), sim, trophy, arcadetrophy or bestruns.</param>
    /// <param name="clubs">Every club.</param>
    public static void Open(string screen, IReadOnlyList<ClubData> clubs)
    {
        switch (screen)
        {
            case "leaders" or "stats":
                Season(clubs, "stats-test.json", 12);
                ScreenFlow.Push(new SeasonStatsScreen().Showing(Environment.GetEnvironmentVariable("SAP_STATS_VIEW") ?? "POINTS"));
                break;
            case "sim":
                Season(clubs, "sim-test.json", 5);
                ScreenFlow.SimSeasonGame();
                break;
            case "trophy":
                Champions(clubs);
                ScreenFlow.Push(new BracketScreen());
                ScreenFlow.Push(TrophyScreen.Season());
                break;
            case "arcadetrophy":
                ScreenFlow.Push(TrophyScreen.Arcade(ArcadeRuns(clubs)));
                break;
            case "bestruns":
                ArcadeRuns(clubs);
                ScreenFlow.Push(new ArcadeBoardScreen());
                break;
            default:
                break;
        }
    }

    /// <summary>A fresh test season with a few rounds settled on paper (the rest of each round follows).</summary>
    /// <param name="clubs">Every club.</param>
    /// <param name="name">The test save's file name.</param>
    /// <param name="games">How many of the human's games to settle.</param>
    public static void Season(IReadOnlyList<ClubData> clubs, string name, int games)
    {
        string path = TestPath(SeasonSave.DefaultPath, name);
        SeasonSession.StartNew(clubs, Club(clubs), Seed(), path);
        int settled = 0;
        while (settled < games && SeasonSession.SimCurrentGame() != null) settled++;    // one at a time, as "sim this game" does
        Console.WriteLine($"SAP: screens: test season in {path}, {SeasonSession.Current!.Games.Count(g => g.Played)} games played");
    }

    /// <summary>A fresh test season settled on paper to the end of the playoffs, so there is a champion.</summary>
    /// <param name="clubs">Every club.</param>
    public static void Champions(IReadOnlyList<ClubData> clubs)
    {
        Season(clubs, "trophy-test.json", int.MaxValue);
        Console.WriteLine($"SAP: screens: champions {PlayoffSession.Current?.Champion}");
    }

    /// <summary>
    /// A test arcade save with a few finished runs on its board: one lost early, one lost late and,
    /// last, a three-lives run won, which stays open for the trophy screen.
    /// </summary>
    /// <param name="clubs">Every club.</param>
    /// <returns>The champion run.</returns>
    public static ArcadeRun ArcadeRuns(IReadOnlyList<ClubData> clubs)
    {
        string path = TestPath(ArcadeSave.DefaultPath, "arcade-test.json");
        ArcadeSave.Delete(path);
        string club = Club(clubs);
        var others = clubs.Where(c => !string.Equals(c.Id, club, StringComparison.Ordinal)).Select(c => c.Id).ToList();
        Run(clubs, others[2 % others.Count], ArcadeRule.SuddenDeath, path, 3);
        Run(clubs, others[5 % others.Count], ArcadeRule.ThreeLives, path, 9);
        var run = Run(clubs, club, ArcadeRule.ThreeLives, path, int.MaxValue);
        Console.WriteLine($"SAP: screens: test arcade board in {path}, {run.Records.Count} runs, this one place {run.Place}");
        return run;
    }

    /// <summary>Plays a run on paper: wins until <paramref name="wins"/>, then losses until it is over.</summary>
    static ArcadeRun Run(IReadOnlyList<ClubData> clubs, string club, ArcadeRule rule, string path, int wins)
    {
        var run = ArcadeSession.StartNew(clubs, club, rule, Seed(), path);
        for (int game = 0; !run.IsOver && ArcadeSession.NextSetup(InputDevice.None) != null; game++)
        {
            bool win = run.Stage < wins && game != 3;    // one loss on the way
            int mine = win ? 3 + (game % 3) : 1, theirs = win ? game % 3 : 2 + (game % 2);
            ArcadeSession.Finish(run.NextAtHome ? new FixtureResult(mine, theirs, ResultKind.Regulation) : new FixtureResult(theirs, mine, ResultKind.Regulation));
        }
        return run;
    }

    static string TestPath(string beside, string name)
    {
        return Path.Combine(Path.GetDirectoryName(beside)!, name);
    }

    static string Club(IReadOnlyList<ClubData> clubs)
    {
        return Environment.GetEnvironmentVariable("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
    }

    static int Seed()
    {
        return int.TryParse(Environment.GetEnvironmentVariable("SAP_SEED"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int s) ? s : 33;
    }
}
