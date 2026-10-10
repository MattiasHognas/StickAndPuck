using System.Globalization;
using StickAndPuck.Core;
using StickAndPuck.Hud;

namespace StickAndPuck.Season;

/// <summary>
/// A plain arcade panel in the A3 pixel font: the human's club, the rule, clubs beaten and lives
/// left, then every game with W or L, the score and how it was decided. The heading says whether the
/// run goes on, ended on a loss, or ended as champions. A stand-in until W11's results screen, which
/// can read the same <see cref="ArcadeRun"/>.
/// </summary>
internal static class ArcadeSummary
{
    const int Line = 12, Pad = 6, Width = 300;

    /// <summary>Draws the panel centred on the 480x270 frame.</summary>
    /// <param name="art">The HUD art.</param>
    /// <param name="run">The run.</param>
    /// <param name="clubs">Every club, for names.</param>
    public static void Draw(HudArt art, ArcadeRun run, IReadOnlyList<ClubData> clubs)
    {
        string Name(string id) => clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;

        int rows = run.Games.Count + 4 + (run.IsOver ? 0 : 1);
        int h = (Pad * 2) + (rows * Line);
        int x = (Game.Width - Width) / 2, y = Math.Max(2, (Game.Height - h) / 2);
        art.DrawPanel(run.Champion ? "panel_gold" : run.Lost ? "panel_red" : "panel", x, y, Width, h);

        int left = x + Pad + 4, top = y + Pad - 2;
        string head = run.Champion ? "ARCADE  CHAMPIONS" : run.Lost ? "ARCADE  RUN OVER" : "ARCADE";
        art.DrawText(head, left, top, art.Score);
        top += Line;
        art.DrawText(Name(run.UserClub), left, top, art.Score);
        art.DrawText(run.Rule == ArcadeRule.SuddenDeath ? "SUDDEN DEATH" : "THREE LIVES", x + 190, top, art.Text);
        top += Line;
        art.DrawText($"BEATEN {Num(run.Stage)} OF {Num(run.Opponents.Count)}", left, top, art.Text);
        art.DrawText($"LIVES {Num(run.LivesLeft)} OF {Num(run.StartLives)}", x + 190, top, art.Text);
        top += Line + (Line / 2);

        for (int i = 0; i < run.Games.Count; i++)
        {
            var g = run.Games[i];
            var ink = g.Won ? art.Text : art.Score;
            string extra = g.Result.Kind switch { ResultKind.Overtime => " OT", ResultKind.Shootout => " SO", _ => "" };
            art.DrawText(Num(i + 1), left, top, ink);
            art.DrawText(g.Won ? "W" : "L", left + 18, top, ink);
            art.DrawText($"{(g.Home ? "VS" : "AT")} {Name(g.Opponent)}", left + 34, top, ink);
            art.DrawText($"{Num(g.GoalsFor)}-{Num(g.GoalsAgainst)}{extra}", x + 230, top, ink);
            top += Line;
        }

        if (!run.IsOver) art.DrawText($"NEXT  {(run.NextAtHome ? "VS" : "AT")} {Name(run.NextOpponent!)}", left, top, art.Score);
    }

    static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
