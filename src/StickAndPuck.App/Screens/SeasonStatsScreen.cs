using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using System.Collections.Immutable;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>The season stats page, opened between games from the results, the table or the bracket.
/// Left and right on SHOW pick points, goals, assists, hits, goalies, player-of-the-game stars or
/// the human's own club; GAMES switches between the regular season and the playoffs once they run.
/// The latest player of the game sits underneath. Reads <see cref="SeasonStats"/>.</summary>
internal sealed class SeasonStatsScreen(AppState app) : MenuScreen(app)
{
    const int PanelW = 440, Line = 11, Rows = 10;

    static readonly ImmutableArray<string> Views = [K.ViewPoints, K.ViewGoals, K.ViewAssists, K.ViewHits, K.ViewGoalies, K.ViewStars, K.ViewYourClub];

    int view;
    bool playoffs = app.Season.Current.Is(s => s.Playoffs.HasValue);

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [new MenuItem(Text.Get(K.Show), value: Text.Get(Views[view]), change: d => view = ((view + d) % Views.Length + Views.Length) % Views.Length)];
        if (App.Season.Current.Is(s => s.Playoffs.HasValue)) items.Add(new MenuItem(Text.Get(K.Games), value: Text.Get(playoffs ? K.Playoffs : K.SeasonWord), change: _ => playoffs = !playoffs));
        items.Add(new MenuItem(Text.Get(App.Season.Current.Is(s => s.Playoffs.HasValue) ? K.Playoffs : K.TableItem), () => Flow.Replace(Flow.Overview())));
        items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
        return items;
    }

    /// <summary>Shows one view, for the headless captures.</summary>
    public SeasonStatsScreen Showing(string name)
    {
        int i = Enumerable.Range(0, Views.Length).FirstOrDefault(j => string.Equals(Views[j], name, StringComparison.OrdinalIgnoreCase) || string.Equals(Text.Get(Views[j]), name, StringComparison.OrdinalIgnoreCase), -1);
        if (i >= 0) view = i;
        return this;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        int h = 5 + ScreenArt.Row + 2 + Line + (Rows * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        if (!App.Season.Current.TryGet(out var season))
        {
            var (px, ptop) = art.Panel(Text.Get(K.SeasonStats), PanelW, 60, 100);
            art.Centred(Text.Get(K.NoSeasonYet), Game.Width / 2, ptop, ScreenArt.Muted);
            art.Menu(items, Selected, px + 120, PanelW - 240, ptop + 14);
            return;
        }
        var clubs = Flow.Clubs;
        season.Stats.CatchUp(season, clubs);
        bool post = playoffs && season.Playoffs.HasValue;
        var table = post ? season.Stats.Playoffs : season.Stats.Regular;
        bool goalies = view == 4;
        var lines = Lines(table, season.UserClub);

        string seasonScope = Text.Get(post ? K.Playoffs : K.SeasonWord);
        string title;
        if (view == 6)
        {
            title = Text.Format(K.ClubViewHeading, Name(clubs, season.UserClub), seasonScope);
        }
        else
        {
            string headingKey = post ? K.LeadersHeadingPlayoff : K.LeadersHeadingSeason;
            title = Text.Format(headingKey, Text.Get(Views[view]));
        }
        var (x, top) = art.Panel(title, PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        (string Head, int X)[] columns = goalies
            ? [(K.ColGp, 250), (K.ColSv, 290), (K.ColSvGp, 330), (K.ColStars, 384)]
            : [(K.ColGp, 204), (K.ColG, 228), (K.ColA, 250), (K.ColPts, 272), (K.ColSh, 300), (K.ColHits, 328), (K.ColClr, 360), (K.ColStars, 394)];
        art.Left(Text.Get(K.ColPlayer), x + 30, top, ScreenArt.Muted);
        art.Left(Text.Get(view == 6 ? K.ColPos : K.ColClub), x + 170, top, ScreenArt.Muted);
        foreach (var (head, cx) in columns) art.Left(Text.Get(head), x + cx, top, Highlight(head) ? art.Gold : ScreenArt.Muted);
        top += Line;

        for (int i = 0; i < Rows; i++)
        {
            if (i < lines.Count) DrawLine(art, lines[i], i, x, top, columns, season.UserClub);
            else if (i == 0) art.Left(Text.Get(post ? K.NoPlayoffGamesYet : K.NoGamesYet), x + 30, top, ScreenArt.Muted);
            top += Line;
        }

        top += 4;
        string star = season.Stats.LastAward.TryGet(out var a)
            ? Text.Format(K.StarLine, a.Label, Short(clubs, a.Club), a.Summary, Short(clubs, a.Home), a.HomeGoals, a.AwayGoals, Short(clubs, a.Away))
            : Text.Get(K.StarNobody);
        art.Centred(art.Clip(star, PanelW - 16), Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 120, PanelW - 240, top);
        art.Hint(Text.Get(K.HintStats));
    }

    /// <summary>One player's line: place, name, club (or position on the club view) and the view's columns.</summary>
    void DrawLine(ScreenArt art, PlayerStats p, int i, int x, int top, (string Head, int X)[] columns, string user)
    {
        var ink = view != 6 && string.Equals(p.Club, user, StringComparison.Ordinal) ? art.Gold : art.Text;
        art.Right(N(i + 1), x + 22, top, ink);
        art.Left(art.Clip(p.Label, 136), x + 30, top, ink);
        art.Left(view == 6 ? p.Position.ToString() : Short(Flow.Clubs, p.Club), x + 170, top, ink);
        string perGame = p.Games > 0 ? ((double)p.Saves / p.Games).ToString("0.0", CultureInfo.InvariantCulture) : "-";
        string[] cells = view == 4
            ? [N(p.Games), N(p.Saves), perGame, N(p.Stars)]
            : [N(p.Games), N(p.Goals), N(p.Assists), N(p.Points), N(p.Shots), N(p.Hits), N(p.Clears), N(p.Stars)];
        for (int c = 0; c < cells.Length; c++) art.Left(cells[c], x + columns[c].X, top, ink);
    }

    /// <summary>The players the view lists, best first.</summary>
    IReadOnlyList<PlayerStats> Lines(StatTable table, string user)
    {
        var skaters = table.Players.Where(p => !p.IsGoalie);
        return view switch
        {
            0 => table.Leaders(Rows),
            1 => table.Scorers(Rows),
            2 => skaters.Where(p => p.Assists > 0).OrderByDescending(p => p.Assists).ThenByDescending(p => p.Points).ThenBy(p => p.Name, StringComparer.Ordinal).Take(Rows).ToList(),
            3 => skaters.Where(p => p.Hits > 0).OrderByDescending(p => p.Hits).ThenBy(p => p.Games).ThenBy(p => p.Name, StringComparer.Ordinal).Take(Rows).ToList(),
            4 => table.Goalies(Rows),
            5 => table.Stars(Rows),
            _ => table.Players.Where(p => string.Equals(p.Club, user, StringComparison.Ordinal) && p.Games > 0)
                .OrderBy(p => p.IsGoalie).ThenByDescending(p => p.Points).ThenByDescending(p => p.Goals).ThenBy(p => p.Number).Take(Rows).ToList(),
        };
    }

    bool Highlight(string head) => (view, head) switch
    {
        (0, K.ColPts) or (1, K.ColG) or (2, K.ColA) or (3, K.ColHits) or (4, K.ColSv) or (5, K.ColStars) => true,
        _ => false,
    };

    static string Name(IReadOnlyList<ClubData> clubs, string id) =>
        clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).Select(c => c.Name).Or(id);

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
