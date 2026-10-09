using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Rules;

/// <summary>
/// W7b: hybrid icing as the SHL plays it. A club that sends the puck from its own half of the red
/// line over the opponents' goal line, untouched on the way and not into the net, ices it, unless
/// it is short-handed. The race decides: when the puck crosses the goal line, the club whose
/// nearest skater is closer to the end-zone dot on the puck's side wins it; the defending club wins
/// a tie. An attacking win waves icing off. A call publishes Icing (Side = offending club) and
/// whistles a faceoff at the offending club's own end-zone dot on the side the puck was sent from.
/// </summary>
internal sealed class IcingRule : ISystem
{
    /// <summary>The x of the end-zone dots.</summary>
    public const float EndDotX = 20.7f;

    static readonly ConditionalWeakTable<World, IcingState> States = new();

    /// <summary>Gets the faceoff dot after icing: the offending club's own end-zone dot on the side the puck was sent from.</summary>
    /// <param name="match">The match state.</param>
    /// <param name="offender">The club that iced the puck.</param>
    /// <param name="sentFrom">Where the puck was last played.</param>
    /// <returns>The dot.</returns>
    public static Vector3 FaceoffDot(MatchState match, Side offender, Vector3 sentFrom)
    {
        int own = FaceoffFormation.DefendedEnd(match, offender);
        return MatchFlow.NearestDot(new Vector3(own * EndDotX, 0, sentFrom.Z >= 0 ? 1 : -1));
    }

    public void Update(World world, float dt)
    {
        var s = States.GetOrCreateValue(world);
        s.Shown = Math.Max(0, s.Shown - dt);
        var pk = world.Puck;
        if (!MatchFlow.IsRunning(world) || world.Match.Phase != MatchPhase.Play || !pk.Live)
        {
            s.Touch = -1;
            s.Over = false;
            return;
        }

        // the last place the puck was played: every step it is carried, or the step it changes sticks or bodies
        if (pk.Carrier >= 0 || pk.LastTouch != s.Touch)
        {
            s.Touch = pk.LastTouch;
            s.From = pk.Position;
        }

        bool over = Math.Abs(pk.Position.X) > RinkSpec.GoalLineX + RinkSpec.PuckRadius + 0.025f;
        bool crossed = over && !s.Over;
        s.Over = over;
        if (!crossed || pk.Carrier >= 0 || s.Touch < 0 || s.Touch >= world.Players.Count) return;

        var offender = world.Players[s.Touch].Side;
        int end = Math.Sign(pk.Position.X);
        int own = FaceoffFormation.DefendedEnd(world.Match, offender);
        // over the opponents' goal line, sent from the club's own side of the red line
        if (end == own || own * s.From.X <= 0) return;
        if (ShortHanded(world.Match, offender)) return;
        if (!DefenceWinsRace(world, offender, end, pk.Position)) return;

        var dot = FaceoffDot(world.Match, offender, s.From);
        if (!MatchFlow.Whistle(world, dot)) return;
        var call = new MatchEvent(MatchEventKind.Icing, world.Time, offender, s.Touch, pk.Position);
        world.Events.Publish(call);
        LineCallsDemo.Log(call, dot);
        s.Shown = MatchTuning.StoppageSeconds;
    }

    public void DrawHud(World world, RenderContext render)
    {
        // a plain label until W10's HUD messages; shown with the debug line
        if (!MatchRules.ShowLine || !States.TryGetValue(world, out var s) || s.Shown <= 0) return;
        DrawRectangle(4, 236, MeasureText("ICING", 10) + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        DrawText("ICING", 8, 238, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    /// <summary>Gets a value indicating whether a club has fewer skaters by penalty, so it may ice the puck.</summary>
    static bool ShortHanded(MatchState match, Side side)
    {
        int own = match.Penalties.Count(p => p.Side == side && p.SecondsLeft > 0);
        int other = match.Penalties.Count(p => p.Side != side && p.SecondsLeft > 0);
        return own > other;
    }

    /// <summary>The hybrid race to the end-zone dot on the puck's side: true when a defender is at least as close as every attacker.</summary>
    static bool DefenceWinsRace(World world, Side offender, int end, Vector3 puck)
    {
        var dot = new Vector2(end * EndDotX, puck.Z >= 0 ? 6.7f : -6.7f);
        float Nearest(Side side) => world.OnIce(side).Where(p => !p.IsGoalie)
            .Select(p => Vector2.Distance(new Vector2(p.Position.X, p.Position.Z), dot))
            .DefaultIfEmpty(float.PositiveInfinity).Min();
        var defender = offender == Side.Home ? Side.Away : Side.Home;
        return Nearest(defender) <= Nearest(offender);
    }
}

/// <summary>Icing's own state for one world.</summary>
internal sealed class IcingState
{
    /// <summary>Gets or sets the last player who played the puck, or -1.</summary>
    public int Touch { get; set; } = -1;

    /// <summary>Gets or sets where that player last played it.</summary>
    public Vector3 From { get; set; }

    /// <summary>Gets or sets a value indicating whether the puck was past a goal line last step.</summary>
    public bool Over { get; set; }

    /// <summary>Gets or sets the real seconds the call label stays up.</summary>
    public float Shown { get; set; }
}
