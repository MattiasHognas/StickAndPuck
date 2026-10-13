using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

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
            new MenuItem(Text.Get(K.Resume), Resume),
            new MenuItem(Text.Get(K.Settings), () => ScreenFlow.Push(new SettingsScreen())),
            new MenuItem(Text.Get(K.QuitToMenu), () => ScreenFlow.Push(new ConfirmScreen(Text.Get(K.LeaveMatch), () => ScreenFlow.ToMenus()))),
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
        var (x, top) = art.Panel(Text.Get(K.Paused), PanelW, (items.Count * ScreenArt.Row) + 30, Top);
        art.Menu(items, Selected, x, PanelW, top);
    }

    static void Resume()
    {
        ScreenFlow.Clear();
    }
}

/// <summary>Results: the final score with crests, how it was decided, shots and the goals, each club's in its
/// own column (period, clock, scorer cut to fit), and the player of the game. Season games go on to
/// the standings, the season stats or the trophy once the champion is crowned; an arcade run that is
/// over goes on to its trophy or the best runs board.</summary>
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
            if (PlayoffSession.Current.Is(p => p.Champion.HasValue)) items.Add(new MenuItem(Text.Get(K.Champions), () => ScreenFlow.Replace(TrophyScreen.Season())));
            items.Add(new MenuItem(Text.Get(K.Standings), () => ScreenFlow.Replace(ScreenFlow.Overview())));
            if (ScreenFlow.CanSimGame()) items.Add(new MenuItem(Text.Get(K.SimNextGame), ScreenFlow.SimSeasonGame));
            items.Add(new MenuItem(Text.Get(K.SeasonStats), () => ScreenFlow.Replace(new SeasonStatsScreen())));
            items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
            return items;
        }
        if (plan.Mode.Is(m => m is ArcadeMode) && ArcadeSession.Current.TryGet(out var run) && run.IsOver)
        {
            List<MenuItem> items = [];
            if (run.Champion) items.Add(new MenuItem(Text.Get(K.Trophy), () => ScreenFlow.Replace(TrophyScreen.Arcade(run))));
            items.Add(new MenuItem(Text.Get(K.BestRuns), () => ScreenFlow.Replace(new ArcadeBoardScreen())));
            items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
            return items;
        }
        if (plan.Mode.TryGet(out var mode) && mode.HasNextGame)
            return [new MenuItem(Text.Get(K.NextGame), () => ScreenFlow.PlayMode(mode, plan.Picks)), new MenuItem(Text.Get(K.MainMenu), Back)];
        if (plan.Mode.TryGet(out var played) && played is ExhibitionMode exhibition)
            return [new MenuItem(Text.Get(K.PlayAgain), () => ScreenFlow.PlayMode(exhibition, plan.Picks)), new MenuItem(Text.Get(K.MainMenu), Back)];
        return [new MenuItem(Text.Get(K.MainMenu), Back)];
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
        var status = result.Plan.Mode.TryGet(out var mode) ? mode.Status : Maybe<string>.None;
        var star = MatchStats.Last.TryGet(out var last) && last.PlayerOfGame.TryGet(out var p) && string.Equals(last.Home, result.Setup.Home.Id, StringComparison.Ordinal) && string.Equals(last.Away, result.Setup.Away.Id, StringComparison.Ordinal) ? p : Maybe<MatchStatLine>.None;
        var home = result.Goals.Where(g => g.Side == Side.Home).ToList();
        var away = result.Goals.Where(g => g.Side == Side.Away).ToList();
        int extra = 5 + ScreenArt.Row + 2 + 24 + 14 + 6 + 6 + (star.HasValue ? 14 : 0) + (status.HasValue ? 14 : 0) + (items.Count * ScreenArt.Row) + 6;
        int fits = Math.Max(2, (Game.Height - 8 - extra) / Line);
        int rows = Math.Max(1, Math.Min(Math.Max(home.Count, away.Count), fits));
        int h = extra + (rows * Line);
        string heading = result.Decided.Length > 0 ? Text.Format(K.FinalDecided, result.Decided) : Text.Get(K.Final);
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
        art.Centred(Text.Format(K.ResultShots, result.HomeShots, result.AwayShots), cx, top, ScreenArt.Muted);
        top += 14 + 6;

        if (result.Goals.Count == 0) art.Centred(Text.Get(K.NoGoals), cx, top, ScreenArt.Muted);
        int colW = cx - 8 - (x + 12);
        Column(art, home, x + 12, colW, top, rows);
        Column(art, away, cx + 8, colW, top, rows);
        top += (rows * Line) + 6;
        if (star.TryGet(out var shown))
        {
            art.Centred(shown.IsGoalie ? Text.Format(K.PlayerOfGameSaves, shown.Label, shown.Saves) : Text.Format(K.PlayerOfGamePoints, shown.Label, shown.Goals, shown.Assists), cx, top, art.Gold);
            top += 14;
        }
        if (status.HasValue)
        {
            art.Centred(status.Or(""), cx, top, art.Gold);
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
        if (goals.Count > shown) art.Left(Text.Format(K.MoreGoals, goals.Count - shown), x + ScorerX, top, ScreenArt.Muted);
    }

    Color Ink(ScreenArt art, Side side)
    {
        int mine = side == Side.Home ? result.HomeGoals : result.AwayGoals, theirs = side == Side.Home ? result.AwayGoals : result.HomeGoals;
        return mine > theirs ? art.Gold : art.Text;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Standings: the SHL table of the season being played (<see cref="SeasonState.Table"/>),
/// the human's club in gold, and the next game. Enter plays it.</summary>
internal sealed class StandingsScreen : MenuScreen
{
    const int PanelW = 380, Line = 11;

    static readonly (string Head, int X)[] Columns =
        [(K.ColGp, 170), (K.ColW, 196), (K.ColOtw, 216), (K.ColOtl, 246), (K.ColL, 276), (K.ColGoals, 296), (K.ColPts, 348)];

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var season = SeasonSession.Current;
        string play = Text.Get(season.Is(s => s.IsOver && !s.Playoffs.HasValue) ? K.StartPlayoffsItem : K.PlayNextGame);
        List<MenuItem> items = [new MenuItem(play, ScreenFlow.PlaySeasonGame, enabled: ScreenFlow.SeasonHasNextGame())];
        if (ScreenFlow.Settings.SimGame) items.Add(new MenuItem(Text.Get(K.SimThisGame), ScreenFlow.SimSeasonGame, enabled: ScreenFlow.CanSimGame()));
        if (season.Is(s => s.Playoffs.HasValue)) items.Add(new MenuItem(Text.Get(K.Playoffs), () => ScreenFlow.Replace(new BracketScreen())));
        items.Add(new MenuItem(Text.Get(K.SeasonStats), () => ScreenFlow.Replace(new SeasonStatsScreen())));
        items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (!SeasonSession.Current.TryGet(out var season)) return;
        var clubs = ScreenFlow.Clubs;
        var table = season.Table;
        var items = Items();
        int h = 5 + ScreenArt.Row + 2 + Line + (table.Count * Line) + 4 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        int round = Round(season);
        var (x, top) = art.Panel(season.IsOver ? Text.Get(K.ShlFinalTable) : Text.Format(K.ShlRound, round, season.Rounds), PanelW, h, Math.Max(2, (Game.Height - h - 14) / 2));

        foreach (var (head, cx) in Columns) art.Left(Text.Get(head), x + cx, top, ScreenArt.Muted);
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
        string next = season.NextGame.TryGet(out var g)
            ? Text.Format(K.NextMatch, Name(clubs, g.Home), Name(clubs, g.Away))
            : Text.Get(season.IsOver ? K.RegularSeasonOver : K.WaitingForRound);
        art.Centred(next, Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 100, PanelW - 200, top);
    }

    /// <summary>The round in the header: the one the human's next game is in, as the NEXT line under the
    /// table says, from 1. Once the human's club has played all its games, the round being finished.</summary>
    public static int Round(SeasonState season)
    {
        int round = season.NextGame.TryGet(out var next) ? next.Round : season.CurrentRound;
        return Math.Clamp(round + 1, 1, Math.Max(1, season.Rounds));
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id)
    {
        return clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).Select(c => c.Name).Or(id);
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
