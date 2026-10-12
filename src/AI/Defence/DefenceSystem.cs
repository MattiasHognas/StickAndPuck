using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;
using StickAndPuck.Players;

namespace StickAndPuck.AI;

/// <summary>
/// W22b: the defending side's reads while the other side carries the puck. Runs after positioning
/// (W6a) and before decisions (W6b): it takes over the AI skaters of the side without the puck,
/// sets their job to <see cref="Job.Requested"/> with its own target, and writes the move, so
/// decisions leave them alone. Loose pucks, faceoffs and the attacking side stay positioning's.
/// <list type="bullet">
/// <item>The skater positioning sends at the carrier contains him: goal side, stick on the puck, at a
/// gap that closes nearer the net. Once per approach he decides whether to play the body, likelier
/// along the boards and for a good checker; only then is he handed back as positioning's presser,
/// so decisions may finish the check.</item>
/// <item>The defenceman away from the puck stays home in front of the net, goal side of whoever is
/// in the slot.</item>
/// <item>The rest mark a man each, most dangerous first, standing goal side and leaning into the
/// passing lane from the carrier, so the open man is covered rather than everyone chasing the puck.</item>
/// <item>Hockey IQ sets the odds: a smart defender leads his man and sticks to the right spot; a
/// poor one reads late, stands a little off, and now and then turns to watch the puck instead.</item>
/// </list>
/// </summary>
internal sealed class DefenceSystem : ISystem
{
    const float Spacing = 2.2f;   // defenders closer than this push their spots apart

    static readonly ConditionalWeakTable<World, DefenceBoard> Boards = [];

    readonly Random rng;

    public DefenceSystem()
#pragma warning disable S2245 // gameplay variation, not security
        : this(new Random())
#pragma warning restore S2245
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for misreads, puck watching and body checks.</param>
    public DefenceSystem(Random rng)
    {
        this.rng = rng;
    }

    /// <summary>Gets what a defender is doing this step, for tests and the HUD.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The duty, or <see cref="Duty.None"/> when the defence is not steering him.</returns>
    public static Duty DutyOf(World world, Player p)
    {
        return Board(world).Get(p.Id)?.Duty ?? Duty.None;
    }

    public void Update(World world, float dt)
    {
        var board = Board(world);
        foreach (var plan in board.All) plan.Duty = Duty.None;
        if (world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime) || !Positioning.Drives(world)) return;
        var pk = world.Puck;
        if (!pk.Live || pk.Carrier < 0 || pk.Carrier >= world.Players.Count) return;
        var carrier = world.Players[pk.Carrier];
        if (carrier.IsGoalie) return;
        var side = carrier.Side == Side.Home ? Side.Away : Side.Home;
        Defend(world, board, side, carrier, dt);
    }

    void Defend(World world, DefenceBoard board, Side side, Player carrier, float dt)
    {
        int end = PuckHandling.AttackEnd(world, side);
        var view = new View(Coverage.OwnNet(end), Flat(carrier.Position), Coverage.ZoneOf(end, Flat(carrier.Position)));
        var team = world.OnIce(side).Where(p => !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)).ToList();
        var free = team.Where(p => p.Slot < 0).ToList();
        var attackers = world.OnIce(carrier.Side).Where(p => !p.IsGoalie && p != carrier).ToList();
        var spots = new Dictionary<int, Vector2>();

        Press(world, board, view, carrier, free, spots);

        // past the far blue line the forecheck is positioning's shape; only the man on the puck is ours
        if (view.Zone != DefenceZone.Forecheck )
        {
            if (view.Zone == DefenceZone.Own) StayHome(world, board, view, free, attackers, spots);
            Mark(world, board, view, free, attackers, spots);
        }

        Space(team, spots);
        foreach (var p in team)
            if (spots.TryGetValue(p.Id, out var spot)) Steer(world, board.For(p.Id), p, spot, view, dt);
    }

    /// <summary>What the defending side reads this step: its net, the carrier and his zone.</summary>
    readonly record struct View(Vector2 Net, Vector2 Carrier, DefenceZone Zone);

    /// <summary>The skater positioning sent at the carrier contains him, or plays the body on this approach.</summary>
    void Press(World world, DefenceBoard board, View view, Player carrier, List<Player> free, Dictionary<int, Vector2> spots)
    {
        var presser = free.FirstOrDefault(p => Positioning.JobOf(world, p) == Job.Pressure);
        if (presser == null) return;
        free.Remove(presser);
        var plan = board.For(presser.Id);
        if (plan.On != carrier.Id || world.Time >= plan.DecideAt)
        {
            // a new approach: decide once whether to finish a check on it
            plan.On = carrier.Id;
            plan.DecideAt = world.Time + 3f;
            float chance = Coverage.EngageChance(view.Zone, view.Net, view.Carrier, presser.Data.Traits.Checking)
                * Coverage.HitPace(Decisions.Tally(world, presser.Side).Checks, Played(world));
            plan.Engage = rng.NextSingle() < chance;
        }
        if (plan.Engage)
        {
            plan.Duty = Duty.Engage;   // positioning's pressure job stands: decisions may hit
            return;
        }
        plan.Duty = Duty.Contain;
        var led = Flat(carrier.Position + carrier.Velocity * (0.35f * presser.Data.Traits.Iq));
        spots[presser.Id] = Coverage.ContainSpot(view.Zone, view.Net, led);
    }

    /// <summary>In the own zone the defenceman furthest across from the puck stays home, on the man in front of the net.</summary>
    static void StayHome(World world, DefenceBoard board, View view, List<Player> free, List<Player> attackers, Dictionary<int, Vector2> spots)
    {
        var home = free.Where(p => Roles.IsDefence(Positioning.RoleOf(world, p)))
            .MaxBy(p => MathF.Abs(p.Position.Z - view.Carrier.Y));
        if (home == null) return;
        free.Remove(home);
        var threat = attackers.Where(a => Vector2.Distance(Flat(a.Position), view.Net) < 8f)
            .MinBy(a => Vector2.Distance(Flat(a.Position), view.Net));
        if (threat != null) attackers.Remove(threat);
        board.For(home.Id).Duty = Duty.Home;
        spots[home.Id] = Coverage.HomeSpot(view.Net, view.Carrier, threat is null ? null : Lead(threat, home));
    }

    /// <summary>The rest mark a man each, most dangerous first, the nearest free defender taking him.</summary>
    static void Mark(World world, DefenceBoard board, View view, List<Player> free, List<Player> attackers, Dictionary<int, Vector2> spots)
    {
        foreach (var a in attackers.OrderByDescending(a => Coverage.Danger(view.Net, Flat(a.Position))))
        {
            if (free.Count == 0) break;
            var ideal = Coverage.MarkSpot(view.Zone, view.Net, view.Carrier, Flat(a.Position));
            var marker = free.MinBy(p => Vector2.DistanceSquared(Positioning.IdealOf(world, p), ideal))!;
            free.Remove(marker);
            var plan = board.For(marker.Id);
            plan.Duty = Duty.Mark;
            plan.Man = a.Id;
            var mark = Coverage.MarkSpot(view.Zone, view.Net, view.Carrier, Lead(a, marker));
            spots[marker.Id] = Coverage.Leash(Positioning.IdealOf(world, marker), mark, view.Zone);
        }
    }

    /// <summary>Takes the skater over from positioning and skates him to his spot.</summary>
    void Steer(World world, DefencePlan plan, Player p, Vector2 spot, View view, float dt)
    {
        var target = plan.Duty == Duty.Contain ? spot : Read(world, plan, p, spot, view.Carrier, dt);
        // outside the own zone only the man on the puck goes to the carrier
        if (plan.Duty == Duty.Mark && view.Zone != DefenceZone.Own) target = Coverage.Room(target, view.Carrier);
        float far = Vector2.Distance(target, Flat(p.Position));
        var move = plan.Duty == Duty.Contain
            ? Steering.Toward(p, target, 0.8f, sprint: true)
            : Steering.Toward(p, target, 1.6f, sprint: far > 6f);
        AiCommand.Take(world, p, AiLayer.Defence, target, move, plan.Duty switch { Duty.Contain => "contain", Duty.Home => "stay home", _ => "mark" });
    }

    /// <summary>Where a defender reads his man to be: a smart one leads him by where he is skating.</summary>
    static Vector2 Lead(Player attacker, Player defender)
    {
        return Flat(attacker.Position + attacker.Velocity * (0.45f * defender.Data.Traits.Iq));
    }

    /// <summary>
    /// The spot as this defender reads it: trailing the ideal one (slower for a poor reader), a little
    /// off it, and now and then, for a poor reader, drawn toward the puck instead.
    /// </summary>
    Vector2 Read(World world, DefencePlan plan, Player p, Vector2 spot, Vector2 carrier, float dt)
    {
        float iq = p.Data.Traits.Iq;
        if (world.Time >= plan.ErrorUntil)
        {
            float angle = rng.NextSingle() * MathF.Tau, size = (1f - iq) * 2.2f * MathF.Sqrt(rng.NextSingle());
            plan.Error = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            plan.Watching = rng.NextSingle() < 0.3f * (1f - iq);
            plan.ErrorUntil = world.Time + 2.5f + rng.NextSingle() * 3f;
        }
        if (plan.Watching) spot = Vector2.Lerp(spot, carrier, 0.4f);
        float follow = Math.Min(1f, (2f + 6f * iq) * dt);
        var read = plan.Read is { } r && world.Tick - plan.ReadTick <= 2 ? r : Flat(p.Position);
        read += (spot - read) * follow;
        plan.Read = read;
        plan.ReadTick = world.Tick;
        return TeamShape.Inside(read + plan.Error);
    }

    /// <summary>Pushes the defenders' spots apart, so two never stack on one man.</summary>
    static void Space(List<Player> team, Dictionary<int, Vector2> spots)
    {
        foreach (var p in team)
        {
            if (!spots.TryGetValue(p.Id, out var at)) continue;
            var push = Vector2.Zero;
            foreach (var mate in team)
            {
                if (mate == p) continue;
                var other = spots.TryGetValue(mate.Id, out var s) ? s : Flat(mate.Position);
                var away = at - other;
                float d = away.Length();
                if (d >= Spacing) continue;
                var dir = d > 1e-3f ? away / d : new Vector2(MathF.Cos(p.Id), MathF.Sin(p.Id));
                push += dir * (Spacing - d) * 0.5f;
            }
            spots[p.Id] = TeamShape.Inside(at + push);
        }
    }

    /// <summary>The share of regulation played so far.</summary>
    static float Played(World world)
    {
        float period = world.Setup.PeriodSeconds;
        int periods = world.Setup.Periods;
        return Math.Min(1f, ((world.Match.Period - 1) * period + (period - world.Match.Clock)) / (periods * period));
    }

    static DefenceBoard Board(World world)
    {
        return Boards.GetValue(world, _ => new DefenceBoard());
    }

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
}

/// <summary>What the defence has a skater doing this step.</summary>
public enum Duty
{
    /// <summary>Not steered by the defence.</summary>
    None,

    /// <summary>On the carrier, goal side, not hitting.</summary>
    Contain,

    /// <summary>On the carrier and allowed to finish a check: positioning's pressure job.</summary>
    Engage,

    /// <summary>Marking a man and his passing lane.</summary>
    Mark,

    /// <summary>The defenceman staying home in front of the net.</summary>
    Home,
}

/// <summary>The defence's state for one match.</summary>
internal sealed class DefenceBoard
{
    readonly Dictionary<int, DefencePlan> plans = [];

    public IEnumerable<DefencePlan> All => plans.Values;

    public DefencePlan? Get(int id)
    {
        return plans.GetValueOrDefault(id);
    }

    public DefencePlan For(int id)
    {
        if (!plans.TryGetValue(id, out var plan)) plans[id] = plan = new DefencePlan();
        return plan;
    }
}

/// <summary>One defender's short memory.</summary>
internal sealed class DefencePlan
{
    public Duty Duty { get; set; }

    /// <summary>Gets or sets the attacker this defender marks.</summary>
    public int Man { get; set; } = -1;

    /// <summary>Gets or sets the carrier of the current approach.</summary>
    public int On { get; set; } = -1;

    /// <summary>Gets or sets when the approach is decided again.</summary>
    public float DecideAt { get; set; }

    /// <summary>Gets or sets whether this approach ends in a body check.</summary>
    public bool Engage { get; set; }

    public Vector2? Read { get; set; }

    public long ReadTick { get; set; } = long.MinValue;

    public Vector2 Error { get; set; }

    public bool Watching { get; set; }

    public float ErrorUntil { get; set; } = float.NegativeInfinity;
}
