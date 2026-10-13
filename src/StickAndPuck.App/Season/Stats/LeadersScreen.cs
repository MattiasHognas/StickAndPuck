using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// W25's stand-in leaders page, reached from the standings: the season's top ten by points,
/// goals, saves or player-of-the-game awards (left and right switch), for the regular season or
/// the playoffs once they run, and the latest player of the game underneath. W33 draws the final page.
/// </summary>
internal sealed class LeadersScreen : MenuScreen
{
    const int PanelW = 420, Line = 11, Rows = 10;

    static readonly string[] Views = [K.ViewPoints, K.ViewGoals, K.ViewGoalies, K.ViewStars];

    int view;
    bool playoffs = HasPlayoffs;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [new MenuItem(Text.Get(K.Show), value: Text.Get(Views[view]), change: d => view = ((view + d) % Views.Length + Views.Length) % Views.Length)];
        if (HasPlayoffs) items.Add(new MenuItem(Text.Get(K.Games), value: Text.Get(playoffs ? K.Playoffs : K.SeasonWord), change: _ => playoffs = !playoffs));
        items.Add(new MenuItem(Text.Get(K.TableItem), () => ScreenFlow.Replace(new StandingsScreen())));
        items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (!SeasonSession.Current.TryGet(out var season)) return;
        var clubs = ScreenFlow.Clubs;
        season.Stats.CatchUp(season, clubs);
        var table = playoffs && season.Playoffs.HasValue ? season.Stats.Playoffs : season.Stats.Regular;
        var items = Items();
        bool goalies = view == 2;
        var lines = view switch { 0 => table.Leaders(Rows), 1 => table.Scorers(Rows), 2 => table.Goalies(Rows), _ => table.Stars(Rows) };

        int h = 5 + ScreenArt.Row + 2 + Line + (Rows * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        string heading = Text.Format(playoffs && season.Playoffs.HasValue ? K.LeadersHeadingPlayoff : K.LeadersHeadingSeason, Text.Get(Views[view]));
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        (string Head, int X)[] columns = goalies
            ? [(K.ColGp, 250), (K.ColSv, 290), (K.ColStars, 340)]
            : [(K.ColGp, 230), (K.ColG, 262), (K.ColA, 290), (K.ColPts, 318), (K.ColStars, 356)];
        art.Left(Text.Get(K.ColPlayer), x + 30, top, ScreenArt.Muted);
        art.Left(Text.Get(K.ColClub), x + 170, top, ScreenArt.Muted);
        foreach (var (head, cx) in columns) art.Left(Text.Get(head), x + cx, top, ScreenArt.Muted);
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
                art.Left(Text.Get(K.NoGamesYet), x + 30, top, ScreenArt.Muted);
            }
            top += Line;
        }

        top += 4;
        string star = season.Stats.LastAward.TryGet(out var a)
            ? Text.Format(K.StarLine, a.Label, Short(clubs, a.Club), a.Summary, Short(clubs, a.Home), a.HomeGoals, a.AwayGoals, Short(clubs, a.Away))
            : Text.Get(K.StarNobody);
        art.Centred(star, Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 110, PanelW - 220, top);
    }

    static bool HasPlayoffs => SeasonSession.Current.Is(s => s.Playoffs.HasValue);

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
