using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>
/// W7b: hybrid icing as the SHL plays it. A club that sends the puck from its own half of the red
/// line over the opponents' goal line, untouched on the way and not into the net, ices it, unless
/// it is short-handed. The race decides: when the puck crosses the goal line, the club whose
/// nearest skater is closer to the end-zone dot on the puck's side wins it; the defending club wins
/// a tie. An attacking win waves icing off. A call publishes Icing (Side = offending club) and
/// whistles a faceoff at the offending club's own end-zone dot on the side the puck was sent from.
/// W44: <see cref="RuleSet.Icing"/> switches it: Hybrid as above, Touch (no race: the icing waits
/// for the next touch and stands when a defender plays the puck first, an attacker waves it off)
/// or Off. W49: under <see cref="RuleSet.IcingNoChange"/> the icing club keeps the skaters it had
/// on the ice until the faceoff after the call (<see cref="HoldsLines"/>; W13's lines read it).
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

    /// <summary>Gets whether a club iced the puck and may not change lines until the puck is dropped (W49).</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>True from the icing whistle until play resumes.</returns>
    public static bool HoldsLines(World world, Side side)
    {
        return States.TryGetValue(world, out var s) && s.Held[(int)side];
    }

    /// <summary>Holds a club's lines as an icing call under <see cref="RuleSet.IcingNoChange"/> does; for the test scenes.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The icing club.</param>
    internal static void HoldLines(World world, Side side)
    {
        States.GetOrCreateValue(world).Held[(int)side] = true;
    }

    public void Update(World world, float dt)
    {
        var s = States.GetOrCreateValue(world);
        var pk = world.Puck;
        if (world.Match.Phase == MatchPhase.Play) Array.Clear(s.Held);   // the puck is down: the hold is over
        var call = world.Setup.Rules.Icing;
        if (!MatchFlow.IsRunning(world) || world.Match.Phase != MatchPhase.Play || !pk.Live || call == IcingCall.Off)
        {
            s.Touch = -1;
            s.Over = false;
            s.Iced = -1;
            return;
        }
        if (s.Iced >= 0 && Touched(world, s)) return;

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
        if (call == IcingCall.Touch)
        {
            s.Iced = s.Touch;    // touch icing: the next player on the puck decides
            s.IcedFrom = s.From;
            return;
        }
        if (!DefenceWinsRace(world, offender, end, pk.Position)) return;
        Call(world, offender, s.Touch, s.From);
    }

    /// <summary>Touch icing waiting on the next touch: a defender on the puck calls it, an attacker waves it off.</summary>
    /// <returns>True while it still waits (nothing else to do this step).</returns>
    static bool Touched(World world, IcingState s)
    {
        var pk = world.Puck;
        int by = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
        if (by == s.Iced || by < 0 || by >= world.Players.Count) return true;
        int iced = s.Iced;
        s.Iced = -1;
        var offender = world.Players[iced].Side;
        if (world.Players[by].Side != offender) Call(world, offender, iced, s.IcedFrom);
        return true;
    }

    static void Call(World world, Side offender, int touch, Vector3 from)
    {
        var dot = FaceoffDot(world.Match, offender, from);
        if (!MatchFlow.Whistle(world, dot)) return;
        world.Events.Publish(new IcingCalled(world.Time, offender, touch, world.Puck.Position));
        if (world.Setup.Rules.IcingNoChange) HoldLines(world, offender);
        LineCallsDemo.Log("Icing", offender, world.Puck.Position, dot);
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

    /// <summary>Gets or sets the player whose touch icing waits on the next touch, or -1.</summary>
    public int Iced { get; set; } = -1;

    /// <summary>Gets or sets where that player sent the puck from.</summary>
    public Vector3 IcedFrom { get; set; }

    /// <summary>Gets, per side, whether the club iced the puck and keeps its skaters until the drop.</summary>
    public bool[] Held { get; } = new bool[2];

}
