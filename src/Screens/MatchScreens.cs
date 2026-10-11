using System.Globalization;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Season;

namespace StickAndPuck.Screens;

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

/// <summary>Results: the final score with crests, how it was decided, shots and the goals. Season games go on to the standings.</summary>
/// <param name="result">The result.</param>
internal sealed class ResultsScreen(MatchResult result) : MenuScreen
{
    const int PanelW = 340, MaxGoals = 9;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var plan = result.Plan;
        if (plan.Season) return [new MenuItem("STANDINGS", () => ScreenFlow.Replace(ScreenFlow.Overview())), new MenuItem("MAIN MENU", Back)];
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
        int goals = Math.Min(result.Goals.Count, MaxGoals);
        string? status = result.Plan.Mode?.Status;
        int h = 5 + ScreenArt.Row + 2 + 24 + 14 + 6 + (goals * 11) + 6 + (status != null ? 14 : 0) + (items.Count * ScreenArt.Row) + 6;
        string heading = result.Decided.Length > 0 ? $"FINAL - {result.Decided}" : "FINAL";
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        int cx = Game.Width / 2;

        // crests, names and the score
        var home = result.Setup.Home;
        var away = result.Setup.Away;
        art.Hud.DrawCrest(home.Id, cx - 40 - 16, top);
        art.Hud.DrawCrest(away.Id, cx + 40, top);
        art.Centred($"{N(result.HomeGoals)} - {N(result.AwayGoals)}", cx, top + 3, art.Gold);
        art.Right(home.Name, cx - 40 - 22, top + 3, Ink(art, Side.Home));
        art.Left(away.Name, cx + 40 + 22, top + 3, Ink(art, Side.Away));
        top += 24;
        art.Centred($"SHOTS  {N(result.HomeShots)} - {N(result.AwayShots)}", cx, top, ScreenArt.Muted);
        top += 14 + 6;

        for (int i = 0; i < goals; i++)
        {
            var g = result.Goals[i];
            string text = $"{g.Period}  {g.Clock}  {g.Scorer}";
            if (g.Side == Side.Home) art.Right(text, cx - 8, top, art.Text);
            else art.Left(text, cx + 8, top, art.Text);
            top += 11;
        }
        if (result.Goals.Count > MaxGoals) art.Centred($"+{N(result.Goals.Count - MaxGoals)} MORE", cx, top - 11, ScreenArt.Muted);
        top += 6;
        if (status != null)
        {
            art.Centred(status, cx, top, art.Gold);
            top += 14;
        }
        art.Menu(items, Selected, x + 60, PanelW - 120, top);
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
        if (ScreenFlow.Prefs.Settings.SimGame) items.Add(new MenuItem("SIM THIS GAME", ScreenFlow.SimSeasonGame, Enabled: ScreenFlow.CanSimGame()));
        if (season?.Playoffs != null) items.Add(new MenuItem("PLAYOFFS", () => ScreenFlow.Replace(new BracketScreen())));
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
        int round = Math.Min(season.CurrentRound + 1, season.Rounds);
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

    static string Name(IReadOnlyList<ClubData> clubs, string id)
    {
        return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
