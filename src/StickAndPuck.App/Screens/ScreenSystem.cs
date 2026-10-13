using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>Start screen, settings, team select, pause, results and standings. Owns
/// <see cref="MatchState.Paused"/>. Runs only when the start scene is picked (the default), so
/// every other SAP_SCENE behaves as before. In a match it pauses on escape, P or start, notes the
/// goals, and a few seconds after the final whistle hands the result to the results screen.</summary>
internal sealed class ScreenSystem : ISystem, IDisposable
{
    /// <summary>Seconds the FINAL banner shows before the results screen.</summary>
    const float ResultsDelay = 3;

    readonly MenuInput input = new();
    readonly List<GoalLine> goals = [];
    readonly int[] score = new int[2];
    Maybe<ScreenArt> art;
    Maybe<Screen> lastTop;
    bool sawShootout;
    float finalAt = float.PositiveInfinity;

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        if (!ScreenFlow.Active) return;
        SetExitKey(KeyboardKey.Null);    // escape pauses and goes back; Quit on the start screen ends the game
        art = ScreenArt.Load();
        input.Swallow();
    }

    public void Update(World world, float dt)
    {
        if (!ScreenFlow.Active) return;
        var top = ScreenFlow.Top;
        bool same = top.TryGet(out var shown) ? lastTop.Is(last => ReferenceEquals(last, shown)) : !lastTop.HasValue;
        if (!same) input.Swallow();    // a button still held from the last screen does not press here
        lastTop = top;

        bool inMatch = ScreenFlow.Playing.HasValue && MatchFlow.IsRunning(world);
        world.Match.Paused = !inMatch || top.HasValue;
        var press = input.Next(top.HasValue, dt);
        if (top.TryGet(out var screen))
        {
            screen.Update(world, press);
            if (!ScreenFlow.Top.HasValue && inMatch) Resumed(world);
            return;
        }
        if (!inMatch) return;

        Watch(world);
        // The replay flag is shared, so the screens never leave a match or pause while a replay
        // plays (a winning goal's replay plays out first); a stale flag would hold the next faceoff
        if (world.ReplayPlaying) return;
        if (world.Match.Phase == MatchPhase.Final)
        {
            if (float.IsPositiveInfinity(finalAt)) finalAt = world.Time;
            if (world.Time - finalAt >= ResultsDelay) ScreenFlow.ShowResults(Result(world));
        }
        else if (press.Contains(MenuButton.Pause))
        {
            Console.WriteLine("SAP: screens: paused");
            ScreenFlow.Push(new PauseScreen());
            world.Match.Paused = true;
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var loaded) || !ScreenFlow.Top.TryGet(out var top)) return;
        if (top.Dims) ScreenArt.Dim();
        top.Draw(loaded, world);
    }

    public void Dispose()
    {
        if (art.TryGet(out var loaded)) loaded.Unload();
        art = Maybe<ScreenArt>.None;
    }

    /// <summary>Back to play: nobody's buttons from the pause screen carry into the match.</summary>
    void Resumed(World world)
    {
        Console.WriteLine("SAP: screens: resumed");
        world.Match.Paused = false;
        foreach (var p in world.Players) p.Command = default;
    }

    /// <summary>Notes the goals for the results screen. This system runs first in each step, before that
    /// step's events exist and after the last step's are cleared, so it watches the score instead:
    /// a goal is in when the score went up, and the puck's last touch is still the scorer's.</summary>
    void Watch(World world)
    {
        var m = world.Match;
        if (m.Phase == MatchPhase.Shootout) sawShootout = true;
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int i = (int)side;
            if (m.Score[i] <= score[i]) continue;
            score[i] = m.Score[i];
            if (sawShootout) continue;    // the shootout winner's goal is added at the end
            string scorer = world.Setup.Club(side).Name;
            int touch = world.Puck.LastTouch;
            if (touch >= 0 && touch < world.Players.Count && world.Players[touch] is { } p && p.Side == side)
                scorer = Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName);
            float elapsed = (m.Period > world.Setup.Periods ? OvertimeRules.PeriodLength(world) : world.Setup.PeriodSeconds) - m.Clock;
            goals.Add(new GoalLine(side, PeriodLabel(world), MatchFlow.ClockText(Math.Max(0, elapsed)), scorer));
        }
    }

    /// <summary>The period a goal went in: "1" to "3", "OT", then "2OT", "3OT" in a long playoff sudden death.</summary>
    static string PeriodLabel(World world)
    {
        int overtime = world.Match.Period - world.Setup.Periods;
        return overtime <= 1 ? HudSystem.PeriodText(world) : Text.Format(K.OvertimeNumber, overtime);
    }

    MatchResult Result(World world)
    {
        var m = world.Match;
        var lines = new List<GoalLine>(goals);
        string decided = "";
        if (sawShootout)
        {
            decided = Text.Get(K.Shootout);
            var winner = m.Score[(int)Side.Home] > m.Score[(int)Side.Away] ? Side.Home : Side.Away;
            lines.Add(new GoalLine(winner, Text.Get(K.PeriodSo), "", Text.Get(K.ShootoutWinner)));
        }
        else if (m.Period > world.Setup.Periods)
        {
            decided = PeriodLabel(world);
        }
        return new MatchResult(world.Setup, m.Score[(int)Side.Home], m.Score[(int)Side.Away], m.Shots[(int)Side.Home], m.Shots[(int)Side.Away],
            decided, lines, ScreenFlow.Playing.TryGet(out var plan) ? plan : throw new InvalidOperationException("the results of a match that was not started"));
    }
}
