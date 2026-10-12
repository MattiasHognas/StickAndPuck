using StickAndPuck.Camera;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Rules;

/// <summary>
/// W7a: faceoffs, the clock, three periods, goals and restarts. Owns <see cref="World.Match"/> and
/// publishes Faceoff, PeriodEnd and MatchEnd; counts the Goal events physics publishes. Runs only
/// in a world started with <see cref="MatchFlow.Begin"/>. See <see cref="MatchFlow"/> for the rules it keeps.
/// </summary>
internal sealed class MatchRules : ISystem
{
    /// <summary>Gets or sets a value indicating whether the plain score and clock line is drawn; SAP_MATCH_DEBUG=1 turns it on.</summary>
    public static bool ShowLine { get; set; } = string.Equals(Environment.GetEnvironmentVariable("SAP_MATCH_DEBUG"), "1", StringComparison.Ordinal);

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        MatchDemo.Update(world);
        var s = MatchFlow.State(world);
        var m = world.Match;
        // W40: from the horn the celebration poses every player, then hands over to the final
        if (Celebration.Update(world, s, dt)) return;
        switch (m.Phase)
        {
            case MatchPhase.Intro:
                if (Wait(s, dt)) MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
                break;
            case MatchPhase.Faceoff:
                // players hold their spots until the puck drops
                FaceoffFormation.Place(world, s.Dot);
                if (!s.RefereeDrops && Wait(s, dt)) MatchFlow.Drop(world);
                break;
            case MatchPhase.Play:
                Play(world, s, dt);
                break;
            case MatchPhase.Stopped:
                if (Wait(s, dt)) MatchFlow.SetUpFaceoff(world, s.Dot);
                break;
            case MatchPhase.Goal:
                // W9's goal replay holds the restart; the faceoff follows a second after it ends
                if (Replay.Playing) s.Timer = 1f;
                else if (Wait(s, dt)) MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
                break;
            case MatchPhase.Intermission:
                if (Wait(s, dt)) MatchFlow.StartPeriod(world, m.Period + 1, world.Setup.PeriodSeconds);
                break;
            default:
                // Overtime and Shootout belong to W7c until it calls StartPeriod; Final is the end
                break;
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        // a plain readout for checking the flow before W10's scorebug; SAP_MATCH_DEBUG=1 turns it on
        if (!ShowLine || !MatchFlow.IsRunning(world)) return;
        var m = world.Match;
        string text = $"{world.Setup.Home.ShortName} {m.Score[0]} - {m.Score[1]} {world.Setup.Away.ShortName}   P{m.Period} {MatchFlow.ClockText(m.Clock)}   {m.Phase}";
        DrawRectangle(4, 252, MeasureText(text, 10) + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        DrawText(text, 8, 254, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    static bool Wait(FlowState s, float dt)
    {
        s.Timer -= dt;
        return s.Timer <= 0;
    }

    static void Play(World world, FlowState s, float dt)
    {
        var m = world.Match;
        // goals first: physics ran earlier this step, so a puck over the line before the clock ran out counts
        foreach (var save in world.Events.Of(MatchEventKind.Save))
            if (save.Side is { } goalieSide) m.Shots[(int)Other(goalieSide)]++;
        if (world.Events.Of(MatchEventKind.Goal).FirstOrDefault() is { Side: { } scorer } goal)
        {
            m.Score[(int)scorer]++;
            m.Shots[(int)scorer]++;
            m.Phase = MatchPhase.Goal;
            world.Puck.Live = false;
            s.Timer = MatchTuning.GoalSeconds;
            Log(world, s, $"goal {world.Setup.Club(scorer).ShortName} by #{Number(world, goal.PlayerId)}");
            // sudden death after regulation
            if (m.Period > world.Setup.Periods) EndMatch(world, s);
            return;
        }

        if (Frozen(world, s, dt)) return;

        m.Clock -= dt;
        if (m.Clock > 0) return;
        m.Clock = 0;
        EndPeriod(world, s);
    }

    /// <summary>A goalie holding the puck long enough freezes it: whistle, faceoff at the nearest end-zone dot.</summary>
    static bool Frozen(World world, FlowState s, float dt)
    {
        var pk = world.Puck;
        bool held = pk.Carrier >= 0 && pk.Carrier < world.Players.Count && world.Players[pk.Carrier].IsGoalie;
        s.Held = held ? s.Held + dt : 0;
        if (s.Held < MatchTuning.FreezeSeconds) return false;
        s.Held = 0;
        return MatchFlow.Whistle(world, MatchFlow.NearestDot(pk.Position));
    }

    static void EndPeriod(World world, FlowState s)
    {
        var m = world.Match;
        world.Puck.Live = false;
        world.Events.Publish(new MatchEvent(MatchEventKind.PeriodEnd, world.Time, Strength: m.Period));
        Log(world, s, $"end of period {m.Period}");
        bool tied = m.Score[0] == m.Score[1];
        if (m.Period < world.Setup.Periods)
        {
            m.Phase = MatchPhase.Intermission;
            s.Timer = MatchTuning.IntermissionSeconds;
        }
        else if (tied && OvertimeFormat.Of(world.Setup).AfterTie(m.Period - world.Setup.Periods) is { } next)
        {
            // a tie: W7c sets up the overtime skaters and calls StartPeriod, or runs the shootout, as the mode's format says
            m.Phase = next;
        }
        else
        {
            EndMatch(world, s);
        }
    }

    /// <summary>The horn: MatchEnd with the winner, then the celebration (W40) holds the final for a while, or the final at once after a tie.</summary>
    internal static void EndMatch(World world, FlowState s)
    {
        var m = world.Match;
        world.Puck.Live = false;
        Side? winner = m.Score[0] > m.Score[1] ? Side.Home : m.Score[1] > m.Score[0] ? Side.Away : null;
        m.Phase = Celebration.Begin(world, s, winner) ? MatchPhase.Stopped : MatchPhase.Final;
        world.Events.Publish(new MatchEvent(MatchEventKind.MatchEnd, world.Time, winner));
        Log(world, s, "final");
    }

    static void Log(World world, FlowState s, string what)
    {
        if (!s.Log) return;
        var m = world.Match;
        Console.WriteLine($"SAP: {what}: {world.Setup.Home.ShortName} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.ShortName}, P{m.Period} {MatchFlow.ClockText(m.Clock)}");
    }

    static string Number(World world, int playerId)
    {
        return playerId >= 0 && playerId < world.Players.Count ? world.Players[playerId].Data.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }
}
