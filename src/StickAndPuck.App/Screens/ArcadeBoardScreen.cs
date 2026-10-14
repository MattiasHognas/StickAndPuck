using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>The arcade best runs board (<see cref="ArcadeRun.Records"/>, kept across runs in
/// arcade.json): place, club, rule, how far the run got, losses, goals and its top scorer, the run
/// just finished in gold. Opened after a run ends and from the new game list.</summary>
internal sealed class ArcadeBoardScreen(AppState app) : MenuScreen(app)
{
    const int PanelW = 460, Line = 12;

    readonly (IReadOnlyList<ArcadeRecord> Records, int Place) board = Board(app);

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items() => [new MenuItem(Text.Get(K.Back), Back)];

    /// <summary>Gets a value indicating whether there is a board to show (any run ever finished). Reads the save.</summary>
    public static bool Any(AppState app) => Board(app).Records.Count > 0;

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var (records, place) = board;
        var clubs = Flow.Clubs;
        int rows = Math.Max(1, records.Count);
        int h = 5 + ScreenArt.Row + 2 + Line + (rows * Line) + 8 + ScreenArt.Row + 6;
        var (x, top) = art.Panel(Text.Get(K.ArcadeBestRuns), PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        (string Head, int X)[] columns = [(K.ColClub, 24), (K.ColRule, 146), (K.ColRun, 198), (K.ColL, 262), (K.ColGoals, 278), (K.ColTopScorer, 322)];
        foreach (var (head, cx) in columns) art.Left(Text.Get(head), x + cx, top, ScreenArt.Muted);
        top += Line;
        if (records.Count == 0) art.Left(Text.Get(K.NoRunsYet), x + 24, top, ScreenArt.Muted);
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var ink = i + 1 == place ? art.Gold : art.Text;
            string name = clubs.FirstOrNone(c => string.Equals(c.Id, r.Club, StringComparison.Ordinal)).Select(c => c.Name).Or(r.Club);
            art.Right(N(i + 1), x + 18, top, ink);
            if (r.Champion) art.Hud.DrawIcon("star", x + 251, top + 2);
            art.Left(art.Clip(name, 118), x + 24, top, ink);
            art.Left(Text.Get(r.Rule == ArcadeRule.SuddenDeath ? K.RuleSudden : K.RuleLives), x + 146, top, ink);
            art.Left(r.Champion ? Text.Get(K.Champions) : Text.Format(K.Beaten, r.Beaten), x + 198, top, ink);
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
    static (IReadOnlyList<ArcadeRecord> Records, int Place) Board(AppState app)
    {
        var open = app.Arcade.Current.HasValue ? app.Arcade.Current : ArcadeSave.Read(app.Arcade.SavePath.Length > 0 ? app.Arcade.SavePath : ArcadeSave.DefaultPath);
        if (!open.TryGet(out var run)) return ([], 0);
        return (run.Records, run.IsOver ? run.Place : 0);
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
