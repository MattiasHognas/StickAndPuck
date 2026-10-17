using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>The start screen: continue the saved season, start a new season, a new game in another mode,
/// the standings, settings and quit. Continue and standings show only when there is a save.</summary>
internal sealed class StartScreen(AppState app) : MenuScreen(app)
{
    const int PanelW = 200;

    Maybe<SeasonState> save;
    bool read;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        if (!read)
        {
            save = SeasonSave.Read();
            read = true;
        }
        bool saved = save.HasValue;
        List<MenuItem> items = [new MenuItem(Text.Get(K.ContinueSeason), Flow.PlaySeasonGame, enabled: saved)];
        if (Flow.Settings.SimGame) items.Add(new MenuItem(Text.Get(K.SimNextGame), Flow.SimSeasonGame, enabled: Season.Is(HasNextGame)));    // off with the SimGame setting
        items.Add(new MenuItem(Text.Get(K.NewSeason), NewSeason));
        items.Add(new MenuItem(Text.Get(K.NewGame), () => Flow.Push(new ModeScreen(App))));
        items.Add(new MenuItem(Text.Get(K.Standings), Standings, enabled: saved));
        items.Add(new MenuItem(Text.Get(K.Settings), () => Flow.Push(new SettingsScreen(App))));
        items.Add(new MenuItem(Text.Get(K.Quit), Flow.Quit));
        return items;
    }

    /// <summary>Gets the season to describe: the open one (a game simmed from here changes it), else the save read on opening, or None.</summary>
    Maybe<SeasonState> Season => App.Season.Current.HasValue ? App.Season.Current : save;

    /// <summary>Whether the human's club has a season or playoff game left, as <see cref="ScreenFlow.SeasonHasNextGame"/> asks of the open season.</summary>
    static bool HasNextGame(SeasonState season)
    {
        if (!season.IsOver) return season.NextGame.HasValue;
        return !season.Playoffs.TryGet(out var playoffs) || playoffs.UserSeries.HasValue;
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        // the bottom of the stack: back does nothing here
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        art.DrawTitle(22);
        var items = Items();
        int h = (items.Count * ScreenArt.Row) + 10;
        var (x, top) = art.Panel("", PanelW, h, 80);
        art.Menu(items, Selected, x, PanelW, top);

        if (Season.TryGet(out var season))
        {
            string club = Flow.Clubs.FirstOrNone(c => string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)).Select(c => c.Name).Or(season.UserClub);
            int played = season.Games.Count(g => g.Played && g.Involves(season.UserClub));
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            string line;
            if (!season.IsOver)
            {
                line = Text.Format(K.StartGameOf, club, played + 1, total);
            }
            else if (season.Playoffs.Is(p => p.Champion.HasValue || !p.UserSeries.HasValue))
            {
                line = Text.Format(K.StartSeasonOver, club);
            }
            else
            {
                line = Text.Format(K.StartPlayoffs, club);
            }
            art.Centred(line, Game.Width / 2, 80 + h + 6, art.Text);
        }
        art.Hint(Text.Get(K.HintMenu));
    }

    void NewSeason()
    {
        Flow.Push(new TeamSelectScreen(App, Text.Get(K.PickYourClub), Maybe<ClubData>.None, club =>
        {
            void Begin()
            {
                var season = App.Season.StartNew(Flow.Clubs, club.Id);
                Console.WriteLine($"SAP: screens: new season for {club.Id}, {season.Games.Count} games, saved to {App.Season.SavePath}");
                Flow.PlaySeasonGame();
            }

            if (!save.HasValue) Begin();
            else Flow.Push(new ConfirmScreen(App, Text.Get(K.ReplaceSave), Begin));
        }));
    }

    void Standings()
    {
        if (App.Season.Continue(Flow.Clubs).HasValue) Flow.Push(Flow.Overview());
    }
}
