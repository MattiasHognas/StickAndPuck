using System.Globalization;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Season;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Screens;

/// <summary>
/// W33: the season stats page, opened between games from the results, the table or the bracket.
/// Left and right on SHOW pick points, goals, assists, hits, goalies, player-of-the-game stars or
/// the human's own club; GAMES switches between the regular season and the playoffs once they run.
/// The latest player of the game sits underneath. Reads W25's <see cref="SeasonStats"/>.
/// </summary>
internal sealed class SeasonStatsScreen : MenuScreen
{
    const int PanelW = 440, Line = 11, Rows = 10;

    static readonly string[] Views = ["POINTS", "GOALS", "ASSISTS", "HITS", "GOALIES", "STARS", "YOUR CLUB"];

    int view;
    bool playoffs = SeasonSession.Current?.Playoffs != null;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [new MenuItem("SHOW", Value: Views[view], Change: d => view = ((view + d) % Views.Length + Views.Length) % Views.Length)];
        if (SeasonSession.Current?.Playoffs != null) items.Add(new MenuItem("GAMES", Value: playoffs ? "PLAYOFFS" : "SEASON", Change: _ => playoffs = !playoffs));
        items.Add(new MenuItem(SeasonSession.Current?.Playoffs != null ? "PLAYOFFS" : "TABLE", () => ScreenFlow.Replace(ScreenFlow.Overview())));
        items.Add(new MenuItem("MAIN MENU", Back));
        return items;
    }

    /// <summary>Shows one view, for the headless captures.</summary>
    /// <param name="name">The view's name, as on the SHOW row.</param>
    /// <returns>This screen.</returns>
    public SeasonStatsScreen Showing(string name)
    {
        int i = Array.FindIndex(Views, v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) view = i;
        return this;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        int h = 5 + ScreenArt.Row + 2 + Line + (Rows * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        if (SeasonSession.Current is not { } season)
        {
            var (px, ptop) = art.Panel("SEASON STATS", PanelW, 60, 100);
            art.Centred("NO SEASON PLAYED YET", Game.Width / 2, ptop, ScreenArt.Muted);
            art.Menu(items, Selected, px + 120, PanelW - 240, ptop + 14);
            return;
        }
        var clubs = ScreenFlow.Clubs;
        season.Stats.CatchUp(season, clubs);
        bool post = playoffs && season.Playoffs != null;
        var table = post ? season.Stats.Playoffs : season.Stats.Regular;
        bool goalies = view == 4;
        var lines = Lines(table, season.UserClub);

        string heading = view == 6
            ? $"{Name(clubs, season.UserClub)}  -  {(post ? "PLAYOFFS" : "SEASON")}"
            : $"SHL  {(post ? "PLAYOFF" : "SEASON")} LEADERS  -  {Views[view]}";
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        (string Head, int X)[] columns = goalies
            ? [("GP", 250), ("SV", 290), ("SV/GP", 330), ("STARS", 384)]
            : [("GP", 226), ("G", 256), ("A", 280), ("PTS", 304), ("SH", 336), ("HITS", 362), ("STARS", 398)];
        art.Left("PLAYER", x + 30, top, ScreenArt.Muted);
        art.Left(view == 6 ? "POS" : "CLUB", x + 180, top, ScreenArt.Muted);
        foreach (var (head, cx) in columns) art.Left(head, x + cx, top, Highlight(head) ? art.Gold : ScreenArt.Muted);
        top += Line;

        for (int i = 0; i < Rows; i++)
        {
            if (i < lines.Count) DrawLine(art, lines[i], i, x, top, columns, season.UserClub);
            else if (i == 0) art.Left(post ? "NO PLAYOFF GAMES PLAYED YET" : "NO GAMES PLAYED YET", x + 30, top, ScreenArt.Muted);
            top += Line;
        }

        top += 4;
        string star = season.Stats.LastAward is { } a
            ? $"PLAYER OF THE GAME: {a.Label} ({Short(clubs, a.Club)}) {a.Summary}  IN {Short(clubs, a.Home)} {N(a.HomeGoals)}-{N(a.AwayGoals)} {Short(clubs, a.Away)}"
            : "PLAYER OF THE GAME: NOBODY YET";
        art.Centred(art.Clip(star, PanelW - 16), Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 120, PanelW - 240, top);
        art.Hint("LEFT RIGHT CHANGES THE LIST   ENTER PICKS   ESC BACK");
    }

    /// <summary>One player's line: place, name, club (or position on the club view) and the view's columns.</summary>
    void DrawLine(ScreenArt art, PlayerStats p, int i, int x, int top, (string Head, int X)[] columns, string user)
    {
        var ink = view != 6 && string.Equals(p.Club, user, StringComparison.Ordinal) ? art.Gold : art.Text;
        art.Right(N(i + 1), x + 22, top, ink);
        art.Left(art.Clip(p.Label, 146), x + 30, top, ink);
        art.Left(view == 6 ? p.Position.ToString() : Short(ScreenFlow.Clubs, p.Club), x + 180, top, ink);
        string perGame = p.Games > 0 ? ((double)p.Saves / p.Games).ToString("0.0", CultureInfo.InvariantCulture) : "-";
        string[] cells = view == 4
            ? [N(p.Games), N(p.Saves), perGame, N(p.Stars)]
            : [N(p.Games), N(p.Goals), N(p.Assists), N(p.Points), N(p.Shots), N(p.Hits), N(p.Stars)];
        for (int c = 0; c < cells.Length; c++) art.Left(cells[c], x + columns[c].X, top, ink);
    }

    /// <summary>The players the view lists, best first.</summary>
    IReadOnlyList<PlayerStats> Lines(StatTable table, string user)
    {
        IEnumerable<PlayerStats> skaters = table.Players.Where(p => !p.IsGoalie);
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
        (0, "PTS") or (1, "G") or (2, "A") or (3, "HITS") or (4, "SV") or (5, "STARS") => true,
        _ => false,
    };

    static string Name(IReadOnlyList<ClubData> clubs, string id) =>
        clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// W33: a game settled with "sim this game" (W26's <see cref="SeasonSession.SimCurrentGame"/>):
/// the stage, both crests and the score with how it was decided, then the next step. When the sim
/// decided the final, CHAMPIONS opens the trophy screen.
/// </summary>
/// <param name="game">The game simmed.</param>
/// <param name="crowned">True when this game crowned the champion.</param>
internal sealed class SimResultScreen(SimmedGame game, bool crowned) : MenuScreen
{
    const int PanelW = 300;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [];
        if (crowned) items.Add(new MenuItem("CHAMPIONS", () => ScreenFlow.Replace(TrophyScreen.Season())));
        items.Add(new MenuItem("CONTINUE", Back));
        if (ScreenFlow.CanSimGame()) items.Add(new MenuItem("SIM NEXT GAME", ScreenFlow.SimSeasonGame));
        if (ScreenFlow.SeasonHasNextGame()) items.Add(new MenuItem("PLAY NEXT GAME", ScreenFlow.PlaySeasonGame));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        var clubs = ScreenFlow.Clubs;
        int h = 5 + ScreenArt.Row + 2 + 24 + 14 + 6 + (items.Count * ScreenArt.Row) + 6;
        string kind = game.Result.Kind switch { ResultKind.Overtime => " - OT", ResultKind.Shootout => " - SHOOTOUT", _ => "" };
        var (x, top) = art.Panel($"SIMMED  {game.Stage}{kind}", PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        int cx = Game.Width / 2;
        bool homeWon = game.Result.HomeGoals > game.Result.AwayGoals;
        art.Hud.DrawCrest(game.Home, cx - 30 - 16, top);
        art.Hud.DrawCrest(game.Away, cx + 30, top);
        art.Centred($"{N(game.Result.HomeGoals)} - {N(game.Result.AwayGoals)}", cx, top + 3, art.Gold);
        art.Right(art.Clip(Name(clubs, game.Home), cx - 52 - x - 8), cx - 30 - 22, top + 3, homeWon ? art.Gold : art.Text);
        art.Left(art.Clip(Name(clubs, game.Away), x + PanelW - 8 - (cx + 52)), cx + 30 + 22, top + 3, homeWon ? art.Text : art.Gold);
        top += 24;
        art.Centred("SETTLED ON PAPER", cx, top, ScreenArt.Muted);
        top += 14 + 6;
        art.Menu(items, Selected, x + 60, PanelW - 120, top);
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id) =>
        clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// W33: the trophy screen. For the SHL champion (after the final, played or simmed, and from the
/// bracket) the cup with the champion's crest, the final's score and the season's award winners;
/// for an arcade champion the cup, the run and its place on the best runs board.
/// </summary>
internal sealed class TrophyScreen : MenuScreen
{
    const int PanelW = 400, Line = 12;

    static readonly Color Light = new(0xf9, 0xda, 0x93, 0xff), Deep = new(0xc8, 0x96, 0x3c, 0xff), Wood = new(0x42, 0x32, 0x36, 0xff);

    readonly string heading, club, title;
    readonly IReadOnlyList<(string Label, string Value)> lines;
    readonly IReadOnlyList<MenuItem> items;

    TrophyScreen(string heading, string club, string title, IReadOnlyList<(string, string)> lines, IReadOnlyList<MenuItem> items)
    {
        this.heading = heading;
        this.club = club;
        this.title = title;
        this.lines = lines;
        this.items = items;
    }

    /// <summary>The SHL champion of the season being played.</summary>
    /// <returns>The screen.</returns>
    public static TrophyScreen Season()
    {
        var clubs = ScreenFlow.Clubs;
        var season = SeasonSession.Current;
        var playoffs = season?.Playoffs;
        string champion = playoffs?.Champion ?? "";
        List<(string, string)> lines = [];
        if (playoffs is { Champion: not null })
        {
            var final = playoffs.Current.Series[0];
            string other = string.Equals(final.High, champion, StringComparison.Ordinal) ? final.Low : final.High;
            int won = Math.Max(final.HighWins, final.LowWins), lost = Math.Min(final.HighWins, final.LowWins);
            lines.Add(("FINAL", $"WON {N(won)}-{N(lost)} V {Name(clubs, other)}"));
        }
        if (season != null)
        {
            season.Stats.CatchUp(season, clubs);    // games settled on paper are credited from their score
            var stats = season.Stats;
            if (stats.Regular.Leaders(1).FirstOrDefault() is { } top) lines.Add(("TOP SCORER", $"{top.Label} {Short(clubs, top.Club)} {N(top.Points)} PTS"));
            if (stats.Regular.Scorers(1).FirstOrDefault() is { } sniper) lines.Add(("MOST GOALS", $"{sniper.Label} {Short(clubs, sniper.Club)} {N(sniper.Goals)}"));
            if (stats.Regular.Goalies(1).FirstOrDefault() is { } goalie) lines.Add(("BEST GOALIE", $"{goalie.Label} {Short(clubs, goalie.Club)} {N(goalie.Saves)} SV"));
            if (stats.Playoffs.Leaders(1).FirstOrDefault() is { } hero) lines.Add(("PLAYOFF STAR", $"{hero.Label} {Short(clubs, hero.Club)} {N(hero.Points)} PTS"));
            string user = Name(clubs, season.UserClub);
            lines.Add(("YOUR CLUB", string.Equals(season.UserClub, champion, StringComparison.Ordinal) ? "CHAMPIONS!" : playoffs is { UserQualified: false } ? $"{user} MISSED THE PLAYOFFS" : $"{user} WENT OUT"));
        }
        return new TrophyScreen("SHL CHAMPIONS", champion, Name(clubs, champion), lines,
        [
            new MenuItem("CONTINUE", ScreenFlow.Pop),
            new MenuItem("SEASON STATS", () => ScreenFlow.Replace(new SeasonStatsScreen())),
        ]);
    }

    /// <summary>An arcade run won by beating every club.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The screen.</returns>
    public static TrophyScreen Arcade(ArcadeRun run)
    {
        var clubs = ScreenFlow.Clubs;
        List<(string, string)> lines =
        [
            ("RULE", run.Rule == ArcadeRule.SuddenDeath ? "SUDDEN DEATH" : "THREE LIVES"),
            ("RECORD", $"{N(run.Stage)} BEATEN, {N(run.Losses)} {(run.Losses == 1 ? "LOSS" : "LOSSES")}"),
            ("GOALS", $"{N(run.Games.Sum(g => g.GoalsFor))}-{N(run.Games.Sum(g => g.GoalsAgainst))}"),
        ];
        if (run.Stats.Leaders(1, run.UserClub).FirstOrDefault() is { } top) lines.Add(("TOP SCORER", $"{top.Label} {N(top.Points)} PTS"));
        if (run.Place > 0) lines.Add(("BEST RUNS", $"PLACE {N(run.Place)} OF {N(run.Records.Count)}"));
        return new TrophyScreen("ARCADE CHAMPIONS", run.UserClub, Name(clubs, run.UserClub), lines,
        [
            new MenuItem("BEST RUNS", () => ScreenFlow.Replace(new ArcadeBoardScreen())),
            new MenuItem("CONTINUE", ScreenFlow.Pop),
        ]);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items() => items;

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        int body = Math.Max(64, 18 + (lines.Count * Line));
        int h = 5 + ScreenArt.Row + 2 + body + 8 + (items.Count * ScreenArt.Row) + 6;
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        DrawCup(art, x + 22, top + 4);
        int tx = x + 92;
        art.Left(art.Clip(title, PanelW - 100), tx, top, art.Gold);
        int y = top + 18;
        foreach (var (label, value) in lines)
        {
            art.Left(label, tx, y, ScreenArt.Muted);
            art.Left(art.Clip(value, x + PanelW - 10 - (tx + 76)), tx + 76, y, art.Text);
            y += Line;
        }
        art.Menu(items, Selected, x + 100, PanelW - 200, top + body + 8);
    }

    /// <summary>The cup, about 52x58, drawn in the palette's golds with the club's crest on the bowl.</summary>
    void DrawCup(ScreenArt art, int x, int y)
    {
        var gold = art.Gold;
        // handles
        DrawRectangle(x, y + 6, 6, 3, Deep);
        DrawRectangle(x, y + 6, 3, 14, Deep);
        DrawRectangle(x, y + 18, 9, 3, Deep);
        DrawRectangle(x + 43, y + 6, 6, 3, Deep);
        DrawRectangle(x + 46, y + 6, 3, 14, Deep);
        DrawRectangle(x + 40, y + 18, 9, 3, Deep);
        // rim, bowl tapering to the stem, the base and the plinth
        DrawRectangle(x + 4, y, 41, 4, Light);
        DrawRectangle(x + 6, y + 4, 37, 16, gold);
        DrawRectangle(x + 9, y + 20, 31, 4, gold);
        DrawRectangle(x + 13, y + 24, 23, 3, Deep);
        DrawRectangle(x + 21, y + 27, 7, 10, gold);
        DrawRectangle(x + 15, y + 37, 19, 4, Deep);
        DrawRectangle(x + 10, y + 41, 29, 11, Wood);
        DrawRectangle(x + 13, y + 44, 23, 3, Deep);
        // shine and shade
        DrawRectangle(x + 10, y + 6, 3, 12, Light);
        DrawRectangle(x + 37, y + 5, 4, 15, Deep);
        DrawRectangle(x + 23, y + 28, 2, 8, Light);
        if (club.Length > 0) art.Hud.DrawCrest(club, x + 16, y + 4);
        art.Hud.DrawIcon("star", x - 6, y - 4);
        art.Hud.DrawIcon("star", x + 48, y + 28);
        art.Hud.DrawIcon("star", x + 44, y - 6);
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id) =>
        clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// W33: the arcade best runs board (W25's <see cref="ArcadeRun.Records"/>, kept across runs in
/// arcade.json): place, club, rule, how far the run got, losses, goals and its top scorer, the run
/// just finished in gold. Opened after a run ends and from the new game list.
/// </summary>
internal sealed class ArcadeBoardScreen : MenuScreen
{
    const int PanelW = 460, Line = 12;

    readonly (IReadOnlyList<ArcadeRecord> Records, int Place) board = Board();

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items() => [new MenuItem("BACK", Back)];

    /// <summary>Gets a value indicating whether there is a board to show (any run ever finished). Reads the save.</summary>
    /// <returns>True when a run is on the board.</returns>
    public static bool Any() => Board().Records.Count > 0;

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var (records, place) = board;
        var clubs = ScreenFlow.Clubs;
        int rows = Math.Max(1, records.Count);
        int h = 5 + ScreenArt.Row + 2 + Line + (rows * Line) + 8 + ScreenArt.Row + 6;
        var (x, top) = art.Panel("ARCADE  BEST RUNS", PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        (string Head, int X)[] columns = [("CLUB", 24), ("RULE", 146), ("RUN", 198), ("L", 262), ("GOALS", 278), ("TOP SCORER", 322)];
        foreach (var (head, cx) in columns) art.Left(head, x + cx, top, ScreenArt.Muted);
        top += Line;
        if (records.Count == 0) art.Left("NO RUNS FINISHED YET", x + 24, top, ScreenArt.Muted);
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var ink = i + 1 == place ? art.Gold : art.Text;
            string name = clubs.FirstOrDefault(c => string.Equals(c.Id, r.Club, StringComparison.Ordinal))?.Name ?? r.Club;
            art.Right(N(i + 1), x + 18, top, ink);
            if (r.Champion) art.Hud.DrawIcon("star", x + 251, top + 2);
            art.Left(art.Clip(name, 118), x + 24, top, ink);
            art.Left(r.Rule == ArcadeRule.SuddenDeath ? "SUDDEN" : "3 LIVES", x + 146, top, ink);
            art.Left(r.Champion ? "CHAMPIONS" : $"{N(r.Beaten)} BEATEN", x + 198, top, ink);
            art.Left(N(r.Losses), x + 262, top, ink);
            art.Left($"{N(r.GoalsFor)}-{N(r.GoalsAgainst)}", x + 278, top, ink);
            string scorer = r.TopScorer.Length > 0 ? $"{r.TopScorer} {N(r.TopPoints)}" : "-";
            art.Left(art.Clip(scorer, PanelW - 330), x + 322, top, ink);
            top += Line;
        }
        if (records.Count == 0) top += Line;
        art.Menu(Items(), Selected, x + 170, PanelW - 340, top + 8);
    }

    /// <summary>The board: the open run's, else the save's; the place is the run just finished, or 0.</summary>
    static (IReadOnlyList<ArcadeRecord> Records, int Place) Board()
    {
        var run = ArcadeSession.Current ?? ArcadeSave.Read(ArcadeSession.SavePath.Length > 0 ? ArcadeSession.SavePath : null);
        if (run == null) return ([], 0);
        return (run.Records, run.IsOver ? run.Place : 0);
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
