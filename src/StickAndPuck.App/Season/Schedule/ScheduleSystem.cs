using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;

namespace StickAndPuck.App.Season;

/// <summary>
/// W16a: when a season game ends (MatchEnd), records its result in <see cref="SeasonSession"/>
/// and settles the rest of the round; <see cref="AutosaveSystem"/> saves it next. Until W11's
/// results and standings screens exist, the table shows a few seconds after a season game's final,
/// and always in SAP_SCENE=seasontable.
/// </summary>
internal sealed class ScheduleSystem : ISystem, IDisposable
{
    /// <summary>Seconds after the final before the stand-in table shows.</summary>
    const float TableDelay = 4;

    Maybe<World> watching;
    bool sawShootout;
    float finishedAt = float.PositiveInfinity;
    Maybe<HudArt> art;

    /// <summary>Gets or sets a value indicating whether the table always shows (the seasontable scene).</summary>
    public static bool ShowTable { get; set; }

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        if (SeasonSession.Current.HasValue) art = HudArt.Load();
    }

    public void Update(World world, float dt)
    {
        if (!watching.TryGet(out var seen) || !ReferenceEquals(world, seen))
        {
            watching = world;
            sawShootout = false;
            finishedAt = float.PositiveInfinity;
        }
        if (world.Match.Phase == MatchPhase.Shootout) sawShootout = true;

        if (!SeasonSession.Playing.TryGet(out var game) || !string.Equals(game.Home, world.Setup.Home.Id, StringComparison.Ordinal) || !string.Equals(game.Away, world.Setup.Away.Id, StringComparison.Ordinal)) return;
        if (!world.Events.Of<MatchEnded>().Any()) return;

        var m = world.Match;
        var kind = sawShootout ? ResultKind.Shootout : m.Period > world.Setup.Periods ? ResultKind.Overtime : ResultKind.Regulation;
        var result = new FixtureResult(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind);
        if (!SeasonSession.Finish(result)) return;
        finishedAt = world.Time;
        Console.WriteLine($"SAP: season game {game.Number + 1}, round {game.Round + 1}: {game.Home} {result.HomeGoals}-{result.AwayGoals} {game.Away} ({kind})");
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !SeasonSession.Current.TryGet(out var season)) return;
        if (ShowTable || world.Time - finishedAt >= TableDelay) SeasonTable.Draw(hud, season, SeasonSession.Clubs);
    }

    public void Dispose()
    {
        if (art.TryGet(out var loaded)) loaded.Unload();
        art = Maybe<HudArt>.None;
    }
}
