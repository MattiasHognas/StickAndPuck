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

    /// <summary>Seconds short of the end of puck handling's one-timer window the receiver lets go of shoot, so a pass
    /// that comes too late is taken, not swung at.</summary>
    const float OneTimerMargin = 0.1f;

    /// <summary>A carrier skating at a defender reads him once as he comes into range: whether to deke, likelier with
    /// better puck control, and how close to let him come first. Hockey IQ sets how well that distance hits the sweet
    /// spot, so a smart skater mostly dekes in time and a poor one too early or too late and loses the puck. Never in
    /// his own zone, never while a shot or pass is under way. True when the deke was pressed this step.</summary>
    bool Deke(World world, Player p, Brain brain)
    {
        if (p.Action != PlayerAction.None || brain.ShootSteps > 0 || p.Speed < 2f) return false;
        int end = PuckHandling.AttackEnd(world, p.Side);
        if (end * world.Puck.Position.X < -RinkSpec.BlueLineX) return false;
        if (!DekeModel.Threat(world, p).TryGet(out var threat)) return false;

        if (brain.DekeFor != threat.Defender.Id || world.Time - brain.DekeRolled > DekeReread)
        {
            brain.DekeFor = threat.Defender.Id;
            brain.DekeRolled = world.Time;
            brain.DekeAt = float.NegativeInfinity;
            var (outcome, iq) = Jitter.Roll(world, p, rng);
            if (rng.NextSingle() < DekeOdds(p.Data.Traits.PuckControl))
            {
                // a fumbled read presses at a weak skater's distance; a lucky one at a star's
                brain.DekeAt = DekeMiddle + PuckHandling.Gauss(rng) * DekeMisjudge(outcome == ReadOutcome.Lucky ? 100 : iq);
            }
        }
        if (threat.Distance > brain.DekeAt) return false;

        brain.DekeAt = float.NegativeInfinity;   // one deke per defender
        AiCommand.Act(world, p, p.Command with { Deke = true }, "deke");
        Decisions.Tally(world, p.Side).Dekes++;
        return true;
    }

    /// <summary>Seconds after reading a defender before the carrier reads the same defender again.</summary>
    const float DekeReread = 2.5f;

    /// <summary>Gets the chance a carrier tries to deke a defender skating at him: 15 % with no puck control, 50 % with full.</summary>
    internal static float DekeOdds(int puckControl)
    {
        return 0.15f + 0.35f * Traits.Unit(puckControl);
    }

    /// <summary>Gets how far off the sweet spot's middle a carrier's deke lands, one standard deviation in metres:
    /// 1.1 m at hockey IQ 0 (as often out of the sweet spot as in it), 0.2 m at 100.</summary>
    internal static float DekeMisjudge(int hockeyIq)
    {
        return Traits.Scale(hockeyIq, 1.1f, 0.2f);
    }

    /// <summary>The teammate an AI pass is meant for reads it once: in a good shooting spot he may one-time it, holding
    /// shoot while it comes so puck handling strikes it on arrival, the stick turned to a corner of the net as it
    /// arrives. Hockey IQ sets how often he sees the chance; a fumbled read misses it. He skates onto the pass's line
    /// either way. True when this skater is the pass's receiver.</summary>
    bool OneTimer(World world, DecisionBoard board, Player p, Brain brain)
    {
        if (!MeetPass(world, board, p) || !board.Pass.TryGet(out var pass)) return false;
        float age = world.Time - pass.At;
        if (!world.Settings.OneTimers || pass.Passer == p.Id || pass.Side != p.Side || age > PuckHandlingSystem.OneTimerWindow - OneTimerMargin) return true;

        int end = PuckHandling.AttackEnd(world, p.Side);
        var blade = Flat(PuckHandling.Blade(p));
        if (!Same(brain.OneTimerRun, pass.At))
        {
            if (p.Action != PlayerAction.None) return true;   // busy: puck handling would take a press as a swing at nothing
            brain.OneTimerRun = pass.At;
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

        // the stick follows the skate onto the pass, then turns to the corner as the puck comes onto the blade
        var pk = world.Puck;
        bool arriving = Vector2.Distance(Flat(pk.Position), blade) < 0.6f + Flat(pk.Velocity).Length() * 0.12f;
        AiCommand.Act(world, p, p.Command with { Shoot = true, Move = arriving ? brain.ShootAim : p.Command.Move, Sprint = !arriving && p.Command.Sprint }, "one-timer");
        return true;
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
