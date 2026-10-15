using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

internal sealed partial class DecisionSystem
{
    /// <summary>The middle of the deke's sweet spot, in metres between the bodies: where a perfect read presses.</summary>
    const float DekeMiddle = (DekeModel.SweetNear + DekeModel.SweetFar) / 2f;

    /// <summary>A one-timer is tried only from a spot whose shot is worth at least this (the carrier's own shot scale).</summary>
    const float OneTimeFrom = 0.3f;

    /// <summary>Seconds the receiver holds shoot to arm a one-timer: a tap, so it is struck low and hard and shoot is
    /// long let go by the time a pass that comes too late is taken rather than one-timed.</summary>
    const float OneTimerTap = 0.1f;

    /// <summary>Steps ahead of the puck reaching the blade that the receiver turns his stick to the net: late enough
    /// that the turn barely moves the blade.</summary>
    const float ArriveSteps = 2f;


    /// <summary>Metres from the pass's line within which the receiver eases off squaring his blade onto it.</summary>
    const float LineArrive = 0.4f;

    /// <summary>A carrier skating at a defender reads him once a possession as he comes at him: whether to deke, likelier with
    /// better puck control, and how close to let him come first. Hockey IQ sets how well that distance hits the sweet
    /// spot, so a smart skater mostly dekes in time and a poor one too early or too late and loses the puck. Never in
    /// his own zone, never while a shot or pass is under way. True when the deke was pressed this step.</summary>
    bool Deke(World world, Player p, Brain brain)
    {
        if (p.Action != PlayerAction.None || brain.ShootSteps > 0 || p.Speed < DekeSpeed) return false;
        int end = PuckHandling.AttackEnd(world, p.Side);
        if (end * world.Puck.Position.X < -RinkSpec.BlueLineX) return false;
        if (!DekeModel.Threat(world, p).TryGet(out var threat) || Closing(p, threat.Defender) < DekeClosing) return false;

        if (brain.DekeRolled < brain.GotAt)
        {
            brain.DekeRolled = world.Time;
            brain.DekeAt = float.NegativeInfinity;
            var (_, iq) = Jitter.Roll(world, p, rng);
            if (rng.NextSingle() < DekeOdds(p.Data.Traits.PuckControl))
            {
                // a fumbled read judges the distance like a weaker skater, a lucky one like a star
                brain.DekeAt = DekeMiddle + PuckHandling.Gauss(rng) * DekeMisjudge(iq);
            }
        }
        if (threat.Distance > brain.DekeAt) return false;

        brain.DekeAt = float.NegativeInfinity;   // one deke per defender
        AiCommand.Act(world, p, p.Command with { Deke = true }, "deke");
        Decisions.Tally(world, p.Side).Dekes++;
        return true;
    }

    /// <summary>Metres per second a defender must be closing on the carrier to be read for a deke: one standing off is skated round, not deked.</summary>
    const float DekeClosing = 2.5f;

    /// <summary>Metres per second a carrier must be skating to deke: one standing still protects the puck instead.</summary>
    const float DekeSpeed = 3f;

    /// <summary>Gets how fast a defender and the carrier come together, in metres per second.</summary>
    static float Closing(Player carrier, Player defender)
    {
        var off = Flat(defender.Position) - Flat(carrier.Position);
        if (off.LengthSquared() < 1e-4f) return 0f;
        return Vector2.Dot(Flat(carrier.Velocity - defender.Velocity), Vector2.Normalize(off));
    }

    /// <summary>Gets the chance a carrier tries to deke a defender skating at him: 5 % with no puck control, 30 % with full.</summary>
    internal static float DekeOdds(int puckControl)
    {
        return 0.05f + 0.25f * Traits.Unit(puckControl);
    }

    /// <summary>Gets how far off the sweet spot's middle a carrier's deke lands, one standard deviation in metres:
    /// 1.1 m at hockey IQ 0 (as often out of the sweet spot as in it), 0.2 m at 100.</summary>
    internal static float DekeMisjudge(int hockeyIq)
    {
        return Traits.Scale(hockeyIq, 1.1f, 0.2f);
    }

    /// <summary>The teammate an AI pass is meant for reads it once: in a good shooting spot he may one-time it, tapping
    /// shoot as it comes so puck handling strikes it on arrival, the stick turned to a corner of the net as it
    /// arrives. Hockey IQ sets how often he sees the chance; a fumbled read misses it. He skates onto the pass's line
    /// either way. True when this skater is the pass's receiver.</summary>
    bool OneTimer(World world, DecisionBoard board, Player p, Brain brain)
    {
        if (!MeetPass(world, board, p) || !board.Pass.TryGet(out var pass)) return false;
        float age = world.Time - pass.At;
        if (!world.Settings.OneTimers || pass.Passer == p.Id || pass.Side != p.Side || age > PuckHandlingSystem.OneTimerWindow) return true;

        int end = PuckHandling.AttackEnd(world, p.Side);
        var blade = Flat(PuckHandling.Blade(p));
        var pk = world.Puck;
        float closing = Flat(pk.Velocity - p.Velocity).Length();
        float gap = Vector2.Distance(Flat(pk.Position), blade) - PuckHandling.Reach(p.Data.Traits);
        if (!Same(brain.OneTimerRun, pass.At))
        {
            // busy, or the pass all but on the blade: a press now would be a swing at nothing, or a shot charged on a taken pass
            if (p.Action != PlayerAction.None || gap < closing * (OneTimerTap + (ArriveSteps * Game.Step))) return true;
            brain.OneTimerRun = pass.At;
            brain.OneTimerPressed = world.Time;
            brain.OneTiming = false;
            var (outcome, iq) = Jitter.Roll(world, p, rng);
            var blockers = world.Players.Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie).Select(o => Flat(o.Position));
            float worth = Choices.ShotQuality(end, blade) * (0.75f + 0.5f * Traits.Unit(p.Data.Traits.ShotAccuracy)) * Choices.ShotLane(end, blade, blockers);
            if (outcome == ReadOutcome.Fumble || worth < OneTimeFrom || rng.NextSingle() >= OneTimerOdds(iq)) return true;
            brain.OneTiming = true;
            brain.ShootAim = OneTimerAim(world, p, end, blade);
            Decisions.Tally(world, p.Side).OneTimers++;
        }
        if (!brain.OneTiming) return true;

        // the blade goes square onto the pass's line, then the stick turns to the corner the step the puck comes onto it
        bool arriving = gap < closing * ArriveSteps * Game.Step;
        var move = arriving ? brain.ShootAim : Steering.Toward(p, Flat(p.Position) + OntoLine(pk, blade) - blade, LineArrive).Move;
        bool hold = world.Time - brain.OneTimerPressed < OneTimerTap;
        AiCommand.Act(world, p, p.Command with { Shoot = hold, Move = move, Sprint = false }, "one-timer");
        return true;
    }

    /// <summary>Gets the point on the puck's line nearest the blade, at least half a metre ahead of the puck.</summary>
    static Vector2 OntoLine(Puck pk, Vector2 blade)
    {
        var from = Flat(pk.Position);
        var v = Flat(pk.Velocity);
        if (v.Length() < 0.1f) return from;
        var dir = Vector2.Normalize(v);
        return from + dir * Math.Max(0.5f, Vector2.Dot(blade - from, dir));
    }

    /// <summary>Gets the chance a receiver in a good spot sees the one-timer: 25 % at hockey IQ 0, 85 % at 100.</summary>
    internal static float OneTimerOdds(int hockeyIq)
    {
        return Traits.Scale(hockeyIq, 0.25f, 0.85f);
    }

    /// <summary>The stick for a one-timer: toward the side of the net the goalie leaves open, within puck handling's aim cone, else straight at the net.</summary>
    static Vector2 OneTimerAim(World world, Player p, int end, Vector2 at)
    {
        var toNet = new Vector2(end * RinkSpec.GoalLineX, 0) - at;
        var goalie = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).FirstOrNone(o => o.IsGoalie);
        int side = Choices.ShotSide(at, goalie.Select(g => g.Position.Z));
        var aim = Vector2.Normalize(new Vector2(end * 0.8f, side * 0.6f));
        return Vector2.Dot(aim, Vector2.Normalize(toNet)) < SimMath.Cos(70f * MathF.PI / 180f) ? Vector2.Normalize(toNet) : aim;
    }
}
