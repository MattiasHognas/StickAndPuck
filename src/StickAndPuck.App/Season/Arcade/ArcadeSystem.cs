using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;

namespace StickAndPuck.App.Season;

/// <summary>
/// W16c: when an arcade game ends (MatchEnd), records its result in <see cref="ArcadeSession"/>,
/// which saves the run at once. A few seconds after the final a stand-in panel shows: the run so far
/// and the lives left, or the results summary when the run is over (until W11's results screen).
/// </summary>
internal sealed class ArcadeSystem : ISystem, IDisposable
{
    /// <summary>Seconds after the final before the panel shows.</summary>
    const float PanelDelay = 4;

    World? watching;
    bool sawShootout;
    float finishedAt = float.PositiveInfinity;
    HudArt? art;

    /// <summary>Gets or sets a value indicating whether the panel always shows (the arcadesummary scene).</summary>
    public static bool ShowSummary { get; set; }

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        if (ArcadeSession.Current != null) art = HudArt.Load();
    }

    public void Update(World world, float dt)
    {
        if (!ReferenceEquals(world, watching))
        {
            watching = world;
            sawShootout = false;
            finishedAt = float.PositiveInfinity;
        }
        if (world.Match.Phase == MatchPhase.Shootout) sawShootout = true;

        var playing = ArcadeSession.Playing;
        if (playing == null || !string.Equals(playing.Home.Id, world.Setup.Home.Id, StringComparison.Ordinal) || !string.Equals(playing.Away.Id, world.Setup.Away.Id, StringComparison.Ordinal)) return;
        if (!world.Events.Of<MatchEnded>().Any()) return;

        var m = world.Match;
        var kind = sawShootout ? ResultKind.Shootout : m.Period > world.Setup.Periods ? ResultKind.Overtime : ResultKind.Regulation;
        var game = ArcadeSession.Finish(new FixtureResult(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind));
        if (game == null) return;
        finishedAt = world.Time;
        var run = ArcadeSession.Current!;
        string outcome = run.Champion ? "champions, every club beaten" : run.Lost ? $"run over after {run.Stage} win(s)" : $"{run.LivesLeft} live(s) left, next {run.NextOpponent}";
        Console.WriteLine($"SAP: arcade stage {game.Stage + 1}: {(game.Won ? "won" : "lost")} {game.GoalsFor}-{game.GoalsAgainst} against {game.Opponent} ({kind}); {outcome}; saved to {ArcadeSession.SavePath}");
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (art == null || ArcadeSession.Current is not { } run) return;
        if (ShowSummary || world.Time - finishedAt >= PanelDelay) ArcadeSummary.Draw(art, run, ArcadeSession.Clubs);
    }

    public void Dispose()
    {
        art?.Unload();
        art = null;
    }
}
