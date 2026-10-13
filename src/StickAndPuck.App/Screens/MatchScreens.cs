using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Screens;

/// <summary>Pause, over a match: resume, settings, or quit to the start screen (a season game left early is not recorded).</summary>
internal sealed class PauseScreen : MenuScreen
{
    const int PanelW = 180;

    /// <summary>The panel's top, below the HUD's banners.</summary>
    const int Top = 128;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        return
        [
            new MenuItem("RESUME", Resume),
            new MenuItem("SETTINGS", () => ScreenFlow.Push(new SettingsScreen())),
            new MenuItem("QUIT TO MENU", () => ScreenFlow.Push(new ConfirmScreen("LEAVE THIS MATCH?", () => ScreenFlow.ToMenus()))),
        ];
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        if (press.Contains(MenuButton.Pause) && !press.Contains(MenuButton.Back)) Resume();    // P or start resumes; escape is back, which resumes too
        else base.Update(world, press);
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        Resume();
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        var (x, top) = art.Panel("PAUSED", PanelW, (items.Count * ScreenArt.Row) + 30, Top);
        art.Menu(items, Selected, x, PanelW, top);
    }

    static void Resume()
    {
        ScreenFlow.Clear();
    }
}

/// <summary>
/// Results: the final score with crests, how it was decided, shots and the goals, each club's in its
/// own column (period, clock, scorer cut to fit), and the player of the game. Season games go on to
/// the standings, the season stats or the trophy once the champion is crowned; an arcade run that is
/// over goes on to its trophy or the best runs board.
/// </summary>
/// <param name="result">The result.</param>
internal sealed class ResultsScreen(MatchResult result) : MenuScreen
{
    const int PanelW = 400, Line = 11, ClockX = 20, ScorerX = 50;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var plan = result.Plan;
        if (plan.Season)
        {
            List<MenuItem> items = [];
            if (PlayoffSession.Current?.Champion != null) items.Add(new MenuItem("CHAMPIONS", () => ScreenFlow.Replace(TrophyScreen.Season())));
            items.Add(new MenuItem("STANDINGS", () => ScreenFlow.Replace(ScreenFlow.Overview())));
            if (ScreenFlow.CanSimGame()) items.Add(new MenuItem("SIM NEXT GAME", ScreenFlow.SimSeasonGame));
            items.Add(new MenuItem("SEASON STATS", () => ScreenFlow.Replace(new SeasonStatsScreen())));
            items.Add(new MenuItem("MAIN MENU", Back));
            return items;
        }
        if (plan.Mode is ArcadeMode && ArcadeSession.Current is { IsOver: true } run)
        {
            List<MenuItem> items = [];
            if (run.Champion) items.Add(new MenuItem("TROPHY", () => ScreenFlow.Replace(TrophyScreen.Arcade(run))));
            items.Add(new MenuItem("BEST RUNS", () => ScreenFlow.Replace(new ArcadeBoardScreen())));
            items.Add(new MenuItem("MAIN MENU", Back));
            return items;
        }
        if (plan.Mode is { HasNextGame: true } mode)
            return [new MenuItem("NEXT GAME", () => ScreenFlow.PlayMode(mode, plan.Picks)), new MenuItem("MAIN MENU", Back)];
        if (plan.Mode is ExhibitionMode exhibition)
            return [new MenuItem("PLAY AGAIN", () => ScreenFlow.PlayMode(exhibition, plan.Picks)), new MenuItem("MAIN MENU", Back)];
        return [new MenuItem("MAIN MENU", Back)];
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        ScreenFlow.Pop();
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        string? status = result.Plan.Mode?.Status;
        var star = MatchStats.Last is { PlayerOfGame: { } p } last && string.Equals(last.Home, result.Setup.Home.Id, StringComparison.Ordinal) && string.Equals(last.Away, result.Setup.Away.Id, StringComparison.Ordinal) ? p : null;
        var home = result.Goals.Where(g => g.Side == Side.Home).ToList();
        var away = result.Goals.Where(g => g.Side == Side.Away).ToList();
        int extra = 5 + ScreenArt.Row + 2 + 24 + 14 + 6 + 6 + (star != null ? 14 : 0) + (status != null ? 14 : 0) + (items.Count * ScreenArt.Row) + 6;
        int fits = Math.Max(2, (Game.Height - 8 - extra) / Line);
        int rows = Math.Max(1, Math.Min(Math.Max(home.Count, away.Count), fits));
        int h = extra + (rows * Line);
        string heading = result.Decided.Length > 0 ? $"FINAL - {result.Decided}" : "FINAL";
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        int cx = Game.Width / 2;

        // crests, names and the score
        var homeClub = result.Setup.Home;
        var awayClub = result.Setup.Away;
        art.Hud.DrawCrest(homeClub.Id, cx - 40 - 16, top);
        art.Hud.DrawCrest(awayClub.Id, cx + 40, top);
        art.Centred($"{N(result.HomeGoals)} - {N(result.AwayGoals)}", cx, top + 3, art.Gold);
        art.Right(art.Clip(homeClub.Name, cx - 62 - x - 8), cx - 40 - 22, top + 3, Ink(art, Side.Home));
        art.Left(art.Clip(awayClub.Name, x + PanelW - 8 - (cx + 62)), cx + 40 + 22, top + 3, Ink(art, Side.Away));
        top += 24;
        art.Centred($"SHOTS  {N(result.HomeShots)} - {N(result.AwayShots)}", cx, top, ScreenArt.Muted);
        top += 14 + 6;

        if (result.Goals.Count == 0) art.Centred("NO GOALS", cx, top, ScreenArt.Muted);
        int colW = cx - 8 - (x + 12);
        Column(art, home, x + 12, colW, top, rows);
        Column(art, away, cx + 8, colW, top, rows);
        top += (rows * Line) + 6;
        if (star != null)
        {
            art.Centred($"PLAYER OF THE GAME  {star.Label}  {(star.IsGoalie ? $"{N(star.Saves)} SAVES" : $"{N(star.Goals)}G {N(star.Assists)}A")}", cx, top, art.Gold);
            top += 14;
        }
        if (status != null)
        {
            art.Centred(status, cx, top, art.Gold);
            top += 14;
        }
        art.Menu(items, Selected, x + 80, PanelW - 160, top);
    }

    /// <summary>One club's goals, oldest first: period, clock and the scorer cut to the column; "+N MORE" when they do not fit.</summary>
    static void Column(ScreenArt art, List<GoalLine> goals, int x, int w, int top, int rows)
    {
        int shown = goals.Count > rows ? rows - 1 : goals.Count;
        for (int i = 0; i < shown; i++)
        {
            var g = goals[i];
            var ink = g.Period is "1" or "2" or "3" ? art.Text : art.Gold;    // overtime and shootout goals stand out
            art.Left(g.Period, x, top, ink);
            art.Left(g.Clock, x + ClockX, top, ink);
            art.Left(art.Clip(g.Scorer, w - ScorerX), x + ScorerX, top, ink);
            top += Line;
        }
        if (goals.Count > shown) art.Left($"+{N(goals.Count - shown)} MORE", x + ScorerX, top, ScreenArt.Muted);
    }

    Color Ink(ScreenArt art, Side side)
    {
        int mine = side == Side.Home ? result.HomeGoals : result.AwayGoals, theirs = side == Side.Home ? result.AwayGoals : result.HomeGoals;
        return mine > theirs ? art.Gold : art.Text;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Standings: the SHL table of the season being played (W16a's <see cref="SeasonState.Table"/>),
/// the human's club in gold, and the next game. Enter plays it.
/// </summary>
internal sealed class StandingsScreen : MenuScreen
{
    const int PanelW = 380, Line = 11;

    static readonly (string Head, int X)[] Columns =
        [("GP", 170), ("W", 196), ("OTW", 216), ("OTL", 246), ("L", 276), ("GOALS", 296), ("PTS", 348)];

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var season = SeasonSession.Current;
        string play = season is { IsOver: true, Playoffs: null } ? "START PLAYOFFS" : "PLAY NEXT GAME";
        List<MenuItem> items = [new MenuItem(play, ScreenFlow.PlaySeasonGame, Enabled: ScreenFlow.SeasonHasNextGame())];
        if (ScreenFlow.Settings.SimGame) items.Add(new MenuItem("SIM THIS GAME", ScreenFlow.SimSeasonGame, Enabled: ScreenFlow.CanSimGame()));
        if (season?.Playoffs != null) items.Add(new MenuItem("PLAYOFFS", () => ScreenFlow.Replace(new BracketScreen())));
        items.Add(new MenuItem("SEASON STATS", () => ScreenFlow.Replace(new SeasonStatsScreen())));
        items.Add(new MenuItem("MAIN MENU", Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (SeasonSession.Current is not { } season) return;
        var clubs = ScreenFlow.Clubs;
        var table = season.Table;
        var items = Items();
        int h = 5 + ScreenArt.Row + 2 + Line + (table.Count * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        int round = Round(season);
        var (x, top) = art.Panel(season.IsOver ? "SHL  FINAL TABLE" : $"SHL  ROUND {N(round)} OF {N(season.Rounds)}", PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        foreach (var (head, cx) in Columns) art.Left(head, x + cx, top, ScreenArt.Muted);
        top += Line;
        for (int i = 0; i < table.Count; i++)
        {
            var r = table[i];
            var ink = string.Equals(r.ClubId, season.UserClub, StringComparison.Ordinal) ? art.Gold : art.Text;
            string name = Name(clubs, r.ClubId);
            art.Right(N(i + 1), x + 22, top, ink);
            art.Left(name, x + 30, top, ink);
            string[] cells = [N(r.Played), N(r.Wins), N(r.OvertimeWins), N(r.OvertimeLosses), N(r.Losses), $"{N(r.GoalsFor)}-{N(r.GoalsAgainst)}", N(r.Points)];
            for (int c = 0; c < cells.Length; c++) art.Left(cells[c], x + Columns[c].X, top, ink);
            top += Line;
        }

        top += 4;
        string next = season.NextGame is { } g
            ? $"NEXT: {Name(clubs, g.Home)} - {Name(clubs, g.Away)}"
            : season.IsOver ? "REGULAR SEASON OVER" : "WAITING FOR THE ROUND";
        art.Centred(next, Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 100, PanelW - 200, top);
    }

    /// <summary>
    /// The round in the header: the one the human's next game is in, as the NEXT line under the
    /// table says, from 1. Once the human's club has played all its games, the round being finished.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>The round, 1 to <see cref="SeasonState.Rounds"/>.</returns>
    public static int Round(SeasonState season)
    {
        int round = season.NextGame?.Round ?? season.CurrentRound;
        return Math.Clamp(round + 1, 1, Math.Max(1, season.Rounds));
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id)
    {
        return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
