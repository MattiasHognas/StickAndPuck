using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>
/// The match flow other systems call into: start a match, blow the whistle for a faceoff at a dot
/// (W7b offside and icing), start an overtime period (W7c), and let the referee drop the puck (W8).
/// <see cref="MatchRules"/> runs the flow each step. A world that never calls <see cref="Begin"/>
/// (practice and test scenes) has no match flow: no clock, no faceoffs.
/// </summary>
/// <remarks>
/// What the flow promises the readers of <see cref="World.Match"/>:
/// the clock runs only in <see cref="MatchPhase.Play"/>; during <see cref="MatchPhase.Faceoff"/>
/// the puck lies dead on <see cref="FaceoffDot"/> and the players hold their spots;
/// a goal (published by physics) is counted once and followed by a centre faceoff;
/// each period end publishes PeriodEnd; after regulation a tie hands over to W7c by setting
/// <see cref="MatchPhase.Overtime"/> or <see cref="MatchPhase.Shootout"/>; otherwise MatchEnd is
/// published once with the winner as Side (null for a tie) and the phase becomes Final.
/// A goal in a period after regulation is sudden death.
/// </remarks>
public static class MatchFlow
{
    static readonly ConditionalWeakTable<World, FlowState> States = new();

    /// <summary>Starts the match: score to zero, period 1, the intro, then the opening faceoff at centre.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="log">Whether to print goals and period ends to the console.</param>
    public static void Begin(World world, bool log = true)
    {
        var s = State(world);
        s.Active = true;
        s.Log = log;
        var m = world.Match;
        m.Score[0] = m.Score[1] = 0;
        m.Shots[0] = m.Shots[1] = 0;
        m.Period = 1;
        m.Clock = world.Setup.PeriodSeconds;
        m.Phase = MatchPhase.Intro;
        s.Timer = MatchTuning.IntroSeconds;
        s.Dot = RinkSpec.CentreDot;
        StopPuck(world, RinkSpec.CentreDot);
    }

    /// <summary>Gets a value indicating whether the world runs the match flow.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>True after <see cref="Begin"/>.</returns>
    public static bool IsRunning(World world)
    {
        return States.TryGetValue(world, out var s) && s.Active;
    }

    /// <summary>Gets the dot of the coming (or last) faceoff; the referee skates here during a stoppage.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The dot.</returns>
    public static Vector3 FaceoffDot(World world)
    {
        return State(world).Dot;
    }

    /// <summary>
    /// Stops play for a faceoff at a dot: puck dead, clock stopped, a Whistle event, then the faceoff
    /// after <see cref="MatchTuning.StoppageSeconds"/>. Publish your own Offside or Icing event too.
    /// </summary>
    /// <param name="world">The match state.</param>
    /// <param name="dot">Where the faceoff will be; see <see cref="NearestDot"/>.</param>
    /// <returns>False if play was not running, so nothing changed.</returns>
    public static bool Whistle(World world, Vector3 dot)
    {
        if (!IsRunning(world) || world.Match.Phase != MatchPhase.Play) return false;
        var s = State(world);
        world.Match.Phase = MatchPhase.Stopped;
        world.Puck.Live = false;
        s.Timer = MatchTuning.StoppageSeconds;
        s.Dot = dot;
        world.Events.Publish(new MatchEvent(MatchEventKind.Whistle, world.Time, Position: world.Puck.Position));
        return true;
    }

    /// <summary>Starts a period after regulation (W7c's 3 on 3 overtime): clock set, faceoff at centre. A goal in it ends the match.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="period">The period number, 4 for overtime.</param>
    /// <param name="seconds">Its length, usually <see cref="MatchTuning.OvertimeSeconds"/>.</param>
    public static void StartPeriod(World world, int period, float seconds)
    {
        world.Match.Period = period;
        world.Match.Clock = seconds;
        SetUpFaceoff(world, RinkSpec.CentreDot);
    }

    /// <summary>Hands puck drops to the referee (W8): faceoffs wait for <see cref="Drop"/> instead of a timer.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="referee">True when the referee drops the puck.</param>
    public static void SetRefereeDrops(World world, bool referee)
    {
        State(world).RefereeDrops = referee;
    }

    /// <summary>Drops the puck if a faceoff is waiting; the referee calls this.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>True if the puck was dropped.</returns>
    public static bool Drop(World world)
    {
        if (!IsRunning(world) || world.Match.Phase != MatchPhase.Faceoff) return false;
        var s = State(world);
        world.Match.Phase = MatchPhase.Play;
        world.Puck.Position = s.Dot;
        world.Puck.Velocity = Vector3.Zero;
        world.Puck.Live = true;
        Draw(world, s);
        world.Events.Publish(new MatchEvent(MatchEventKind.Faceoff, world.Time, Position: s.Dot));
        return true;
    }

    /// <summary>
    /// The draw: the two skaters nearest the dot, one per club, contest it on puck control and hockey
    /// IQ with some luck, and the puck is pulled back towards the winner's blade. Without it the puck
    /// lay still on the dot and the quicker centre to skate onto it won every draw.
    /// </summary>
    static void Draw(World world, FlowState s)
    {
        Player? Centre(Side side) => world.OnIce(side).Where(p => !p.IsGoalie).MinBy(p => Vector3.DistanceSquared(p.Position, s.Dot));
        if (Centre(Side.Home) is not { } home || Centre(Side.Away) is not { } away) return;
        static float Skill(Player p) => 0.5f + Traits.Unit(p.Data.Traits.PuckControl) + 0.5f * p.Data.Traits.Iq;
        float h = Skill(home), a = Skill(away);
        var winner = s.Dice.NextSingle() * (h + a) < h ? home : away;
        var pull = winner.Position + (winner.Position - s.Dot) * 0.5f - s.Dot;
        pull.Y = 0;
        if (pull.LengthSquared() > 1e-6f) world.Puck.Velocity = Vector3.Normalize(pull) * MatchTuning.DrawSpeed;
    }

    /// <summary>Gets the faceoff dot nearest a point on the ice.</summary>
    /// <param name="position">A point, for example where the puck was when play stopped.</param>
    /// <returns>The nearest of the nine dots.</returns>
    public static Vector3 NearestDot(Vector3 position)
    {
        return RinkSpec.FaceoffDots.MinBy(d => Vector2.DistanceSquared(new(d.X, d.Z), new(position.X, position.Z)));
    }

    /// <summary>Formats game seconds as m:ss, rounding up so the clock shows 0:00 only when time is out.</summary>
    /// <param name="seconds">Seconds left.</param>
    /// <returns>The clock text.</returns>
    public static string ClockText(float seconds)
    {
        int s = (int)MathF.Ceiling(Math.Max(0, seconds) - 0.0001f);
        return $"{s / 60}:{s % 60:00}";
    }

    internal static FlowState State(World world)
    {
        return States.GetOrCreateValue(world);
    }

    internal static void SetUpFaceoff(World world, Vector3 dot)
    {
        var s = State(world);
        s.Dot = dot;
        s.Timer = MatchTuning.FaceoffSeconds;
        world.Match.Phase = MatchPhase.Faceoff;
        StopPuck(world, dot);
        FaceoffFormation.Place(world, dot);
    }

    static void StopPuck(World world, Vector3 dot)
    {
        var pk = world.Puck;
        pk.Live = false;
        pk.Carrier = -1;
        pk.LastTouch = -1;
        pk.Position = dot with { Y = 0 };
        pk.Velocity = Vector3.Zero;
    }
}

/// <summary>The flow's own state for one world.</summary>
internal sealed class FlowState
{
    public bool Active { get; set; }

    public bool Log { get; set; }

    /// <summary>Gets or sets the real seconds left in the current pause (intro, faceoff, goal, stoppage, intermission).</summary>
    public float Timer { get; set; }

    public Vector3 Dot { get; set; }

    public bool RefereeDrops { get; set; }

    /// <summary>Gets the luck in the draws; seeded so test runs repeat.</summary>
#pragma warning disable S2245 // gameplay dice, not security
    public Random Dice { get; } = new(43);
#pragma warning restore S2245

    /// <summary>Gets or sets how long a goalie has held the puck in play.</summary>
    public float Held { get; set; }
}
