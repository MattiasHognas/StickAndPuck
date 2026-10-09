using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;

namespace StickAndPuck.AI;

/// <summary>
/// W6a: where each AI skater skates, by role, in attack and defence, from smart positioning.
/// Writes Move and Sprint into the <see cref="Player.Command"/> of every skater with no human
/// slot (the other buttons are cleared for decisions to fill); goalies are W5's.
/// <list type="bullet">
/// <item>Each side's skaters get a role (<see cref="Roles"/>) and the side a phase: attacking when
/// it has the puck or touched it last, else defending.</item>
/// <item>Jobs: the quickest skater of each side to a loose puck chases it, the defender nearest
/// the carrier closes him down from the net side, an AI carrier skates the puck up ice, and the
/// rest hold their role's spot in the <see cref="TeamShape"/>. At a faceoff they line up on the dot.</item>
/// <item>Smart positioning: a smart skater reads where the puck is going, follows the play quickly
/// and finds the exact spot; a poor one trails the play and settles a few metres off it.</item>
/// <item>Teammates keep a couple of metres apart, so a shape never collapses into a pile.</item>
/// </list>
/// Decisions (W6b) run next and add passes, shots and checks; see <see cref="Positioning"/>.
/// </summary>
internal sealed class PositioningSystem : ISystem
{
    const float Spacing = 2.4f;       // teammates closer than this push their spots apart
    const float PressureGap = 1.3f;   // how far goal side of the carrier the pressure skater stands

    readonly Random rng;

    public PositioningSystem()
#pragma warning disable S2245 // gameplay variation, not security
        : this(new Random())
#pragma warning restore S2245
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for each skater's misjudgement.</param>
    public PositioningSystem(Random rng)
    {
        this.rng = rng;
    }

    public void Update(World world, float dt)
    {
        // the shootout (W7c, src/Rules/Overtime) skates its own shooter
        if (world.Match.Phase == MatchPhase.Shootout) return;
        StandInPlay.Before(world, dt);
        var board = Positioning.Board(world);
        AssignRoles(world, board);
        var step = Read(world, board);
        foreach (var p in world.Players)
            if (p.OnIce && !p.IsGoalie) Plan(world, board.For(p.Id), p, step, dt);
        Space(world, board);
        foreach (var p in world.Players)
            if (p.OnIce && !p.IsGoalie && p.Slot < 0) Steer(world, board.For(p.Id), p);
    }

    /// <summary>This step's reading of the play: who has the puck, whether play is on, and who chases and presses for each side.</summary>
    sealed record StepView(Player? Carrier, bool Play, int?[] Chaser, int?[] Presser);

    static StepView Read(World world, PositioningBoard board)
    {
        var pk = world.Puck;
        var carrier = pk.Carrier >= 0 && pk.Carrier < world.Players.Count ? world.Players[pk.Carrier] : null;
        var lastTouch = pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count ? world.Players[pk.LastTouch] : null;
        bool play = world.Match.Phase is MatchPhase.Play or MatchPhase.Overtime && pk.Live;

        var owner = carrier?.Side ?? lastTouch?.Side;
        foreach (var side in new[] { Side.Home, Side.Away })
            board.Phase[(int)side] = owner == side ? TeamPhase.Attack : owner == null ? Loose(world, side) : TeamPhase.Defend;

        var view = new StepView(carrier, play, new int?[2], new int?[2]);
        if (play && carrier == null && Chaseable(pk))
        {
            view.Chaser[0] = Quickest(world, Side.Home);
            view.Chaser[1] = Quickest(world, Side.Away);
        }
        if (play && carrier != null)
        {
            var defending = carrier.Side == Side.Home ? Side.Away : Side.Home;
            view.Presser[(int)defending] = Nearest(world, defending, carrier);
        }
        return view;
    }

    /// <summary>Picks a skater's job and target for this step.</summary>
    void Plan(World world, SkaterPlan plan, Player p, StepView step, float dt)
    {
        var pk = world.Puck;
        int end = PuckHandling.AttackEnd(world, p.Side);
        float smart = Traits.Unit(p.Data.Traits.SmartPositioning);
        var teamPhase = Positioning.Board(world).Phase[(int)p.Side];
        plan.Ideal = TeamShape.Spot(plan.Role, teamPhase, end, new Vector2(pk.Position.X, pk.Position.Z));

        plan.Job = p.Slot >= 0 ? Job.Human
            : world.Tick - plan.RequestTick <= 1 && plan.Request != null ? Job.Requested
            : world.Match.Phase == MatchPhase.Faceoff ? Job.Faceoff
            : !step.Play ? Job.Idle
            : step.Carrier == p ? Job.Carry
            : step.Chaser[(int)p.Side] == p.Id ? Job.Chase
            : step.Presser[(int)p.Side] == p.Id ? Job.Pressure
            : Job.Shape;

        plan.Target = plan.Job switch
        {
            Job.Requested => plan.Request!.Value,
            Job.Faceoff => TeamShape.Faceoff(plan.Role, end, new Vector2(pk.Position.X, pk.Position.Z)),
            Job.Carry => CarryTarget(world, p, end),
            Job.Chase => ChaseTarget(p, pk, smart),
            Job.Pressure => PressureTarget(step.Carrier!, end, smart),
            Job.Shape => ShapeTarget(world, plan, p, TeamShape.Spot(plan.Role, teamPhase, end, Read(pk, smart)), smart, dt),
            _ => new Vector2(p.Position.X, p.Position.Z),
        };
        if (plan.Job != Job.Shape) plan.Read = null;
    }

    /// <summary>Writes the command that takes an AI skater to its target; the stand-in may add a shot or pass.</summary>
    static void Steer(World world, SkaterPlan plan, Player p)
    {
        var at = new Vector2(p.Position.X, p.Position.Z);
        var command = plan.Job switch
        {
            Job.Idle => Steering.Toward(p, at),
            Job.Chase or Job.Pressure => Steering.Toward(p, plan.Target, 0.8f, sprint: true),
            Job.Carry => Steering.Toward(p, plan.Target, 1.5f, sprint: true),
            Job.Requested => Steering.Toward(p, plan.Target, sprint: plan.RequestSprint),
            Job.Faceoff => Steering.Toward(p, plan.Target, 1.5f),
            _ => Steering.Toward(p, plan.Target, sprint: Vector2.Distance(plan.Target, at) > 12f),
        };
        StandInPlay.Act(world, p, ref command);
        p.Command = command;
    }

    public void DrawScene(World world, RenderContext render)
    {
        StandInPlay.DrawTargets(world);
    }

    public void DrawHud(World world, RenderContext render)
    {
        StandInPlay.DrawHud(world);
    }

    /// <summary>Hands out roles again whenever the skaters on the ice change (line changes, penalties).</summary>
    static void AssignRoles(World world, PositioningBoard board)
    {
        string lineup = string.Join(",", world.Players.Where(p => p.OnIce && !p.IsGoalie).Select(p => p.Id));
        if (string.Equals(lineup, board.Lineup, StringComparison.Ordinal)) return;
        board.Lineup = lineup;
        foreach (var side in new[] { Side.Home, Side.Away })
            foreach (var (id, role) in Roles.Assign([.. world.OnIce(side).Where(p => !p.IsGoalie)]))
                board.For(id).Role = role;
    }

    /// <summary>With nobody having touched the puck, the side that can get to it first plays as if attacking.</summary>
    static TeamPhase Loose(World world, Side side)
    {
        var other = side == Side.Home ? Side.Away : Side.Home;
        return BestTime(world, side) <= BestTime(world, other) ? TeamPhase.Attack : TeamPhase.Defend;
    }

    static bool Chaseable(Puck pk)
    {
        return pk.Position.Y < 1.5f && !PuckHandling.InNet(pk.Position) && MathF.Abs(pk.Position.X) < RinkSpec.HalfLength;
    }

    /// <summary>The puck as a skater reads it: a smart one sees a little of where it is going, a poor one only where it is.</summary>
    static Vector2 Read(Puck pk, float smart)
    {
        var ahead = new Vector2(pk.Velocity.X, pk.Velocity.Z) * (0.3f * smart);
        if (ahead.Length() > 4f) ahead = Vector2.Normalize(ahead) * 4f;
        return TeamShape.Inside(new Vector2(pk.Position.X, pk.Position.Z) + ahead, 0.5f);
    }

    /// <summary>Gets the AI skater of a side that would reach the puck first, unless a human on that side would.</summary>
    static int? Quickest(World world, Side side)
    {
        Player? best = null;
        float bestTime = float.MaxValue;
        foreach (var p in world.OnIce(side))
        {
            if (p.IsGoalie || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            float t = TimeTo(p, world.Puck);
            if (t < bestTime) { bestTime = t; best = p; }
        }
        return best is { Slot: < 0 } ? best.Id : null;
    }

    static float BestTime(World world, Side side)
    {
        return world.OnIce(side).Where(p => !p.IsGoalie).Select(p => TimeTo(p, world.Puck)).DefaultIfEmpty(float.MaxValue).Min();
    }

    static float TimeTo(Player p, Puck pk)
    {
        var at = Intercept(p, pk, 1f);
        return Vector2.Distance(at, new Vector2(p.Position.X, p.Position.Z)) / SkatingModel.TopSpeed(p.Data.Traits, p.Energy);
    }

    /// <summary>Gets where a skater meets a sliding puck; a smart skater leads it fully, a poor one partly.</summary>
    static Vector2 Intercept(Player p, Puck pk, float smart)
    {
        var from = new Vector2(p.Position.X, p.Position.Z);
        var puck = new Vector2(pk.Position.X, pk.Position.Z);
        var v = new Vector2(pk.Velocity.X, pk.Velocity.Z);
        float speed = SkatingModel.TopSpeed(p.Data.Traits, p.Energy);
        var at = puck;
        for (int i = 0; i < 4; i++)
        {
            float t = Math.Min(Vector2.Distance(from, at) / speed, 1.5f);
            at = puck + v * t * (0.5f + 0.5f * smart);
        }
        return TeamShape.Inside(at, SkatingModel.BodyRadius + 0.1f);
    }

    static Vector2 ChaseTarget(Player p, Puck pk, float smart)
    {
        var at = Intercept(p, pk, smart);
        return TeamShape.Inside(Steering.BodyForBlade(p, at), SkatingModel.BodyRadius + 0.05f);
    }

    /// <summary>The nearest AI skater of the defending side to the carrier; nobody when a human is nearer.</summary>
    static int? Nearest(World world, Side side, Player carrier)
    {
        var best = world.OnIce(side).Where(p => !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight))
            .MinBy(p => Vector3.DistanceSquared(p.Position, carrier.Position));
        return best is { Slot: < 0 } ? best.Id : null;
    }

    /// <summary>Goal side of the carrier, a step ahead of where he is going for a smart defender.</summary>
    static Vector2 PressureTarget(Player carrier, int defenderEnd, float smart)
    {
        var at = carrier.Position + carrier.Velocity * (0.35f * smart);
        var c = new Vector2(at.X, at.Z);
        var net = new Vector2(-defenderEnd * RinkSpec.GoalLineX, 0);
        var toNet = net - c;
        if (toNet.LengthSquared() > 1e-4f) c += Vector2.Normalize(toNet) * PressureGap;
        return TeamShape.Inside(c, SkatingModel.BodyRadius + 0.2f);
    }

    /// <summary>
    /// Until decisions (W6b) take over, an AI carrier skates the puck into the slot, slipping
    /// sideways away from a defender in his path.
    /// </summary>
    static Vector2 CarryTarget(World world, Player p, int end)
    {
        float pa = end * p.Position.X, pz = p.Position.Z;
        var spot = pa > RinkSpec.GoalLineX - 9f
            ? new Vector2(RinkSpec.GoalLineX - 8f, pz > 0 ? -2.5f : 2.5f)
            : new Vector2(Math.Min(pa + 10f, RinkSpec.GoalLineX - 8f), pz * 0.5f);
        var ahead = world.Players
            .Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie)
            .Select(o => (o, a: end * o.Position.X - pa, d: Vector3.Distance(o.Position, p.Position)))
            .Where(x => x.a > 0 && x.d < 5f)
            .OrderBy(x => x.d)
            .Select(x => x.o)
            .FirstOrDefault();
        if (ahead != null) spot.Y += ahead.Position.Z > pz ? -3f : 3f;
        return TeamShape.Inside(new Vector2(end * spot.X, spot.Y));
    }

    /// <summary>The role's spot as this skater reads it: trailing the ideal spot and a little off it, by smart positioning.</summary>
    Vector2 ShapeTarget(World world, SkaterPlan plan, Player p, Vector2 spot, float smart, float dt)
    {
        if (world.Time >= plan.ErrorUntil)
        {
            float angle = rng.NextSingle() * MathF.Tau, size = (1f - smart) * 3.5f * MathF.Sqrt(rng.NextSingle());
            plan.Error = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            plan.ErrorUntil = world.Time + 4f + rng.NextSingle() * 4f;
        }
        float follow = Math.Min(1f, (1.2f + 4.8f * smart) * dt);
        var read = plan.Read ?? new Vector2(p.Position.X, p.Position.Z);
        read += (spot - read) * follow;
        plan.Read = read;
        return TeamShape.Inside(read + plan.Error);
    }

    /// <summary>Pushes the spots of teammates holding shape apart, so nobody stacks on another.</summary>
    static void Space(World world, PositioningBoard board)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var team = world.OnIce(side).Where(p => !p.IsGoalie).ToList();
            foreach (var p in team)
            {
                var plan = board.For(p.Id);
                if (plan.Job != Job.Shape) continue;
                var push = Vector2.Zero;
                foreach (var mate in team)
                {
                    if (mate == p) continue;
                    var other = board.For(mate.Id);
                    var away = plan.Target - (other.Job == Job.Shape ? other.Target : new Vector2(mate.Position.X, mate.Position.Z));
                    float d = away.Length();
                    if (d >= Spacing) continue;
                    var dir = d > 1e-3f ? away / d : new Vector2(MathF.Cos(p.Id), MathF.Sin(p.Id));
                    push += dir * (Spacing - d) * 0.6f;
                }
                plan.Target = TeamShape.Inside(plan.Target + push);
            }
        }
    }
}
