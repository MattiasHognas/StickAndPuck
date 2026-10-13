using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Screens;

/// <summary>What the match being played is, so the results screen knows where to go next.</summary>
/// <param name="Mode">The game mode, or null for a season game.</param>
/// <param name="Picks">The clubs the player picked for the mode.</param>
internal sealed record MatchPlan(IGameMode? Mode, IReadOnlyList<ClubData> Picks)
{
    /// <summary>Gets a value indicating whether the match is a season game.</summary>
    public bool Season => Mode == null;
}

/// <summary>A goal for the results screen.</summary>
/// <param name="Side">The scoring club.</param>
/// <param name="Period">"1" to "3", "OT" or "SO".</param>
/// <param name="Clock">The clock when it went in.</param>
/// <param name="Scorer">"#NR NAME", or the club for an own goal.</param>
internal sealed record GoalLine(Side Side, string Period, string Clock, string Scorer);

/// <summary>How a match ended.</summary>
internal sealed record MatchResult(MatchSetup Setup, int HomeGoals, int AwayGoals, int HomeShots, int AwayShots, string Decided, IReadOnlyList<GoalLine> Goals, MatchPlan Plan);

/// <summary>
/// W11: where the game is between screens and matches. Game.cs asks <see cref="TakeNextWorld"/>
/// after every frame's steps; when a screen starts a match or goes back to the menus, the loop
/// swaps in the new world with fresh systems. Inactive (and so out of the way) in every
/// SAP_SCENE except the start screen's.
/// </summary>
internal static class ScreenFlow
{
    static readonly List<Screen> Stack = [];
    static World? next;

    /// <summary>Gets a value indicating whether the screens run (the start scene was picked).</summary>
    public static bool Active { get; private set; }

    /// <summary>Gets a value indicating whether the player chose Quit.</summary>
    public static bool QuitRequested { get; private set; }

    /// <summary>Gets every club.</summary>
    public static IReadOnlyList<ClubData> Clubs { get; private set; } = [];

    /// <summary>Gets or sets the player's settings as saved; <see cref="Settings"/> is what runs.</summary>
    public static ScreenPrefs Prefs { get; set; } = new();

    /// <summary>Gets the settings to run with: the saved ones under the SAP_* overrides, which never get saved.</summary>
    public static Settings Settings => Core.Settings.FromEnvironment(Prefs.Settings);

    /// <summary>Gets the match being played, or null on the menus.</summary>
    public static MatchPlan? Playing { get; private set; }

    /// <summary>Gets the clubs on the rink behind the menus.</summary>
    static MatchSetup? Backdrop { get; set; }

    /// <summary>Gets the top screen, or null while a match runs.</summary>
    public static Screen? Top => Stack.Count > 0 ? Stack[^1] : null;

    /// <summary>Gets the screens, bottom first.</summary>
    public static IReadOnlyList<Screen> Screens => Stack;

    /// <summary>Turns the screens on and builds the start screen's world.</summary>
    /// <param name="clubs">Every club.</param>
    /// <param name="first">The first screen.</param>
    /// <returns>The world behind the start screen.</returns>
    public static World Begin(IReadOnlyList<ClubData> clubs, Screen first)
    {
        Active = true;
        Clubs = clubs;
        Prefs = ScreenPrefsFile.Read();
        MenuInput.LoadScript();
        Stack.Clear();
        Stack.Add(first);
        return MenuWorld();
    }

    /// <summary>Takes the world a screen asked for, if any. Game.cs swaps it in.</summary>
    /// <returns>The new world, or null to keep the current one.</returns>
    public static World? TakeNextWorld()
    {
        var w = next;
        next = null;
        return w;
    }

    /// <summary>Ends the game after this frame.</summary>
    public static void Quit()
    {
        Console.WriteLine("SAP: screens: quit");
        QuitRequested = true;
    }

    /// <summary>Opens a screen over the current one.</summary>
    public static void Push(Screen screen)
    {
        Stack.Add(screen);
    }

    /// <summary>Closes the top screen.</summary>
    public static void Pop()
    {
        if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
    }

    /// <summary>Replaces the top screen.</summary>
    public static void Replace(Screen screen)
    {
        Pop();
        Push(screen);
    }

    /// <summary>Closes every screen (resuming a paused match).</summary>
    public static void Clear()
    {
        Stack.Clear();
    }

    /// <summary>Leaves a match (or the results) for the menus, with the given screen on top of the start screen.</summary>
    /// <param name="screen">The screen to show, or null for the start screen alone.</param>
    public static void ToMenus(Screen? screen = null)
    {
        if (Playing != null) EndSessions();    // a game left early is not recorded; continuing reads the save again
        Playing = null;
        Stack.Clear();
        Stack.Add(new StartScreen());
        if (screen != null) Stack.Add(screen);
        next = MenuWorld();
        Console.WriteLine($"SAP: screens: menus, {Describe()}");
    }

    /// <summary>Shows the results of the match that just ended, on the menu rink.</summary>
    public static void ShowResults(MatchResult result)
    {
        var plan = Playing;
        Playing = null;
        Backdrop = result.Setup;
        Stack.Clear();
        Stack.Add(new StartScreen());
        Stack.Add(new ResultsScreen(result));
        next = MenuWorld();
        Console.WriteLine($"SAP: screens: results {result.Setup.Home.Id} {result.HomeGoals}-{result.AwayGoals} {result.Setup.Away.Id}{(result.Decided.Length > 0 ? " " + result.Decided : "")}, {(plan?.Season == true ? "season" : plan?.Mode?.Title)}");
    }

    /// <summary>
    /// Plays the human's next season game; once the regular season is over, their next playoff game
    /// (W16b starts the playoffs from the final table). With nothing left to play, shows the table or bracket.
    /// </summary>
    public static void PlaySeasonGame()
    {
        ArcadeSession.End();
        if (SeasonSession.Current == null) SeasonSession.Continue(Clubs);
        var setup = SeasonSession.NextSetup(Device()) ?? PlayoffSession.NextSetup(Device());
        if (setup == null)
        {
            ToMenus(Overview());
            return;
        }
        Start(setup, new MatchPlan(null, []));
    }

    /// <summary>
    /// W26: settles the human's next season or playoff game on paper instead of playing it
    /// (<see cref="SeasonSession.SimCurrentGame"/>). W33: the table or bracket comes back with the
    /// simmed score on top, which offers the trophy when the sim decided the final.
    /// </summary>
    public static void SimSeasonGame()
    {
        ArcadeSession.End();
        if (SeasonSession.Current == null) SeasonSession.Continue(Clubs);
        bool crowned = PlayoffSession.Current?.Champion != null;
        var simmed = SeasonSession.SimCurrentGame();
        crowned = !crowned && PlayoffSession.Current?.Champion != null;
        if (simmed != null) Console.WriteLine($"SAP: screens: simmed {simmed.Home} {simmed.Result.HomeGoals}-{simmed.Result.AwayGoals} {simmed.Away} ({simmed.Stage}){(crowned ? ", champions crowned" : "")}");
        while (Top is SimResultScreen or ResultsScreen) Pop();
        var overview = Overview();
        if (Top is StandingsScreen or BracketScreen)
        {
            if (Top.GetType() != overview.GetType()) Replace(overview);    // the table draws live; only swap to the bracket when the playoffs start
        }
        else
        {
            Push(overview);
        }
        if (simmed != null) Push(new SimResultScreen(simmed, crowned));
    }

    /// <summary>Gets a value indicating whether the season screens offer "Sim this game" (the SimGame setting, on by default).</summary>
    /// <returns>True when the entry shows.</returns>
    public static bool CanSimGame()
    {
        return Settings.SimGame && SeasonHasNextGame();
    }

    /// <summary>Gets a value indicating whether the human's club has a season or playoff game left.</summary>
    /// <returns>True when there is a next game, or the playoffs have yet to start.</returns>
    public static bool SeasonHasNextGame()
    {
        if (SeasonSession.Current is not { } season) return false;
        if (!season.IsOver) return season.NextGame != null;
        return season.Playoffs == null || season.Playoffs.UserSeries != null;
    }

    /// <summary>The season screen that fits: the bracket once the playoffs have started, the table before.</summary>
    /// <returns>The screen.</returns>
    public static Screen Overview()
    {
        return SeasonSession.Current?.Playoffs != null ? new BracketScreen() : new StandingsScreen();
    }

    /// <summary>Starts a game mode with the player's picks.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="picks">The clubs picked.</param>
    public static void PlayMode(IGameMode mode, IReadOnlyList<ClubData> picks)
    {
        EndSessions();    // only the mode's own games are recorded; a mode with a save opens it again
        var setup = mode.Setup(new GameModeStart(Clubs, picks, Device()));
        if (setup == null)
        {
            ToMenus();
            return;
        }
        Start(setup, new MatchPlan(mode, picks));
    }

    /// <summary>Applies the settings to the world on show and saves them.</summary>
    /// <param name="world">The world.</param>
    public static void ApplySettings(World world)
    {
        Apply(world, Settings);
        ScreenPrefsFile.Write(Prefs);
    }

    /// <summary>
    /// Gives the world its settings and points the debug switches that live as statics in their
    /// systems (W7's match line, W13's energy panel) at them, so a change on the settings screen
    /// shows at once, in the paused match too.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="settings">The settings.</param>
    public static void Apply(World world, Settings settings)
    {
        world.Settings = settings with { Penalties = world.Setup.Rules.Penalties, Fights = world.Setup.Rules.Fights };    // W44: the match's rules switch W14 and W15
        MatchRules.ShowLine = settings.MatchDebug;
        Lines.LinesSystem.ShowPanel = settings.LinesDebug;
    }

    /// <summary>The menu backdrop: both starting lineups standing at centre ice, paused.</summary>
    static World MenuWorld()
    {
        var world = new World(Backdrop ?? Scenes.DefaultSetup(Clubs), Settings);
        Apply(world, world.Settings);
        Scenes.AddStarters(world);
        world.Match.Paused = true;
        return world;
    }

    static void Start(MatchSetup setup, MatchPlan plan)
    {
        setup = (setup with { PeriodSeconds = PeriodSeconds() }).WithRules(RulePicks.For(plan));
        var world = Build(setup, Seeds.NewMatch());    // W67: every match has a seed, written to its replay
        Playing = plan;
        Backdrop = setup;
        Started = setup;
        Stack.Clear();
        next = world;
        Console.WriteLine($"SAP: screens: match {setup.Home.Id} vs {setup.Away.Id}, {(plan.Season ? "season" : plan.Mode!.Title)}, {setup.PeriodSeconds:0} s periods, seed {world.Seed}, {world.Settings}, {setup.Rules}");
    }

    /// <summary>Gets the setup the last match started from, before the difficulty scaled it: what its replay rebuilds it from (W67).</summary>
    public static MatchSetup? Started { get; private set; }

    /// <summary>
    /// W67: rebuilds a match from its replay, as <see cref="Start"/> built it: the screens run with the
    /// replay's settings (nothing is saved) and no menus, and the match starts at once.
    /// </summary>
    /// <param name="clubs">Every club.</param>
    /// <param name="prefs">The settings the match was played with.</param>
    /// <param name="setup">The setup it started from.</param>
    /// <param name="mode">The game mode's type name, "" for a season game.</param>
    /// <param name="seed">Its seed.</param>
    /// <returns>The match.</returns>
    public static World BeginReplay(IReadOnlyList<ClubData> clubs, ScreenPrefs prefs, MatchSetup setup, int seed, string mode)
    {
        Active = true;
        Clubs = clubs;
        Prefs = prefs;
        Stack.Clear();
        var world = Build(setup, seed);
        Playing = new MatchPlan(GameModes.Named(mode), [setup.Home, setup.Away]);    // the results screen offers what it offered in the match
        Backdrop = setup;
        Started = setup;
        return world;
    }

    static World Build(MatchSetup setup, int seed)
    {
        var world = new World(setup, Settings) { Seed = seed };
        Apply(world, world.Settings);
        Scenes.AddStarters(world);
        for (int slot = 0; slot < setup.Inputs.Count; slot++)
        {
            var input = setup.Inputs[slot];
            if (input.Device == InputDevice.None) continue;
            var skaters = world.OnIce(input.Side).Where(p => !p.IsGoalie && p.Slot < 0).ToList();
            if ((skaters.Find(p => p.Data.Position == PlayingPosition.C) ?? skaters.FirstOrDefault()) is { } centre) centre.Slot = slot;
        }
        MatchFlow.Begin(world);
        return world;
    }

    /// <summary>The period length from the settings; SAP_PERIOD=seconds overrides it for quick checks.</summary>
    static float PeriodSeconds()
    {
        return float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0
            ? s
            : Prefs.PeriodMinutes * 60f;
    }

    /// <summary>The human's device; SAP_HUMAN=0 lets the AI play both clubs.</summary>
    static InputDevice Device()
    {
        return string.Equals(Environment.GetEnvironmentVariable("SAP_HUMAN"), "0", StringComparison.Ordinal) ? InputDevice.None : Prefs.Controls;
    }

    /// <summary>Leaves season, playoff and arcade mode, so their systems record nothing until a mode opens its save again.</summary>
    static void EndSessions()
    {
        SeasonSession.End();
        PlayoffSession.End();
        ArcadeSession.End();
    }

    static string Describe()
    {
        return string.Join(" > ", Stack.Select(s => s.GetType().Name));
    }
}
