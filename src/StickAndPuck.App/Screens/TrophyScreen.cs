using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>The trophy screen. For the SHL champion (after the final, played or simmed, and from the
/// bracket) the cup with the champion's crest, the final's score and the season's award winners;
/// for an arcade champion the cup, the run and its place on the best runs board.</summary>
internal sealed class TrophyScreen : MenuScreen
{
    const int PanelW = 400, Line = 12;

    static readonly Color Light = new(0xf9, 0xda, 0x93, 0xff), Deep = new(0xc8, 0x96, 0x3c, 0xff), Wood = new(0x42, 0x32, 0x36, 0xff);

    readonly string heading, club, title;
    readonly IReadOnlyList<(string Label, string Value)> lines;
    readonly IReadOnlyList<MenuItem> items;

    TrophyScreen(AppState app, string heading, string club, string title, IReadOnlyList<(string, string)> lines, IReadOnlyList<MenuItem> items)
        : base(app)
    {
        this.heading = heading;
        this.club = club;
        this.title = title;
        this.lines = lines;
        this.items = items;
    }

    /// <summary>The SHL champion of the season being played.</summary>
    public static TrophyScreen Season(AppState app)
    {
        var clubs = app.Screens.Clubs;
        var current = app.Season.Current;
        var playoffs = current.TryGet(out var open) ? open.Playoffs : Maybe<PlayoffState>.None;
        string champion = playoffs.TryGet(out var bracket) ? bracket.Champion.Or("") : "";
        List<(string, string)> statLines = [];
        if (playoffs.TryGet(out var decided) && decided.Champion.HasValue)
        {
            var final = decided.Current.Series[0];
            string other = string.Equals(final.High, champion, StringComparison.Ordinal) ? final.Low : final.High;
            int won = Math.Max(final.HighWins, final.LowWins), lost = Math.Min(final.HighWins, final.LowWins);
            statLines.Add((Text.Get(K.TrophyFinal), Text.Format(K.TrophyFinalLine, won, lost, Name(clubs, other))));
        }
        if (current.TryGet(out var season))
        {
            season.Stats.CatchUp(season, clubs);    // games settled on paper are credited from their score
            var stats = season.Stats;
            if (stats.Regular.Leaders(1).FirstOrDefault() is { } top) statLines.Add((Text.Get(K.TopScorer), Text.Format(K.TopScorerLine, top.Label, Short(clubs, top.Club), top.Points)));
            if (stats.Regular.Scorers(1).FirstOrDefault() is { } sniper) statLines.Add((Text.Get(K.MostGoals), Text.Format(K.MostGoalsLine, sniper.Label, Short(clubs, sniper.Club), sniper.Goals)));
            if (stats.Regular.Goalies(1).FirstOrDefault() is { } goalie) statLines.Add((Text.Get(K.BestGoalie), Text.Format(K.BestGoalieLine, goalie.Label, Short(clubs, goalie.Club), goalie.Saves)));
            if (stats.Playoffs.Leaders(1).FirstOrDefault() is { } hero) statLines.Add((Text.Get(K.PlayoffStar), Text.Format(K.TopScorerLine, hero.Label, Short(clubs, hero.Club), hero.Points)));
            string user = Name(clubs, season.UserClub);
            string yourClubLine;
            if (string.Equals(season.UserClub, champion, StringComparison.Ordinal))
            {
                yourClubLine = Text.Get(K.ChampionsShout);
            }
            else
            {
                var resultKey = playoffs.Is(p => !p.UserQualified) ? K.MissedPlayoffs : K.WentOut;
                yourClubLine = Text.Format(resultKey, user);
            }
            statLines.Add((Text.Get(K.YourClub), yourClubLine));
        }
        return new TrophyScreen(app, Text.Get(K.ShlChampions), champion, Name(clubs, champion), statLines,
        [
            new MenuItem(Text.Get(K.Continue), app.Screens.Pop),
            new MenuItem(Text.Get(K.SeasonStats), () => app.Screens.Replace(new SeasonStatsScreen(app))),
        ]);
    }

    /// <summary>An arcade run won by beating every club.</summary>
    public static TrophyScreen Arcade(AppState app, ArcadeRun run)
    {
        var clubs = app.Screens.Clubs;
        List<(string, string)> statLines =
        [
            (Text.Get(K.Rule), Text.Get(run.Rule == ArcadeRule.SuddenDeath ? K.SuddenDeath : K.ThreeLives)),
            (Text.Get(K.Record), Text.Format(run.Losses == 1 ? K.RecordLineOne : K.RecordLineMany, run.Stage, run.Losses)),
            (Text.Get(K.GoalsLabel), $"{N(run.Games.Sum(g => g.GoalsFor))}-{N(run.Games.Sum(g => g.GoalsAgainst))}"),
        ];
        if (run.Stats.Leaders(1, run.UserClub).FirstOrDefault() is { } top)
            statLines.Add((Text.Get(K.TopScorer), Text.Format(K.TopScorerArcade, top.Label, top.Points)));
        if (run.Place > 0)
            statLines.Add((Text.Get(K.BestRuns), Text.Format(K.PlaceOf, run.Place, run.Records.Count)));
        return new TrophyScreen(app, Text.Get(K.ArcadeChampions), run.UserClub, Name(clubs, run.UserClub), statLines,
        [
            new MenuItem(Text.Get(K.BestRuns), () => app.Screens.Replace(new ArcadeBoardScreen(app))),
            new MenuItem(Text.Get(K.Continue), app.Screens.Pop),
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
        clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).Select(c => c.Name).Or(id);

    static string Short(IReadOnlyList<ClubData> clubs, string id)
    {
        var club = clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return club is { ShortName.Length: > 0 } ? club.ShortName : id;
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
