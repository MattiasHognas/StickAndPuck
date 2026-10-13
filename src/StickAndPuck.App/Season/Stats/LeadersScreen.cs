using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Season;

/// <summary>
/// W25's stand-in leaders page, reached from the standings: the season's top ten by points,
/// goals, saves or player-of-the-game awards (left and right switch), for the regular season or
/// the playoffs once they run, and the latest player of the game underneath. W33 draws the final page.
/// </summary>
internal sealed class LeadersScreen : MenuScreen
{
    const int PanelW = 420, Line = 11, Rows = 10;

    static readonly string[] Views = ["POINTS", "GOALS", "GOALIES", "STARS"];

    int view;
    bool playoffs = SeasonSession.Current?.Playoffs != null;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [new MenuItem("SHOW", Value: Views[view], Change: d => view = ((view + d) % Views.Length + Views.Length) % Views.Length)];
        if (SeasonSession.Current?.Playoffs != null) items.Add(new MenuItem("GAMES", Value: playoffs ? "PLAYOFFS" : "SEASON", Change: _ => playoffs = !playoffs));
        items.Add(new MenuItem("TABLE", () => ScreenFlow.Replace(new StandingsScreen())));
        items.Add(new MenuItem("MAIN MENU", Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (SeasonSession.Current is not { } season) return;
        var clubs = ScreenFlow.Clubs;
        season.Stats.CatchUp(season, clubs);
        var table = playoffs && season.Playoffs != null ? season.Stats.Playoffs : season.Stats.Regular;
        var items = Items();
        bool goalies = view == 2;
        var lines = view switch { 0 => table.Leaders(Rows), 1 => table.Scorers(Rows), 2 => table.Goalies(Rows), _ => table.Stars(Rows) };

        int h = 5 + ScreenArt.Row + 2 + Line + (Rows * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        string heading = $"SHL  {(playoffs && season.Playoffs != null ? "PLAYOFF" : "SEASON")} LEADERS  -  {Views[view]}";
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        (string Head, int X)[] columns = goalies
            ? [("GP", 250), ("SV", 290), ("STARS", 340)]
            : [("GP", 230), ("G", 262), ("A", 290), ("PTS", 318), ("STARS", 356)];
        art.Left("PLAYER", x + 30, top, ScreenArt.Muted);
        art.Left("CLUB", x + 170, top, ScreenArt.Muted);
        foreach (var (head, cx) in columns) art.Left(head, x + cx, top, ScreenArt.Muted);
        top += Line;

        for (int i = 0; i < Rows; i++)
        {
            if (i < lines.Count)
            {
                var p = lines[i];
                var ink = string.Equals(p.Club, season.UserClub, StringComparison.Ordinal) ? art.Gold : art.Text;
                art.Right(N(i + 1), x + 22, top, ink);
                art.Left(p.Label, x + 30, top, ink);
                art.Left(Short(clubs, p.Club), x + 170, top, ink);
                string[] cells = goalies ? [N(p.Games), N(p.Saves), N(p.Stars)] : [N(p.Games), N(p.Goals), N(p.Assists), N(p.Points), N(p.Stars)];
                for (int c = 0; c < cells.Length; c++) art.Left(cells[c], x + columns[c].X, top, ink);
            }
            else if (i == 0)
            {
                art.Left("NO GAMES PLAYED YET", x + 30, top, ScreenArt.Muted);
            }
            top += Line;
        }

        top += 4;
        string star = season.Stats.LastAward is { } a
            ? $"PLAYER OF THE GAME: {a.Label} ({Short(clubs, a.Club)}) {a.Summary}  IN {Short(clubs, a.Home)} {N(a.HomeGoals)}-{N(a.AwayGoals)} {Short(clubs, a.Away)}"
            : "PLAYER OF THE GAME: NOBODY YET";
        art.Centred(star, Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 110, PanelW - 220, top);
    }

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
