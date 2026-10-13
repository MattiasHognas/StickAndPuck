using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// A plain bracket panel in the A3 pixel font: one column per round, each series as its two seeds
/// with their wins, the human's club in gold and the series loser dimmed, and the champion (or how
/// far the human's club got) underneath. A stand-in until W11's screens, which can read the same
/// <see cref="PlayoffState"/>.
/// </summary>
internal static class PlayoffBracket
{
    const int Line = 11, Pad = 6, Width = 468, Height = 230, Column = 114;

    /// <summary>Draws the bracket centred on the 480x270 frame.</summary>
    /// <param name="art">The HUD art.</param>
    /// <param name="playoffs">The playoffs.</param>
    /// <param name="clubs">Every club, for names.</param>
    public static void Draw(HudArt art, PlayoffState playoffs, IReadOnlyList<ClubData> clubs)
    {
        int x = (Game.Width - Width) / 2, y = (Game.Height - Height) / 2;
        art.DrawPanel("panel", x, y, Width, Height);
        art.DrawText(Text.Get(K.ShlPlayoffs), x + Pad + 4, y + Pad - 2, art.Score);

        int top = y + Pad + Line + 4;
        for (int r = 0; r < PlayoffState.RoundCount; r++)
        {
            int cx = x + Pad + 4 + (r * Column);
            var round = r < playoffs.Rounds.Count ? playoffs.Rounds[r] : null;
            art.DrawText(PlayoffState.RoundTitle(r), cx, top, art.Text);
            var series = round?.Series ?? (r == 1 ? Waiting(playoffs) : []);
            int gap = r == 0 ? 3 * Line : (Height - (Pad * 2) - (Line * 6)) / Math.Max(1, series.Count);
            for (int s = 0; s < series.Count; s++)
            {
                int sy = top + Line + 4 + (s * gap);
                DrawSeries(art, series[s], playoffs.UserClub, clubs, cx, sy);
            }
        }
        art.DrawText(Footer(playoffs, clubs), x + Pad + 4, y + Height - Pad - Line, art.Score);
    }

    static void DrawSeries(HudArt art, PlayoffSeries series, string user, IReadOnlyList<ClubData> clubs, int x, int y)
    {
        DrawSide(art, series.HighSeed, series.High, series.HighWins, series, user, clubs, x, y);
        DrawSide(art, series.LowSeed, series.Low, series.LowWins, series, user, clubs, x, y + Line);
    }

    static void DrawSide(HudArt art, int seed, string club, int wins, PlayoffSeries series, string user, IReadOnlyList<ClubData> clubs, int x, int y)
    {
        if (seed == 0)
        {
            art.DrawText("?", x + 16, y, art.Text);
            return;
        }
        var ink = string.Equals(club, user, StringComparison.Ordinal) ? art.Score : art.Text;
        if (series.IsOver && !string.Equals(series.Winner, club, StringComparison.Ordinal)) ink = Raylib.ColorAlpha(ink, 0.4f);
        art.DrawText(Num(seed), x, y, ink);
        art.DrawText(Short(clubs, club), x + 16, y, ink);
        art.DrawText(Num(wins), x + 60, y, ink);
    }

    static string Footer(PlayoffState playoffs, IReadOnlyList<ClubData> clubs)
    {
        if (playoffs.Champion is { } champion) return Text.Format(K.ShlChampionsOf, Name(clubs, champion));
        string user = Name(clubs, playoffs.UserClub);
        if (!playoffs.UserQualified) return Text.Format(K.MissedPlayoffsPlace, user, playoffs.SeedOf(playoffs.UserClub));
        if (playoffs.UserSeries is { } s)
        {
            string other = Name(clubs, string.Equals(s.High, playoffs.UserClub, StringComparison.Ordinal) ? s.Low : s.High);
            return Text.Format(K.NextPlayoffGameV, playoffs.CurrentTitle, s.Games.Count + 1, other);
        }
        return Text.Format(playoffs.UserAlive ? K.WaitForPlayIn : K.AreOut, user);
    }

    /// <summary>The quarter-finals while the play-in runs: the top six seeds, the top two waiting for the play-in winners.</summary>
    static List<PlayoffSeries> Waiting(PlayoffState playoffs)
    {
        PlayoffSeries Pair(int high, int low) => new()
        {
            HighSeed = high, LowSeed = low, High = playoffs.Seeds[high - 1], Low = low == 0 ? "" : playoffs.Seeds[low - 1], BestOf = 7,
        };
        return playoffs.Seeds.Count < PlayoffState.Direct ? [] : [Pair(1, 0), Pair(2, 0), Pair(3, 6), Pair(4, 5)];
    }

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id)
    {
        return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))?.Name ?? id;
    }

    static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
