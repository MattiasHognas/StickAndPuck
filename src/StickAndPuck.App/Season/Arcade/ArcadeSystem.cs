using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;

namespace StickAndPuck.App.Season;

/// <summary>When an arcade game ends (MatchEnd), records its result in <see cref="ArcadeSession"/>,
/// which saves the run at once. A few seconds after the final a stand-in panel shows: the run so far
/// and the lives left, or the results summary when the run is over (without the results screen).</summary>
internal sealed class ArcadeSystem : ISystem, IDisposable
{
    /// <summary>Seconds after the final before the panel shows.</summary>
    const float PanelDelay = 4;

    Maybe<World> watching;
    bool sawShootout;
    float finishedAt = float.PositiveInfinity;
    Maybe<HudArt> art;


    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        if (world.App.Arcade.Current.HasValue) art = HudArt.Load();
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

        if (!world.App.Arcade.Playing.TryGet(out var playing) || !string.Equals(playing.Home.Id, world.Setup.Home.Id, StringComparison.Ordinal) || !string.Equals(playing.Away.Id, world.Setup.Away.Id, StringComparison.Ordinal)) return;
        if (!world.Events.Of<MatchEnded>().Any()) return;

        var m = world.Match;
        var kind = sawShootout ? ResultKind.Shootout : m.Period > world.Setup.Periods ? ResultKind.Overtime : ResultKind.Regulation;
        if (!world.App.Arcade.Finish(new FixtureResult(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind)).TryGet(out var game) || !world.App.Arcade.Current.TryGet(out var run)) return;
        finishedAt = world.Time;
        string outcome = run.Champion ? "champions, every club beaten" : run.Lost ? $"run over after {run.Stage} win(s)" : $"{run.LivesLeft} live(s) left, next {run.NextOpponent.Or("")}";
        Console.WriteLine($"SAP: arcade stage {game.Stage + 1}: {(game.Won ? "won" : "lost")} {game.GoalsFor}-{game.GoalsAgainst} against {game.Opponent} ({kind}); {outcome}; saved to {world.App.Arcade.SavePath}");
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !world.App.Arcade.Current.TryGet(out var run)) return;
        if (world.Switches.ArcadeSummary || world.Time - finishedAt >= PanelDelay) ArcadeSummary.Draw(hud, run, world.App.Arcade.Clubs);
    }

    public void Dispose()
    {
        if (art.TryGet(out var loaded)) loaded.Unload();
        art = Maybe<HudArt>.None;
    }
}
