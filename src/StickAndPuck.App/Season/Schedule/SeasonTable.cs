using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// A plain standings panel in the A3 pixel font: rank, club, games, regulation and overtime wins
/// and losses, goals and points, with the human's club in gold. A stand-in until W11's standings
/// screen, which can use the same <see cref="SeasonState.Table"/>.
/// </summary>
internal static class SeasonTable
{
    const int Line = 12, Pad = 6;

    static readonly (string Head, int X)[] Columns =
        [(K.ColGp, 150), (K.ColW, 174), (K.ColOtw, 194), (K.ColOtl, 222), (K.ColL, 250), (K.ColGoals, 268), (K.ColPts, 314)];

    /// <summary>Draws the table centred on the 480x270 frame.</summary>
    /// <param name="art">The HUD art.</param>
    /// <param name="season">The season.</param>
    /// <param name="clubs">Every club, for names.</param>
    public static void Draw(HudArt art, SeasonState season, IReadOnlyList<ClubData> clubs)
    {
        var table = season.Table;
        int w = 340, h = (Pad * 2) + ((table.Count + 2) * Line);
        int x = (Game.Width - w) / 2, y = Math.Max(2, (Game.Height - h) / 2);
        art.DrawPanel("panel", x, y, w, h);

        int played = Math.Min(season.CurrentRound, season.Rounds);
        int top = y + Pad - 2;
        art.DrawText(Text.Format(K.ShlRound, played, season.Rounds), x + Pad + 4, top, art.Score);
        top += Line;
        foreach (var (head, cx) in Columns) art.DrawText(Text.Get(head), x + cx, top, art.Text);

        for (int i = 0; i < table.Count; i++)
        {
            var r = table[i];
            top += Line;
            var ink = string.Equals(r.ClubId, season.UserClub, StringComparison.Ordinal) ? art.Score : art.Text;
            string name = clubs.FirstOrDefault(c => string.Equals(c.Id, r.ClubId, StringComparison.Ordinal))?.Name ?? r.ClubId;
            art.DrawText(Num(i + 1), x + Pad + 4, top, ink);
            art.DrawText(name, x + Pad + 22, top, ink);
            string[] cells = [Num(r.Played), Num(r.Wins), Num(r.OvertimeWins), Num(r.OvertimeLosses), Num(r.Losses), $"{r.GoalsFor}-{r.GoalsAgainst}", Num(r.Points)];
            for (int c = 0; c < cells.Length; c++) art.DrawText(cells[c], x + Columns[c].X, top, ink);
        }
    }

    static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
