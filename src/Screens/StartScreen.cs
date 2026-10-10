using StickAndPuck.Core;
using StickAndPuck.Season;

namespace StickAndPuck.Screens;

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
        return
        [
            new MenuItem("CONTINUE SEASON", ScreenFlow.PlaySeasonGame, Enabled: saved),
            new MenuItem("NEW SEASON", NewSeason),
            new MenuItem("NEW GAME", () => ScreenFlow.Push(new ModeScreen())),
            new MenuItem("STANDINGS", Standings, Enabled: saved),
            new MenuItem("SETTINGS", () => ScreenFlow.Push(new SettingsScreen())),
            new MenuItem("QUIT", ScreenFlow.Quit),
        ];
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

        if (save != null)
        {
            string club = ScreenFlow.Clubs.FirstOrDefault(c => string.Equals(c.Id, save.UserClub, StringComparison.Ordinal))?.Name ?? save.UserClub;
            int played = save.Games.Count(g => g.Played && g.Involves(save.UserClub));
            int total = save.Games.Count(g => g.Involves(save.UserClub));
            string line = !save.IsOver ? $"{club} - GAME {played + 1} OF {total}"
                : save.Playoffs is { Champion: not null } || save.Playoffs is { UserSeries: null } ? $"{club} - SEASON OVER"
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
