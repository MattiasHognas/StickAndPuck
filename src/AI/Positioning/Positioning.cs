using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;

namespace StickAndPuck.AI;

/// <summary>
/// W6a: what positioning decided this step, per skater, and the way for decisions (W6b) to ask
/// for a spot. <see cref="PositioningSystem"/> runs before <see cref="DecisionSystem"/> each step:
/// <list type="number">
/// <item>Positioning gives every skater a <see cref="Role"/>, a <see cref="Job"/> and a target, and
/// writes Move and Sprint into each AI skater's command to take it there.</item>
/// <item>Decisions read <see cref="JobOf"/>, <see cref="TargetOf"/> and so on, then add Pass, Shoot
/// or Check to the command. To send a skater elsewhere they either rewrite Move right away with
/// <see cref="Steering.Toward"/>, or call <see cref="Request"/>, which positioning follows from the
/// next step for as long as the request is renewed every step.</item>
/// </list>
/// </summary>
public static class Positioning
{
    static readonly ConditionalWeakTable<World, PositioningBoard> Boards = [];

    /// <summary>Gets a skater's role.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The role; centre for anyone positioning has not seen yet.</returns>
    public static Role RoleOf(World world, Player p)
    {
        return Board(world).Get(p.Id)?.Role ?? Role.Centre;
    }

    /// <summary>Gets what a skater is doing this step.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The job.</returns>
    public static Job JobOf(World world, Player p)
    {
        return Board(world).Get(p.Id)?.Job ?? Job.Idle;
    }

    /// <summary>Gets where a skater is heading this step, after smart positioning and spacing.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The target on the ice (x, z).</returns>
    public static Vector2 TargetOf(World world, Player p)
    {
        return Board(world).Get(p.Id)?.Target ?? new Vector2(p.Position.X, p.Position.Z);
    }

    /// <summary>Gets the ideal spot for a skater's role this step, from where the puck is now, before smart positioning reads or blurs it.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The spot on the ice (x, z).</returns>
    public static Vector2 IdealOf(World world, Player p)
    {
        return Board(world).Get(p.Id)?.Ideal ?? new Vector2(p.Position.X, p.Position.Z);
    }

    /// <summary>Gets whether a side is attacking or defending this step.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The phase.</returns>
    public static TeamPhase PhaseOf(World world, Side side)
    {
        return Board(world).Phase[(int)side];
    }

    /// <summary>
    /// Asks positioning to send an AI skater to a spot instead of its shape spot. Positioning
    /// follows the request on its next update; renew it every step to keep it, since a request
    /// not renewed for a step lapses and the skater goes back to its job.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <param name="target">The spot on the ice (x, z).</param>
    /// <param name="sprint">Whether the skater may sprint there.</param>
    public static void Request(World world, Player p, Vector2 target, bool sprint = false)
    {
        var plan = Board(world).For(p.Id);
        plan.Request = target;
        plan.RequestSprint = sprint;
        plan.RequestTick = world.Tick;
    }

    internal static PositioningBoard Board(World world)
    {
        return Boards.GetValue(world, _ => new PositioningBoard());
    }
}

/// <summary>Positioning's state for one match: roles, jobs and each skater's plan.</summary>
internal sealed class PositioningBoard
{
    readonly Dictionary<int, SkaterPlan> plans = [];

    /// <summary>Gets each side's phase, indexed by <see cref="Side"/>.</summary>
    public TeamPhase[] Phase { get; } = [TeamPhase.Defend, TeamPhase.Defend];

    /// <summary>Gets the on-ice skaters the roles were handed out to, so they are only redone after a change.</summary>
    public string Lineup { get; set; } = "";

    public SkaterPlan? Get(int id)
    {
        return plans.GetValueOrDefault(id);
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
    public Vector2? Read { get; set; }

    /// <summary>Gets or sets this skater's misjudgement of its spot; bigger for poor positioning.</summary>
    public Vector2 Error { get; set; }

    /// <summary>Gets or sets when the misjudgement is next re-rolled.</summary>
    public float ErrorUntil { get; set; } = float.NegativeInfinity;

    public Vector2? Request { get; set; }

    public bool RequestSprint { get; set; }

    public long RequestTick { get; set; } = long.MinValue;
}
