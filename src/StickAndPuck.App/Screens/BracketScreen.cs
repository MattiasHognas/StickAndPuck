using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>
/// The playoff bracket of the season being played (W16b's <see cref="PlayoffState"/>): one column
/// per round, each series as two seeds with their wins, the human's club in gold and series losers
/// greyed, and the champion or the human's next game underneath. Enter plays it.
/// </summary>
internal sealed class BracketScreen : MenuScreen
{
    const int PanelW = 468, Line = 11, Column = 114;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [new MenuItem(Text.Get(K.PlayNextGame), ScreenFlow.PlaySeasonGame, Enabled: ScreenFlow.SeasonHasNextGame())];
        if (ScreenFlow.Settings.SimGame) items.Add(new MenuItem(Text.Get(K.SimThisGame), ScreenFlow.SimSeasonGame, Enabled: ScreenFlow.CanSimGame()));
        if (PlayoffSession.Current?.Champion != null) items.Add(new MenuItem(Text.Get(K.Trophy), () => ScreenFlow.Push(TrophyScreen.Season())));
        items.Add(new MenuItem(Text.Get(K.TableItem), () => ScreenFlow.Replace(new StandingsScreen())));
        items.Add(new MenuItem(Text.Get(K.SeasonStats), () => ScreenFlow.Replace(new SeasonStatsScreen())));
        items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (PlayoffSession.Current is not { } playoffs) return;
        var clubs = ScreenFlow.Clubs;
        int rows = 4 * 3;
        var items = Items();
        int h = 5 + ScreenArt.Row + 2 + Line + 4 + (rows * Line) + 6 + Line + 4 + (items.Count * ScreenArt.Row) + 4;
        var (x, top) = art.Panel(Text.Get(K.ShlPlayoffs), PanelW, h, Math.Max(2, (Game.Height - h - 15) / 2));

        for (int r = 0; r < PlayoffState.RoundCount; r++)
        {
            int cx = x + 10 + (r * Column);
            var round = r < playoffs.Rounds.Count ? playoffs.Rounds[r] : null;
            art.Left(PlayoffState.RoundTitle(r), cx, top, round == null ? ScreenArt.Muted : art.Text);
            if (round == null) continue;
            int gap = round.Series.Count <= 1 ? 0 : (rows * Line - (2 * Line)) / (round.Series.Count - 1);
            gap = Math.Min(gap, 3 * Line);
            for (int s = 0; s < round.Series.Count; s++)
                DrawSeries(art, round.Series[s], playoffs.UserClub, clubs, cx, top + Line + 4 + (s * gap));
        }

        top += Line + 4 + (rows * Line) + 6;
        art.Centred(Footer(playoffs, clubs), Game.Width / 2, top, art.Gold);
        top += Line + 4;
        art.Menu(items, Selected, x + 140, PanelW - 280, top);
    }

    static void DrawSeries(ScreenArt art, PlayoffSeries series, string user, IReadOnlyList<ClubData> clubs, int x, int y)
    {
        DrawSide(art, series.HighSeed, series.High, series.HighWins, series, user, clubs, x, y);
        DrawSide(art, series.LowSeed, series.Low, series.LowWins, series, user, clubs, x, y + Line);
    }

    static void DrawSide(ScreenArt art, int seed, string club, int wins, PlayoffSeries series, string user, IReadOnlyList<ClubData> clubs, int x, int y)
    {
        var ink = string.Equals(club, user, StringComparison.Ordinal) ? art.Gold : art.Text;
        if (series.IsOver && !string.Equals(series.Winner, club, StringComparison.Ordinal)) ink = ScreenArt.Muted;
        art.Right(N(seed), x + 10, y, ink);
        art.Left(Short(clubs, club), x + 16, y, ink);
        art.Right(N(wins), x + 76, y, ink);
    }

    static string Footer(PlayoffState playoffs, IReadOnlyList<ClubData> clubs)
    {
        if (playoffs.Champion is { } champion) return Text.Format(K.ShlChampionsOf, Name(clubs, champion));
        string user = Name(clubs, playoffs.UserClub);
        if (!playoffs.UserQualified) return Text.Format(K.MissedPlayoffs, user);
        if (playoffs.UserSeries is { } s)
        {
            string other = Name(clubs, string.Equals(s.High, playoffs.UserClub, StringComparison.Ordinal) ? s.Low : s.High);
            return Text.Format(K.NextPlayoffGame, playoffs.CurrentTitle, s.Games.Count + 1, other);
        }
        return Text.Format(playoffs.UserAlive ? K.WaitForNextRound : K.AreOut, user);
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

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
