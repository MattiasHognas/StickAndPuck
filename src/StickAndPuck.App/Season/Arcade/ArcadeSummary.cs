using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>A plain arcade panel in the A3 pixel font: the human's club, the rule, clubs beaten and lives
/// left, then every game with W or L, the score and how it was decided. The heading says whether the
/// run goes on, ended on a loss, or ended as champions, and after the run the best runs so far
/// (<see cref="ArcadeRun.Records"/>). The trophy screen reads the same <see cref="ArcadeRun"/>.</summary>
internal static class ArcadeSummary
{
    const int Line = 12, Pad = 6, Width = 300, BestShown = 3;

    /// <summary>Draws the panel centred on the 480x270 frame.</summary>
    public static void Draw(HudArt art, ArcadeRun run, IReadOnlyList<ClubData> clubs)
    {
        string Name(string id) => clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).Select(c => c.Name).Or(id);

        var best = run.IsOver ? run.Records.Take(BestShown).ToList() : [];
        int rows = run.Games.Count + 4 + (run.IsOver ? 0 : 1) + (best.Count > 0 ? best.Count + 1 : 0);
        int h = (Pad * 2) + (rows * Line);
        int x = (Game.Width - Width) / 2, y = Math.Max(2, (Game.Height - h) / 2);
        string panel;
        if (run.Champion)
            panel = "panel_gold";
        else if (run.Lost)
            panel = "panel_red";
        else
            panel = "panel";
        art.DrawPanel(panel, x, y, Width, h);

        int left = x + Pad + 4, top = y + Pad - 2;
        string head;
        if (run.Champion)
            head = Text.Get(K.ArcadeHeadChampions);
        else if (run.Lost)
            head = Text.Get(K.ArcadeHeadRunOver);
        else
            head = Text.Get(K.ArcadeHead);
        art.DrawText(head, left, top, art.Score);
        top += Line;
        art.DrawText(Name(run.UserClub), left, top, art.Score);
        art.DrawText(Text.Get(run.Rule == ArcadeRule.SuddenDeath ? K.SuddenDeath : K.ThreeLives), x + 190, top, art.Text);
        top += Line;
        art.DrawText(Text.Format(K.BeatenOf, run.Stage, run.Opponents.Count), left, top, art.Text);
        art.DrawText(Text.Format(K.LivesOf, run.LivesLeft, run.StartLives), x + 190, top, art.Text);
        top += Line + (Line / 2);

        for (int i = 0; i < run.Games.Count; i++)
        {
            var g = run.Games[i];
            var ink = g.Won ? art.Text : art.Score;
            string extra = g.Result.Kind switch { ResultKind.Overtime => Text.Get(K.SuffixOt), ResultKind.Shootout => Text.Get(K.SuffixSo), _ => "" };
            art.DrawText(Num(i + 1), left, top, ink);
            art.DrawText(Text.Get(g.Won ? K.Win : K.Loss), left + 18, top, ink);
            art.DrawText(Text.Format(g.Home ? K.VsClub : K.AtClub, Name(g.Opponent)), left + 34, top, ink);
            art.DrawText($"{Num(g.GoalsFor)}-{Num(g.GoalsAgainst)}{extra}", x + 230, top, ink);
            top += Line;
        }

        if (!run.IsOver) art.DrawText(Text.Format(run.NextAtHome ? K.NextVs : K.NextAt, Name(run.NextOpponent.Or(""))), left, top, art.Score);
        if (best.Count == 0) return;

        // The best runs so far, this one marked
        art.DrawText(Text.Get(K.BestRunsHead), left, top, art.Score);
        top += Line;
        for (int i = 0; i < best.Count; i++)
        {
            var r = best[i];
            bool mine = i + 1 == run.Place;
            var ink = mine ? art.Score : art.Text;
            art.DrawText(Num(i + 1), left, top, ink);
            art.DrawText(Text.Format(K.BestRunLine, Name(r.Club), r.Champion ? Text.Get(K.Champions) : Text.Format(K.Beaten, r.Beaten)), left + 18, top, ink);
            art.DrawText(Text.Get(r.Rule == ArcadeRule.SuddenDeath ? K.RuleSd : K.Rule3l), x + 230, top, ink);
            art.DrawText($"{Num(r.GoalsFor)}-{Num(r.GoalsAgainst)}", x + 250, top, ink);
            top += Line;
        }
    }

    static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
