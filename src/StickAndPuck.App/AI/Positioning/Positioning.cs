using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.AI.Dev;

namespace StickAndPuck.App.AI;

/// <summary>What positioning decided this step, per skater, and the way for decisions to ask
/// for a spot. <see cref="PositioningSystem"/> runs before <see cref="DecisionSystem"/> each step:
/// <list type="number">
/// <item>Positioning gives every skater a <see cref="Role"/>, a <see cref="Job"/> and a target, and
/// writes Move and Sprint into each AI skater's command to take it there.</item>
/// <item>Decisions read <see cref="JobOf"/>, <see cref="TargetOf"/> and so on, then add Pass, Shoot
/// or Check to the command. To send a skater elsewhere they either rewrite Move right away with
/// <see cref="Steering.Toward"/>, or call <see cref="Request"/>, which positioning follows from the
/// next step for as long as the request is renewed every step.</item>
/// </list></summary>
public static class Positioning
{

    /// <summary>Gets a skater's role.</summary>
    public static Role RoleOf(World world, Player p)
    {
        return Board(world).Get(p.Id).TryGet(out var plan) ? plan.Role : Role.Centre;
    }

    /// <summary>Gets what a skater is doing this step.</summary>
    public static Job JobOf(World world, Player p)
    {
        return Board(world).Get(p.Id).TryGet(out var plan) ? plan.Job : Job.Idle;
    }

    /// <summary>Gets where a skater is heading this step, after the hockey IQ read and spacing.</summary>
    public static Vector2 TargetOf(World world, Player p)
    {
        return Board(world).Get(p.Id).TryGet(out var plan) ? plan.Target : new Vector2(p.Position.X, p.Position.Z);
    }

    /// <summary>Gets the ideal spot for a skater's role this step, from where the puck is now, before hockey IQ reads or blurs it.</summary>
    public static Vector2 IdealOf(World world, Player p)
    {
        return Board(world).Get(p.Id).TryGet(out var plan) ? plan.Ideal : new Vector2(p.Position.X, p.Position.Z);
    }

    /// <summary>Gets a skater's station in his side's attacking-zone cycle.</summary>
    public static Maybe<Station> StationOf(World world, Player p)
    {
        return Board(world).Get(p.Id).TryGet(out var plan) ? plan.Station : Maybe<Station>.None;
    }

    /// <summary>Gets whether a side is cycling the puck in the attacking zone this step.</summary>
    public static bool Cycling(World world, Side side)
    {
        return Board(world).Cycle[(int)side].Active;
    }

    /// <summary>Gets what a side's cycle came to this match.</summary>
    public static CycleTally CycleOf(World world, Side side)
    {
        return Board(world).Cycle[(int)side].Tally;
    }

    /// <summary>Gets whether a side is attacking or defending this step.</summary>
    public static TeamPhase PhaseOf(World world, Side side)
    {
        return Board(world).Phase[(int)side];
    }

    /// <summary>Asks positioning to send an AI skater to a spot instead of its shape spot. Positioning
    /// follows the request on its next update; renew it every step to keep it, since a request
    /// not renewed for a step lapses and the skater goes back to its job.</summary>
    public static void Request(World world, Player p, Vector2 target, bool sprint = false)
    {
        var plan = Board(world).For(p.Id);
        plan.Request = target;
        plan.RequestSprint = sprint;
        plan.RequestTick = world.Tick;
    }

    /// <summary>Gets whether the AI runs in this world: in a match under the match flow, or under the
    /// positioning scenes' stand-in. Test scenes that script their own skaters are left alone.</summary>
    public static bool Drives(World world)
    {
        return MatchFlow.IsRunning(world) || StandInPlay.Running(world);
    }

    internal static PositioningBoard Board(World world)
    {
        return world.Positioning.Get(() => new PositioningBoard());
    }
}

/// <summary>Positioning's state for one match: roles, jobs and each skater's plan.</summary>
internal sealed class PositioningBoard
{
    readonly Dictionary<int, SkaterPlan> plans = [];

    /// <summary>Gets each side's phase, indexed by <see cref="Side"/>.</summary>
    public TeamPhase[] Phase { get; } = [TeamPhase.Defend, TeamPhase.Defend];

    /// <summary>Gets each side's attacking-zone cycle, indexed by <see cref="Side"/>.</summary>
    public CycleState[] Cycle { get; } = [new(), new()];

    /// <summary>Gets the on-ice skaters the roles were handed out to, so they are only redone after a change.</summary>
    public string Lineup { get; set; } = "";

    public Maybe<SkaterPlan> Get(int id)
    {
        return plans.TryGetValue(id, out var plan) ? plan : Maybe<SkaterPlan>.None;
    }

    public SkaterPlan For(int id)
    {
        if (!plans.TryGetValue(id, out var plan)) plans[id] = plan = new SkaterPlan();
        return plan;
    }
}

/// <summary>One skater's plan: role, job, ideal spot, the smoothed spot it is skating to, and any request.</summary>
internal sealed class SkaterPlan
{
    public Role Role { get; set; }

    public Job Job { get; set; }

    /// <summary>Gets or sets the role's ideal spot this step.</summary>
    public Vector2 Ideal { get; set; }

    /// <summary>Gets or sets where the skater is heading this step.</summary>
    public Vector2 Target { get; set; }

    /// <summary>Gets or sets the shape spot as this skater reads it: it trails the ideal spot, slower for poor positioning.</summary>
    public Maybe<Vector2> Read { get; set; }

    /// <summary>Gets or sets this skater's misjudgement of its spot; bigger for poor positioning.</summary>
    public Vector2 Error { get; set; }

    /// <summary>Gets or sets when the misjudgement is next re-rolled.</summary>
    public float ErrorUntil { get; set; } = float.NegativeInfinity;

    /// <summary>Gets or sets the skater's cycle station this step, or None.</summary>
    public Maybe<Station> Station { get; set; }

    /// <summary>Gets or sets the spot this skater last read as open for a pass, or None.</summary>
    public Maybe<Vector2> Open { get; set; }

    /// <summary>Gets or sets when the skater next reads where to get open.</summary>
    public float OpenNext { get; set; }

    public Maybe<Vector2> Request { get; set; }

    public bool RequestSprint { get; set; }

    public long RequestTick { get; set; } = long.MinValue;
}
