using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Screens
{
    /// <summary>Where the game is between screens and matches. Game.cs asks <see cref="TakeNextWorld"/>
    /// after every frame's steps; when a screen starts a match or goes back to the menus, the loop
    /// swaps in the new world with fresh systems. Inactive (and so out of the way) in every
    /// SAP_SCENE except the start screen's.</summary>
    internal sealed class ScreenFlow(AppState app)
    {
        private readonly List<Screen> stack = [];
        private Maybe<World> next;
        private Maybe<ScreenPrefs> matchPrefs;
        private IReadOnlyList<ClubData> shlClubs = [];
        private IReadOnlyList<ClubData> nhlClubs = [];

        /// <summary>Gets a value indicating whether the screens run (the start scene was picked).</summary>
        public bool Active { get; private set; }

        /// <summary>Gets a value indicating whether the player chose Quit.</summary>
        public bool QuitRequested { get; private set; }

        /// <summary>Gets the clubs of the league the menus play in (<see cref="League"/>).</summary>
        public IReadOnlyList<ClubData> Clubs => ClubsOf(League);

        /// <summary>Gets the league the menus play in: the player's pick (<see cref="ScreenPrefs.League"/>), or
        /// SAP_LEAGUE=shl|nhl, which is never saved.</summary>
        public League League =>
            Leagues.Parse(Env.Get("SAP_LEAGUE")).TryGet(out League forced) ? forced : Prefs.League;

        /// <summary>Gets a league's clubs, read once.</summary>
        public IReadOnlyList<ClubData> ClubsOf(League league)
        {
            if (league == League.Nhl && nhlClubs.Count == 0)
            {
                nhlClubs = Core.Clubs.LoadNhl();
            }
            else if (league == League.Shl && shlClubs.Count == 0)
            {
                shlClubs = Core.Clubs.LoadAll();
            }

            return league == League.Nhl ? nhlClubs : shlClubs;
        }

        /// <summary>Picks the league the menus play in. Its clubs show on team select, its season and
        /// arcade saves are the ones continued, and the rules go to the league's preset (the player can
        /// change them after). The sessions of the other league close and the menu rink shows the new
        /// league's clubs. The caller saves the settings.</summary>
        public void PickLeague(League league)
        {
            if (league == Prefs.League)
            {
                return;
            }

            Prefs = Prefs with { League = league, Rules = RuleChoice.Of(Leagues.Preset(league)) };
            EndSessions();
            if (Active && !Playing.HasValue)
            {
                Backdrop = Maybe.None<MatchSetup>();
                next = MenuWorld();
            }

            Console.WriteLine(
                $"SAP: screens: league {league}, {Clubs.Count} clubs, {Prefs.Rules.Resolve().Name} rules"
            );
        }

        /// <summary>Gets or sets the player's settings as saved; <see cref="Settings"/> is what runs.</summary>
        public ScreenPrefs Prefs { get; set; } = new();

        /// <summary>Gets the settings to run with: the saved ones under the SAP_* overrides, which never get saved.</summary>
        public Settings Settings => Settings.FromEnvironment(Prefs.Settings);

        /// <summary>Gets the match being played, or None on the menus.</summary>
        public Maybe<MatchPlan> Playing { get; private set; }

        /// <summary>Gets the clubs on the rink behind the menus.</summary>
        private Maybe<MatchSetup> Backdrop { get; set; }

        /// <summary>Gets the top screen, or None while a match runs.</summary>
        public Maybe<Screen> Top => stack.Count > 0 ? stack[^1] : Maybe.None<Screen>();

        /// <summary>Gets the screens, bottom first.</summary>
        public IReadOnlyList<Screen> Screens => stack;

        /// <summary>Turns the screens on and builds the start screen's world.</summary>
        public World Begin(IReadOnlyList<ClubData> clubs, Screen first)
        {
            return Begin(clubs, first, Maybe.None<MatchSetup>());
        }

        /// <summary>Turns the screens on and builds the start screen's world, with these clubs on the rink behind (None: the default clubs).</summary>
        public World Begin(IReadOnlyList<ClubData> clubs, Screen first, Maybe<MatchSetup> backdrop)
        {
            Backdrop = backdrop;
            Active = true;
            UseClubs(clubs);
            Prefs = ScreenPrefsFile.Read();
            app.MenuScript.Load();
            stack.Clear();
            stack.Add(first);
            return MenuWorld();
        }

        /// <summary>Takes the world a screen asked for, if any. Game.cs swaps it in.</summary>
        public Maybe<World> TakeNextWorld()
        {
            Maybe<World> w = next;
            next = Maybe.None<World>();
            return w;
        }

        /// <summary>Ends the game after this frame.</summary>
        public void Quit()
        {
            Console.WriteLine("SAP: screens: quit");
            QuitRequested = true;
        }

        /// <summary>Opens a screen over the current one.</summary>
        public void Push(Screen screen)
        {
            stack.Add(screen);
        }

        /// <summary>Closes the top screen.</summary>
        public void Pop()
        {
            if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        /// <summary>Replaces the top screen.</summary>
        public void Replace(Screen screen)
        {
            Pop();
            Push(screen);
        }

        /// <summary>Closes every screen (resuming a paused match).</summary>
        public void Clear()
        {
            stack.Clear();
        }

        /// <summary>Leaves a match (or the results) for the menus, with the start screen alone.</summary>
        public void ToMenus()
        {
            ToMenus(Maybe.None<Screen>());
        }

        /// <summary>Leaves a match (or the results) for the menus, with the given screen on top of the start screen.</summary>
        public void ToMenus(Screen screen)
        {
            ToMenus(Maybe.Some(screen));
        }

        private void ToMenus(Maybe<Screen> screen)
        {
            if (Playing.HasValue)
            {
                EndSessions(); // a game left early is not recorded; continuing reads the save again
            }

            Playing = Maybe.None<MatchPlan>();
            stack.Clear();
            stack.Add(new StartScreen(app));
            if (screen.TryGet(out Screen shown))
            {
                stack.Add(shown);
            }

            next = MenuWorld();
            Console.WriteLine($"SAP: screens: menus, {Describe()}");
        }

        /// <summary>Shows the results of the match that just ended, on the menu rink.</summary>
        public void ShowResults(MatchResult result)
        {
            Maybe<MatchPlan> plan = Playing;
            Playing = Maybe.None<MatchPlan>();
            Backdrop = result.Setup;
            stack.Clear();
            stack.Add(new StartScreen(app));
            stack.Add(new ResultsScreen(app, result));
            next = MenuWorld();
            string decidedText = result.Decided.Length > 0 ? " " + result.Decided : "";
            string modeTitle = "";
            if (plan.TryGet(out MatchPlan ended))
            {
                modeTitle = ended.Mode.TryGet(out IGameMode mode) ? mode.Title : "season";
            }
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: results {result.Setup.Home.Id} {result.HomeGoals}-{result.AwayGoals} {result.Setup.Away.Id}{decidedText}, {modeTitle}"
                )
            );
        }

        /// <summary>Plays the human's next season game; once the regular season is over, their next playoff game
        /// (the playoffs start from the final table). With nothing left to play, shows the table or bracket.</summary>
        public void PlaySeasonGame()
        {
            PlaySeasonGame(Maybe.None<TalkTone>());
        }

        /// <summary>Plays the human's next season or playoff game after the coach's pre-game talk (W125): the
        /// tone goes on the setup (<see cref="MatchSetup.Talk"/>, for the human's club's side) so the match
        /// and its replay play it.</summary>
        public void PlaySeasonGameAfterTalk(TalkTone tone)
        {
            PlaySeasonGame(Maybe.Some(tone));
        }

        private void PlaySeasonGame(Maybe<TalkTone> talk)
        {
            app.Arcade.End();
            if (!app.Season.Current.HasValue)
            {
                _ = app.Season.Continue(Clubs);
            }

            NextMatch match = app.Season.NextSetup(Device());
            if (match is not NextMatch.Play)
            {
                match = app.Playoffs.NextSetup(Device());
            }

            if (match is not NextMatch.Play play)
            {
                ToMenus(Overview());
                return;
            }
            Start(WithTalk(play.Setup, talk), MatchPlan.SeasonGame());
        }

        /// <summary>A season setup with the coach's talk to the human's club, when one was given and the rules play it.</summary>
        private MatchSetup WithTalk(MatchSetup setup, Maybe<TalkTone> talk)
        {
            if (
                !talk.TryGet(out TalkTone tone)
                || !setup.Rules.PreGameTalk
                || !app.Season.Current.TryGet(out SeasonState season)
            )
            {
                return setup;
            }

            Side side = string.Equals(setup.Home.Id, season.UserClub, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Console.WriteLine($"SAP: screens: pre-game talk {tone} to {season.UserClub}");
            return setup with { Talk = new CoachTalk(side, tone) };
        }

        /// <summary>Settles the human's next season or playoff game on paper instead of playing it
        /// (<see cref="SeasonSession.SimCurrentGame"/>). The table or bracket comes back with the
        /// simmed score on top, which offers the trophy when the sim decided the final.</summary>
        public void SimSeasonGame()
        {
            app.Arcade.End();
            if (!app.Season.Current.HasValue)
            {
                _ = app.Season.Continue(Clubs);
            }

            bool crowned = app.Playoffs.Current.Is(p => p.Champion.HasValue);
            Maybe<SimmedGame> simmed = app.Season.SimCurrentGame();
            crowned = !crowned && app.Playoffs.Current.Is(p => p.Champion.HasValue);
            if (simmed.TryGet(out SimmedGame game))
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: screens: simmed {game.Home} {game.Result.HomeGoals}-{game.Result.AwayGoals} {game.Away} ({game.Stage}){(crowned ? ", champions crowned" : "")}"
                    )
                );
            }

            while (Top.Is(t => t is SimResultScreen or ResultsScreen))
            {
                Pop();
            }

            Screen overview = AfterSeasonGame();
            if (
                Top.TryGet(out Screen top)
                && top is StandingsScreen or BracketScreen or DressingRoomScreen
            )
            {
                if (top.GetType() != overview.GetType())
                {
                    Replace(overview); // the table draws live; only swap to the bracket when the playoffs start
                }
            }
            else
            {
                Push(overview);
            }
            if (simmed.TryGet(out SimmedGame settled))
            {
                Push(new SimResultScreen(app, settled, crowned));
            }
        }

        /// <summary>Gets a value indicating whether the season screens offer "Sim this game" (the SimGame setting, on by default).</summary>
        public bool CanSimGame()
        {
            return Settings.SimGame && SeasonHasNextGame();
        }

        /// <summary>Gets a value indicating whether the human's club has a season or playoff game left.</summary>
        public bool SeasonHasNextGame()
        {
            return app.Season.Current.TryGet(out SeasonState season)
                && (
                    !season.IsOver
                        ? season.NextGame.HasValue
                        : !season.Playoffs.TryGet(out PlayoffState playoffs)
                            || playoffs.UserSeries.HasValue
                );
        }

        /// <summary>The season screen that fits: the bracket once the playoffs have started, the table before.</summary>
        public Screen Overview()
        {
            return app.Season.Current.Is(s => s.Playoffs.HasValue)
                ? new BracketScreen(app)
                : new StandingsScreen(app);
        }

        /// <summary>The screen after a season or playoff result: the dressing room while there is a next game
        /// and the season's rules have it on (<see cref="RuleSet.DressingRoom"/>), else the table or bracket.</summary>
        public Screen AfterSeasonGame()
        {
            return SeasonHasNextGame() && DressingRoomScreen.Style(app) != DressingRoomStyle.Off
                ? new DressingRoomScreen(app)
                : Overview();
        }

        /// <summary>Starts a game mode with the player's picks.</summary>
        public void PlayMode(IGameMode mode, IReadOnlyList<ClubData> picks)
        {
            EndSessions(); // only the mode's own games are recorded; a mode with a save opens it again
            if (
                !mode.Setup(new GameModeStart(Clubs, picks, Device(), app))
                    .TryGet(out MatchSetup setup)
            )
            {
                ToMenus();
                return;
            }
            Start(setup, new MatchPlan(Maybe.Some(mode), picks));
        }

        /// <summary>Applies the settings to the world on show and saves them.</summary>
        public void ApplySettings(World world)
        {
            Apply(world, Settings);
            ScreenPrefsFile.Write(Prefs);
        }

        /// <summary>Gives the world its settings and points its debug switches (the match line, the energy
        /// panel) at them, so a change on the settings screen
        /// shows at once, in the paused match too.</summary>
        public static void Apply(World world, Settings settings)
        {
            world.Settings = settings with
            {
                Penalties = world.Setup.Rules.Penalties,
                Fights = world.Setup.Rules.Fights,
            }; // the match's rules switch penalties and fights
            Text.Use(settings.Language); // a language change shows at once
            world.Switches.MatchLine = settings.MatchDebug;
            world.Switches.LinesPanel = settings.LinesDebug;
        }

        /// <summary>The menu backdrop: both starting lineups standing at centre ice, paused.</summary>
        private World MenuWorld()
        {
            World world = new(
                Backdrop.TryGet(out MatchSetup backdrop) ? backdrop : Scenes.DefaultSetup(Clubs),
                Settings
            )
            {
                App = app,
            };
            Apply(world, world.Settings);
            Scenes.AddStarters(world);
            world.Match.Paused = true;
            return world;
        }

        /// <summary>Gives a mode's setup the period length from the settings and the rules the plan plays, as every match the screens start gets them.</summary>
        public MatchSetup Prepare(MatchSetup setup, MatchPlan plan)
        {
            return (setup with { PeriodSeconds = PeriodSeconds() }).WithRules(
                RulePicks.For(app, plan)
            );
        }

        /// <summary>Starts a match whose setup, seed and settings came from elsewhere (the host of a network
        /// match): built as <see cref="Start"/> builds one, but with <paramref name="settings"/> in place of this
        /// machine's, and <paramref name="recorded"/> written to its replay so the replay plays it the same.
        /// This machine's own settings stay as saved.</summary>
        public World StartShared(
            MatchSetup setup,
            MatchPlan plan,
            int seed,
            Settings settings,
            ScreenPrefs recorded
        )
        {
            EndSessions();
            World world = Build(setup, seed, settings);
            Playing = plan;
            Backdrop = setup;
            Started = setup;
            matchPrefs = recorded;
            stack.Clear();
            next = world;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: shared match {setup.Home.Id} vs {setup.Away.Id}, {setup.PeriodSeconds:0} s periods, seed {seed}, {world.Settings}, {setup.Rules}"
                )
            );
            return world;
        }

        /// <summary>Gets the settings the match being played records in its replay: the shared ones of a network match, else the saved ones.</summary>
        public ScreenPrefs RecordedPrefs => matchPrefs.Or(Prefs);

        private void Start(MatchSetup setup, MatchPlan plan)
        {
            setup = Prepare(setup, plan);
            World world = Build(setup, Seeds.NewMatch()); // every match has a seed, written to its replay
            matchPrefs = Maybe.None<ScreenPrefs>();
            Playing = plan;
            Backdrop = setup;
            Started = setup;
            stack.Clear();
            next = world;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: screens: match {setup.Home.Id} vs {setup.Away.Id}, {(plan.Mode.TryGet(out IGameMode mode) ? mode.Title : "season")}, {setup.PeriodSeconds:0} s periods, seed {world.Seed}, {world.Settings}, {setup.Rules}"
                )
            );
        }

        /// <summary>Gets the setup the last match started from, before the difficulty scaled it: what its replay rebuilds it from, or None before the first.</summary>
        public Maybe<MatchSetup> Started { get; private set; }

        /// <summary>Rebuilds a match from its replay, as <see cref="Start"/> built it: the screens run with the
        /// replay's settings (nothing is saved) and no menus, and the match starts at once.</summary>
        public World BeginReplay(
            IReadOnlyList<ClubData> clubs,
            ScreenPrefs prefs,
            MatchSetup setup,
            int seed,
            string mode
        )
        {
            Active = true;
            UseClubs(clubs);
            Prefs = prefs with { League = setup.League }; // the menus behind the replay list its league's clubs
            stack.Clear();
            World world = Build(setup, seed);
            matchPrefs = Maybe.None<ScreenPrefs>();
            Playing = new MatchPlan(GameModes.Named(mode), [setup.Home, setup.Away]); // the results screen offers what it offered in the match
            Backdrop = setup;
            Started = setup;
            return world;
        }

        private World Build(MatchSetup setup, int seed)
        {
            return Build(setup, seed, Settings);
        }

        private World Build(MatchSetup setup, int seed, Settings settings)
        {
            World world = new(setup, settings) { Seed = seed, App = app };
            Apply(world, world.Settings);
            Scenes.AddStarters(world);
            for (int slot = 0; slot < setup.Inputs.Count; slot++)
            {
                InputSlot input = setup.Inputs[slot];
                if (input.Device == InputDevice.None)
                {
                    continue;
                }

                List<Player> skaters =
                [
                    .. world.OnIce(input.Side).Where(p => !p.IsGoalie && p.Slot < 0),
                ];
                if (
                    (
                        skaters.Find(p => p.Data.Position == PlayingPosition.C)
                        ?? skaters.FirstOrDefault()
                    ) is
                    { } centre
                )
                {
                    centre.Slot = slot;
                }
            }
            MatchFlow.Begin(world);
            return world;
        }

        /// <summary>The period length from the settings; SAP_PERIOD=seconds overrides it for quick checks.</summary>
        private float PeriodSeconds()
        {
            return
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                ? s
                : Prefs.PeriodMinutes * 60f;
        }

        /// <summary>The human's device; SAP_HUMAN=0 lets the AI play both clubs.</summary>
        private InputDevice Device()
        {
            return Env.Is("SAP_HUMAN", "0") ? InputDevice.None : Prefs.Controls;
        }

        /// <summary>Leaves season, playoff and arcade mode, so their systems record nothing until a mode opens its save again.</summary>
        private void EndSessions()
        {
            app.Season.End();
            app.Playoffs.End();
            app.Arcade.End();
        }

        /// <summary>Keeps the clubs the game loaded as their league's list.</summary>
        private void UseClubs(IReadOnlyList<ClubData> clubs)
        {
            if (Leagues.Of(clubs) == League.Nhl)
            {
                nhlClubs = clubs;
            }
            else
            {
                shlClubs = clubs;
            }
        }

        private string Describe()
        {
            return string.Join(" > ", stack.Select(s => s.GetType().Name));
        }
    }
}
