using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>
/// W25: follows every match with a <see cref="MatchStatTracker"/> and, at MatchEnd, hands the
/// sheet to whatever the match belongs to: the season (regular or playoff game), the arcade run,
/// or just <see cref="MatchStats.Last"/> for exhibition and versus. Runs before
/// <see cref="ScheduleSystem"/>, <see cref="PlayoffSystem"/> and <see cref="ArcadeSystem"/>, so
/// the game being played is still known and the autosave that follows carries the lines; the
/// games settled on paper by those systems are credited on the next step with
/// <see cref="SeasonStats.CatchUp"/>.
/// </summary>
internal sealed class StatsSystem : ISystem
{
    readonly MatchStatTracker tracker = new();
    bool finished;
    bool catchUp = true;

    public bool RunsWhilePaused => true;

    public void Update(World world, float dt)
    {
        if (!ReferenceEquals(world, tracker.World))
        {
            finished = false;
            catchUp = true;
        }
        if (catchUp)
        {
            catchUp = false;
            if (SeasonSession.Current is { } s && s.Stats.CatchUp(s, SeasonSession.Clubs) > 0) SeasonSession.Unsaved = true;
        }
        if (finished) return;

        tracker.Observe(world);
        if (!world.Events.Of(MatchEventKind.MatchEnd).Any()) return;
        finished = true;
        catchUp = true;
        var sheet = tracker.Finish();
        MatchStats.Last = sheet;
        string star = sheet.PlayerOfGame is { } p ? $"{p.Label} ({p.Club}) {(p.IsGoalie ? $"{p.Saves} saves" : $"{p.Goals}G {p.Assists}A")}" : "nobody";
        Console.WriteLine($"SAP: stats: {sheet.Home} {sheet.HomeGoals}-{sheet.AwayGoals} {sheet.Away}, player of the game {star}");

        if (SeasonSession.Current is { } season && Matches(world, SeasonSession.Playing?.Home, SeasonSession.Playing?.Away))
        {
            season.Stats.Credit(SeasonStats.GameKey(SeasonSession.Playing!.Number), sheet);
        }
        else if (SeasonSession.Current is { Playoffs: { } playoffs } && PlayoffSession.Playing is { } series && Matches(world, series.NextHome, series.NextAway))
        {
            int round = playoffs.Rounds.Count - 1;
            int index = playoffs.Current.Series.IndexOf(series);
            if (index >= 0) SeasonSession.Current.Stats.Credit(SeasonStats.PlayoffKey(round, index, series.Games.Count), sheet);
        }
        else if (ArcadeSession.Current is { } run && ArcadeSession.Playing is { } playing && Matches(world, playing.Home.Id, playing.Away.Id))
        {
            run.Credit(sheet);
        }
    }

    static bool Matches(World world, string? home, string? away)
    {
        return string.Equals(home, world.Setup.Home.Id, StringComparison.Ordinal) && string.Equals(away, world.Setup.Away.Id, StringComparison.Ordinal);
    }
}
