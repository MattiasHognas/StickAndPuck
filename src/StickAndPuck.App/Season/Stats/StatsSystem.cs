using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>Follows every match with a <see cref="MatchStatTracker"/> and, at MatchEnd, hands the
/// sheet to whatever the match belongs to: the season (regular or playoff game), the arcade run,
/// or just <see cref="AppState.LastMatchStats"/> for exhibition and versus. Runs before
/// <see cref="ScheduleSystem"/>, <see cref="PlayoffSystem"/> and <see cref="ArcadeSystem"/>, so
/// the game being played is still known and the autosave that follows carries the lines; the
/// games settled on paper by those systems are credited on the next step with
/// <see cref="SeasonStats.CatchUp"/>.</summary>
internal sealed class StatsSystem : ISystem
{
    readonly MatchStatTracker tracker = new();
    bool finished;
    bool catchUp = true;

    public bool RunsWhilePaused => true;

    public void Update(World world, float dt)
    {
        if (!tracker.World.TryGet(out var followed) || !ReferenceEquals(world, followed))
        {
            finished = false;
            catchUp = true;
        }
        if (catchUp)
        {
            catchUp = false;
            if (world.App.Season.Current.TryGet(out var s) && s.Stats.CatchUp(s, world.App.Season.Clubs) > 0) world.App.Season.Unsaved = true;
        }
        if (finished) return;

        tracker.Observe(world);
        if (!world.Events.Of<MatchEnded>().Any()) return;
        finished = true;
        catchUp = true;
        var sheet = tracker.Finish();
        world.App.LastMatchStats = sheet;
        string star;
        if (sheet.PlayerOfGame.TryGet(out var p))
        {
            string performance = p.IsGoalie
                ? string.Create(CultureInfo.InvariantCulture, $"{p.Saves} saves")
                : string.Create(CultureInfo.InvariantCulture, $"{p.Goals}G {p.Assists}A");
            star = $"{p.Label} ({p.Club}) {performance}";
        }
        else
        {
            star = "nobody";
        }
        Console.WriteLine($"SAP: stats: {sheet.Home} {sheet.HomeGoals}-{sheet.AwayGoals} {sheet.Away}, player of the game {star}");

        if (world.App.Season.Current.TryGet(out var season) && world.App.Season.Playing.TryGet(out var game) && Matches(world, game.Home, game.Away))
        {
            season.Stats.Credit(SeasonStats.GameKey(game.Number), sheet);
        }
        else if (world.App.Season.Current.TryGet(out var current) && current.Playoffs.TryGet(out var playoffs) && world.App.Playoffs.Playing.TryGet(out var series) && Matches(world, series.NextHome, series.NextAway))
        {
            int round = playoffs.Rounds.Count - 1;
            int index = playoffs.Current.Series.IndexOf(series);
            if (index >= 0) current.Stats.Credit(SeasonStats.PlayoffKey(round, index, series.Games.Count), sheet);
        }
        else if (world.App.Arcade.Current.TryGet(out var run) && world.App.Arcade.Playing.TryGet(out var playing) && Matches(world, playing.Home.Id, playing.Away.Id))
        {
            run.Credit(sheet);
        }
    }

    static bool Matches(World world, string home, string away)
    {
        return string.Equals(home, world.Setup.Home.Id, StringComparison.Ordinal) && string.Equals(away, world.Setup.Away.Id, StringComparison.Ordinal);
    }
}
