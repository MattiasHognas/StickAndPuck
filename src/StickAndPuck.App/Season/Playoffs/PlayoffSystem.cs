using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;

namespace StickAndPuck.App.Season;

/// <summary>
/// W16b: when a playoff game ends (MatchEnd), records its result in <see cref="PlayoffSession"/>
/// and settles the other games up to the human's next one; <see cref="AutosaveSystem"/> saves it
/// next. Until W11's screens exist, the bracket shows a few seconds after a playoff game's final,
/// and always in SAP_SCENE=playoffbracket.
/// </summary>
internal sealed class PlayoffSystem : ISystem, IDisposable
{
    /// <summary>Seconds after the final before the stand-in bracket shows.</summary>
    const float BracketDelay = 4;

    Maybe<World> watching;
    bool sawShootout;
    float finishedAt = float.PositiveInfinity;
    Maybe<HudArt> art;

    /// <summary>Gets or sets a value indicating whether the bracket always shows (the playoffbracket scene).</summary>
    public static bool ShowBracket { get; set; }

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        if (PlayoffSession.Current.HasValue) art = HudArt.Load();
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

        if (!PlayoffSession.Playing.TryGet(out var series) || !string.Equals(series.NextHome, world.Setup.Home.Id, StringComparison.Ordinal) || !string.Equals(series.NextAway, world.Setup.Away.Id, StringComparison.Ordinal)) return;
        if (!world.Events.Of<MatchEnded>().Any()) return;

        var m = world.Match;
        var kind = sawShootout ? ResultKind.Shootout : m.Period > world.Setup.Periods ? ResultKind.Overtime : ResultKind.Regulation;
        var result = new FixtureResult(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind);
        if (!PlayoffSession.Current.TryGet(out var playoffs)) return;
        string round = playoffs.Current.Name;
        int game = series.Games.Count + 1;
        if (!PlayoffSession.Finish(result)) return;
        finishedAt = world.Time;
        Console.WriteLine($"SAP: playoff {round} game {game}: {world.Setup.Home.Id} {result.HomeGoals}-{result.AwayGoals} {world.Setup.Away.Id} ({kind}), series {series.HighWins}-{series.LowWins}");
        Log(playoffs);
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !PlayoffSession.Current.TryGet(out var playoffs)) return;
        if (ShowBracket || world.Time - finishedAt >= BracketDelay) PlayoffBracket.Draw(hud, playoffs, SeasonSession.Clubs);
    }

    public void Dispose()
    {
        if (art.TryGet(out var loaded)) loaded.Unload();
        art = Maybe<HudArt>.None;
    }

    /// <summary>Prints where the playoffs stand: the champion, or the human's next game.</summary>
    /// <param name="playoffs">The playoffs.</param>
    public static void Log(PlayoffState playoffs)
    {
        if (playoffs.IsOver) Console.WriteLine($"SAP: {playoffs.Champion.Or("")} are SHL champions");
        else if (playoffs.UserSeries.TryGet(out var s)) Console.WriteLine($"SAP: next playoff game: {playoffs.Current.Name} game {s.Games.Count + 1}, {s.NextHome} vs {s.NextAway} (series {s.High} {s.HighWins}-{s.LowWins} {s.Low})");
    }
}
