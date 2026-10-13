using StickAndPuck.App.Core;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Screens;

/// <summary>
/// The start screen: continue the saved season, start a new season, a new game in another mode,
/// the standings, settings and quit. Continue and standings show only when there is a save.
/// </summary>
internal sealed class StartScreen : MenuScreen
{
    const int PanelW = 200;

    SeasonState? save;
    bool read;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        if (!read)
        {
            save = SeasonSave.Read();
            read = true;
        }
        bool saved = save != null;
        List<MenuItem> items = [new MenuItem("CONTINUE SEASON", ScreenFlow.PlaySeasonGame, Enabled: saved)];
        if (ScreenFlow.Settings.SimGame) items.Add(new MenuItem("SIM NEXT GAME", ScreenFlow.SimSeasonGame, Enabled: Season is { } s && HasNextGame(s)));    // W33, off with the SimGame setting
        items.Add(new MenuItem("NEW SEASON", NewSeason));
        items.Add(new MenuItem("NEW GAME", () => ScreenFlow.Push(new ModeScreen())));
        items.Add(new MenuItem("STANDINGS", Standings, Enabled: saved));
        items.Add(new MenuItem("SETTINGS", () => ScreenFlow.Push(new SettingsScreen())));
        items.Add(new MenuItem("QUIT", ScreenFlow.Quit));
        return items;
    }

    /// <summary>Gets the season to describe: the open one (a game simmed from here changes it), else the save read on opening.</summary>
    SeasonState? Season => SeasonSession.Current ?? save;

    /// <summary>Whether the human's club has a season or playoff game left, as <see cref="ScreenFlow.SeasonHasNextGame"/> asks of the open season.</summary>
    static bool HasNextGame(SeasonState season)
    {
        if (!season.IsOver) return season.NextGame != null;
        return season.Playoffs == null || season.Playoffs.UserSeries != null;
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

        if (Season is { } season)
        {
            string club = ScreenFlow.Clubs.FirstOrDefault(c => string.Equals(c.Id, season.UserClub, StringComparison.Ordinal))?.Name ?? season.UserClub;
            int played = season.Games.Count(g => g.Played && g.Involves(season.UserClub));
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            string line = !season.IsOver ? $"{club} - GAME {played + 1} OF {total}"
                : season.Playoffs is { Champion: not null } || season.Playoffs is { UserSeries: null } ? $"{club} - SEASON OVER"
                : $"{club} - PLAYOFFS";
            art.Centred(line, Game.Width / 2, 80 + h + 6, art.Text);
        }
        art.Hint("ARROWS MOVE   ENTER PICKS   ESC BACK");
    }

    void NewSeason()
    {
        ScreenFlow.Push(new TeamSelectScreen("PICK YOUR CLUB", null, club =>
        {
            void Begin()
            {
                var season = SeasonSession.StartNew(ScreenFlow.Clubs, club.Id);
                Console.WriteLine($"SAP: screens: new season for {club.Id}, {season.Games.Count} games, saved to {SeasonSession.SavePath}");
                ScreenFlow.PlaySeasonGame();
            }

            if (save == null) Begin();
            else ScreenFlow.Push(new ConfirmScreen("REPLACE THE SAVED SEASON?", Begin));
        }));
    }

    static void Standings()
    {
        if (SeasonSession.Continue(ScreenFlow.Clubs) != null) ScreenFlow.Push(ScreenFlow.Overview());
    }
}
