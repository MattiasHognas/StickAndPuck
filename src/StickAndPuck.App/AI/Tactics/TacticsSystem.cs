using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI;

/// <summary>
/// W22d: each club plays its own tactics, from the <c>tactics</c> field of its club file
/// (<see cref="ClubTactics"/>). Runs after positioning (W6a), defence (W22b) and special teams
/// (W22c) and before decisions (W6b), and takes over AI skaters the same way defence does: job
/// <see cref="Job.Requested"/>, its own target and the move, so decisions leave them alone.
/// Only at even strength in regulation; overtime's 3-on-3 and special teams stay as they are.
/// <list type="bullet">
/// <item>Aggressive forecheck (2-1-2): while the other side has the puck in its own half, the
/// skater positioning sends at the carrier stays on him, a second forward goes in on his nearest
/// outlet, the third sits high in the zone and both defencemen pinch at the blue line.</item>
/// <item>Trap (1-3-1): while the other side has the puck in its own zone, one forward angles at
/// their blue line, three wait in a line across the red line and the last defenceman stays back.</item>
/// <item>Dump and chase: an AI carrier coming through the neutral zone with defenders ahead
/// carries wide and, once over the red line, fires the puck into the corner with fewer defenders;
/// two forwards go after it.</item>
/// <item>Power play: a club that always sets up the umbrella or the 1-3-1 gets that shape instead
/// of W22c's coin toss.</item>
/// <item>Balanced and carry change nothing: the AI as it plays without tactics.</item>
/// </list>
/// Hockey IQ still sets how quickly and exactly each skater reads his tactical spot.
/// </summary>
internal sealed class TacticsSystem : ISystem
{
    const float NoShotRange = 26.4f;   // a wrist shot from nearer, facing the net, goes at the net (PuckHandling.LongestShot is 26)
    const float Release = 0.32f;       // seconds from deciding a dump to the puck leaving the stick: 3 steps of shoot plus the strike delay
    const float WideZ = 10f;           // how wide a dump-and-chase carrier takes the puck before the red line
    const float ChaseFor = 3.5f;       // seconds the second forward goes after a dump
    const float Settled = 1.5f;        // seconds the breakout side has had the puck before it counts in the tally

    /// <summary>The dice seed without a match seed (W67).</summary>
    internal const int Fallback = 4;

    readonly Random rng;

    public TacticsSystem()
        : this(Seeds.Dice(Seeds.Environment, "tactics", Fallback))
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for the dump-or-carry roll.</param>
    public TacticsSystem(Random rng)
    {
        this.rng = rng;
    }

    /// <summary>Gets or sets a value indicating whether the tactics line is drawn on the HUD without the match debug setting.</summary>
    public static bool ShowLine { get; set; } = string.Equals(Environment.GetEnvironmentVariable("SAP_TACTICS_LINE"), "1", StringComparison.Ordinal);

    public void Update(World world, float dt)
    {
        var board = Tactics.Board(world);
        board.Tasks.Clear();
        var pk = world.Puck;
        if (!Positioning.Drives(world) || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime) || !pk.Live)
        {
            board.Chasing = Maybe<ChaseWatch>.None;
            board.Dump = Maybe<DumpPlan>.None;
            Track(world, board, Maybe<Player>.None, 0f, false);
            return;
        }

        var carrier = pk.Carrier >= 0 && pk.Carrier < world.Players.Count ? world.Players[pk.Carrier] : Maybe<Player>.None;
        if (Penalties.PowerPlay(world).TryGetSide(out var pp)) PowerPlay(world, pp);
        bool even = world.Match.Phase == MatchPhase.Play && Penalties.PowerPlay(world) == SideOrNone.None
            && Skaters(world, Side.Home).Count == Skaters(world, Side.Away).Count;
        Track(world, board, carrier, dt, even);
        if (!even) { board.Chasing = Maybe<ChaseWatch>.None; board.Dump = Maybe<DumpPlan>.None; return; }

        Watch(world, board, carrier);
        if (carrier.TryGet(out var skater) && !skater.IsGoalie)
        {
            if (Tactics.Of(world, skater.Side).Entry == ZoneEntry.DumpAndChase && skater.Slot < 0) Entry(world, board, skater);
            var defending = Other(skater.Side);
            switch (Tactics.Of(world, defending).Forecheck)
            {
                case Forecheck.Aggressive: Aggressive(world, board, defending, skater, dt); break;
                case Forecheck.Trap: Trap(world, board, defending, skater, dt); break;
                default: break;
            }
        }
        else if (!carrier.HasValue && board.Chasing.TryGet(out var chase))
        {
            if (world.Time > chase.Until) board.Chasing = Maybe<ChaseWatch>.None;
            else Chase(world, board, chase.Side, dt);
        }
    }

    /// <summary>Follows a dump: off the stick, then who gets to it first.</summary>
    static void Watch(World world, TacticsBoard board, Maybe<Player> onPuck)
    {
        if (!board.Chasing.TryGet(out var dumped)) return;
        if (!onPuck.TryGet(out var carrier))
        {
            board.Chasing = dumped with { Released = true };
        }
        else if (dumped.Released || carrier.Id != dumped.Dumper || world.Time > dumped.Until)
        {
            if (dumped.Released && carrier.Side == dumped.Side) Tactics.Tally(world, dumped.Side).Recovered++;
            board.Chasing = Maybe<ChaseWatch>.None;
        }
    }

    /// <summary>A club that always plays one power play shape gets it, in place of special teams' coin toss.</summary>
    static void PowerPlay(World world, Side pp)
    {
        var st = SpecialTeams.Board(world);
        if (st.PowerPlay.Is(pp) && PowerPlayShapes.Formation(Tactics.Of(world, pp).PowerPlay).TryGet(out var shape)) st.PpShape = shape;
    }

    /// <summary>
    /// Dump and chase: once per run with the puck, a carrier coming up to the red line with
    /// defenders ahead decides to dump it. He takes it wide, and once the puck will leave his
    /// stick over the red line and too far out to count as a shot at the net, he fires it into the
    /// corner the defence has left emptier.
    /// </summary>
    void Entry(World world, TacticsBoard board, Player p)
    {
        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        float a = end * at.X;
        var brain = Decisions.Board(world).For(p.Id);
        var run = (p.Id, brain.GotAt);
        if (board.Dump.TryGet(out var plan) && (plan.Player, plan.GotAt) != run) board.Dump = Maybe<DumpPlan>.None;

        if (!board.Dump.HasValue)
        {
            if (a is < -8f or > 0.5f || board.Weighed == run) return;
            board.Weighed = run;
            var opponents = Skaters(world, Other(p.Side));
            bool covered = opponents.Exists(o => end * o.Position.X > a && Vector3.Distance(o.Position, p.Position) < 12f);
            if (rng.NextSingle() >= (covered ? 0.75f : 0.2f)) return;   // open ice ahead: carry it in
            float wide = MathF.Abs(at.Y) > 2f ? MathF.Sign(at.Y) : rng.NextSingle() < 0.5f ? -1f : 1f;
            board.Dump = new DumpPlan(p.Id, brain.GotAt, wide);
        }

        if (!board.Dump.TryGet(out var dump)) return;   // set just above
        if (a > RinkSpec.BlueLineX - 0.5f) { board.Dump = Maybe<DumpPlan>.None; return; }   // missed the moment: carry it in
        if (AiCommand.Committed(world, p)) return;
        AiCommand.Hold(world, p);   // no pass or shot of decisions' own meanwhile

        var then = at + Flat(p.Velocity) * Release;
        var net = new Vector2(end * RinkSpec.GoalLineX, 0);
        if (end * then.X < 0.6f || Vector2.Distance(then, net) < NoShotRange)
        {
            // take it wide toward the red line, where a dump is not icing and not a shot on goal
            var lane = TeamShape.Inside(new Vector2(end * (a + 6f), dump.Wide * WideZ));
            AiCommand.Steer(world, p, AiLayer.Tactics, Steering.Toward(p, lane, 1f, sprint: true), "dump: take it wide");
            return;
        }

        var aim = Vector2.Normalize(DumpTarget(world, p.Side, end) - at);
        AiCommand.Shoot(world, p, AiLayer.Tactics, aim, 3, "dump");   // a tap: a wrist shot along the ice
        Tactics.Tally(world, p.Side).Dumps++;
        board.Dump = Maybe<DumpPlan>.None;
        board.Chasing = new ChaseWatch(p.Side, p.Id, world.Time + Release + ChaseFor, Released: false);
    }

    /// <summary>Where a dump goes: deep in the corner the defence has left emptier, along the boards and short of the goal line, away from the goalie's stick.</summary>
    static Vector2 DumpTarget(World world, Side side, int end)
    {
        return new Vector2(end * (RinkSpec.GoalLineX - 3f), Emptier(world, side, end) * (RinkSpec.HalfWidth - 1f));
    }

    /// <summary>The side of the attacked zone (z sign) with fewer defenders in it.</summary>
    static float Emptier(World world, Side side, int end)
    {
        float lean = 0;
        foreach (var o in world.OnIce(Other(side)))
            if (!o.IsGoalie && end * o.Position.X > RinkSpec.BlueLineX - 3f) lean += MathF.Sign(o.Position.Z);
        return lean > 0 ? -1f : 1f;
    }

    /// <summary>After a dump, the forward nearest the loose puck, besides positioning's chaser, goes in after it too.</summary>
    void Chase(World world, TacticsBoard board, Side side, float dt)
    {
        var puck = Flat(world.Puck.Position);
        if (Skaters(world, side)
            .Where(p => p.Slot < 0 && !Roles.IsDefence(Positioning.RoleOf(world, p)) && Positioning.JobOf(world, p) != Job.Chase)
            .MinBy(p => Vector2.Distance(Flat(p.Position), puck)) is not { } second)
        {
            return;
        }

        int end = PuckHandling.AttackEnd(world, side);
        // never ahead of the puck over the blue line: that is offside
        var spot = end * puck.X > RinkSpec.BlueLineX ? puck : new Vector2(end * Math.Min(end * puck.X, RinkSpec.BlueLineX - 0.8f), puck.Y);
        Take(world, board, second, TacticTask.Chase, TeamShape.Inside(spot), dt, sprint: true);
    }

    /// <summary>
    /// 2-1-2: with the puck in the other side's half, a second forward takes the carrier's nearest
    /// outlet, the third sits high, and the defence pinch at the blue line (or stand up at the red
    /// line while the puck is still outside the zone). The man on the puck stays positioning's and
    /// defence's.
    /// </summary>
    void Aggressive(World world, TacticsBoard board, Side side, Player carrier, float dt)
    {
        int end = PuckHandling.AttackEnd(world, side);
        var c = Flat(carrier.Position);
        float ca = end * c.X;
        if (ca <= 0f) return;
        var team = Skaters(world, side).Where(p => p.Slot < 0 && !OnCarrier(world, p)).ToList();
        var forwards = team.Where(p => !Roles.IsDefence(Positioning.RoleOf(world, p))).ToList();
        var defence = team.Where(p => Roles.IsDefence(Positioning.RoleOf(world, p))).ToList();

        if (Skaters(world, carrier.Side).Where(p => p != carrier).MinBy(p => Vector3.Distance(p.Position, carrier.Position)) is { } outlet && forwards.Count > 0)
        {
            var o = Flat(outlet.Position);
            var second = forwards.MinBy(p => Vector2.Distance(Flat(p.Position), o))!;
            forwards.Remove(second);
            var between = o + SafeNormal(c - o) * 1.5f;
            Take(world, board, second, TacticTask.Outlet, TeamShape.Inside(between), dt, sprint: true);
        }

        bool deep = ca > RinkSpec.BlueLineX;
        foreach (var p in forwards)
        {
            var high = deep ? new Vector2(end * (RinkSpec.BlueLineX + 4f), c.Y * 0.3f) : new Vector2(end * Math.Max(ca - 4f, 0.5f), c.Y * 0.3f);
            Take(world, board, p, TacticTask.High, TeamShape.Inside(high), dt, sprint: false);
        }
        foreach (var p in defence)
        {
            float s = TeamShape.Lateral(Positioning.RoleOf(world, p), end);
            var pinch = deep ? new Vector2(end * (RinkSpec.BlueLineX + 0.8f), s * 5.5f + c.Y * 0.2f) : new Vector2(0f, s * 4f + c.Y * 0.2f);
            Take(world, board, p, TacticTask.Pinch, TeamShape.Inside(pinch), dt, sprint: false);
        }
    }

    /// <summary>
    /// 1-3-1: with the puck in the other side's zone (or just out of it), the man nearest the
    /// carrier angles him at their blue line without going in deep, three wait in a line across
    /// the red line and the last defenceman stays back by his own blue line.
    /// </summary>
    void Trap(World world, TacticsBoard board, Side side, Player carrier, float dt)
    {
        int end = PuckHandling.AttackEnd(world, side);
        var c = Flat(carrier.Position);
        float ca = end * c.X;
        if (ca <= RinkSpec.BlueLineX - 2f) return;
        var team = Skaters(world, side).Where(p => p.Slot < 0).ToList();
        if (team.Count == 0) return;

        // the first forward: whoever positioning sent at the carrier, else the nearest forward
        if ((team.FirstOrDefault(p => OnCarrier(world, p))
            ?? team.Where(p => !Roles.IsDefence(Positioning.RoleOf(world, p))).MinBy(p => Vector2.Distance(Flat(p.Position), c))) is { } first)
        {
            team.Remove(first);
            var angle = new Vector2(end * Math.Min(ca - 3f, RinkSpec.BlueLineX + 2f), c.Y * 0.6f);
            Take(world, board, first, TacticTask.Angle, TeamShape.Inside(angle), dt, sprint: false);
        }

        // the defenceman furthest back stays back; the other three make the wall
        if (team.Where(p => Roles.IsDefence(Positioning.RoleOf(world, p))).MinBy(p => end * p.Position.X) is { } back && team.Count > 3)
        {
            team.Remove(back);
            var spot = new Vector2(end * (-RinkSpec.BlueLineX - 2f), c.Y * 0.2f);
            Take(world, board, back, TacticTask.Back, TeamShape.Inside(spot), dt, sprint: false);
        }
        float[] lanes = team.Count switch { 1 => [0f], 2 => [-4f, 4f], _ => [-7f, 0f, 7f] };
        var wall = team.OrderBy(p => p.Position.Z).Take(lanes.Length).ToList();
        for (int i = 0; i < wall.Count; i++)
        {
            var spot = new Vector2(end * -1.5f, lanes[i] + c.Y * 0.25f);
            Take(world, board, wall[i], TacticTask.Wall, TeamShape.Inside(spot), dt, sprint: false);
        }
    }

    /// <summary>Gets whether positioning or defence has this skater on the carrier.</summary>
    static bool OnCarrier(World world, Player p)
    {
        return Positioning.JobOf(world, p) == Job.Pressure || DefenceSystem.DutyOf(world, p) is Duty.Contain or Duty.Engage;
    }

    /// <summary>Takes a skater over from positioning and defence and skates him to his tactical spot, read by his hockey IQ.</summary>
    static void Take(World world, TacticsBoard board, Player p, TacticTask task, Vector2 spot, float dt, bool sprint)
    {
        board.Tasks[p.Id] = task;
        float iq = p.Data.Traits.Iq;
        var read = board.Reads.TryGetValue(p.Id, out var r) && world.Tick - r.Tick <= 2 ? r.At : Flat(p.Position);
        read += (spot - read) * Math.Min(1f, (2f + 6f * iq) * dt);
        board.Reads[p.Id] = (read, world.Tick);
        var target = sprint ? spot : read;
        float far = Vector2.Distance(target, Flat(p.Position));
        var move = sprint ? Steering.Toward(p, target, 0.8f, sprint: true) : Steering.Toward(p, target, 1.6f, sprint: far > 4f);
        AiCommand.Take(world, p, AiLayer.Tactics, target, move, Reason(task));
    }

    /// <summary>Counts each side's zone entries and, while the other side breaks out of its zone, where its skaters stand.</summary>
    static void Track(World world, TacticsBoard board, Maybe<Player> carrier, float dt, bool even)
    {
        var pk = world.Puck;
        var lastTouch = pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count ? world.Players[pk.LastTouch] : Maybe<Player>.None;
        var owner = carrier.TryGet(out var holder) ? holder.Side.OrNone() : lastTouch.TryGet(out var toucher) ? toucher.Side.OrNone() : SideOrNone.None;
        var skater = carrier.TryGet(out var held) && !held.IsGoalie ? held : Maybe<Player>.None;
        if (carrier.TryGet(out var onPuck) && !board.Owner.Is(onPuck.Side))
        {
            board.Owner = onPuck.Side.OrNone();
            board.OwnedSince = world.Time;
        }
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int end = PuckHandling.AttackEnd(world, side);
            float a = end * pk.Position.X;
            float last = board.LastA[(int)side];
            board.LastA[(int)side] = a;
            if (!even) continue;
            var tally = Tactics.Tally(world, side);
            if (owner.Is(side) && last <= RinkSpec.BlueLineX && a > RinkSpec.BlueLineX)
            {
                tally.Entries++;
                if (carrier.TryGet(out var entering) && entering.Side == side) tally.Carried++;
            }

            // the other side carrying the puck out over its own blue line: how many of this side are back, goal side of it
            if (skater.TryGet(out var exiting) && exiting.Side != side && board.LastCarrierA[(int)side] > RinkSpec.BlueLineX && end * exiting.Position.X <= RinkSpec.BlueLineX)
            {
                tally.Exits++;
                tally.BackAtExits += Skaters(world, side).Count(p => end * p.Position.X < end * exiting.Position.X - 1f);
            }
            board.LastCarrierA[(int)side] = skater.TryGet(out var leaving) && leaving.Side != side ? end * leaving.Position.X : float.NegativeInfinity;

            // the other side carrying the puck in its own zone, settled for a moment: a breakout this side is meeting
            if (!skater.TryGet(out var breaking) || breaking.Side == side || end * breaking.Position.X <= RinkSpec.BlueLineX) continue;
            if (world.Time - board.OwnedSince < Settled) continue;
            tally.BreakoutSeconds += dt;
            foreach (var p in Skaters(world, side))
            {
                float pa = end * p.Position.X;
                if (pa > RinkSpec.BlueLineX) tally.DeepSkaterSeconds += dt;
                else if (pa >= -RinkSpec.BlueLineX) tally.NeutralSkaterSeconds += dt;
            }
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!ShowLine && !world.Settings.MatchDebug) return;
        string Club(Side side)
        {
            var t = Tactics.Of(world, side);
            var tally = Tactics.Tally(world, side);
            return $"{world.Setup.Club(side).ShortName} {Name(t.Forecheck)} {(t.Entry == ZoneEntry.DumpAndChase ? "DUMP" : "CARRY")} {tally.Dumps}/{tally.Entries}";
        }
        DrawText($"{Club(Side.Home)}   {Club(Side.Away)}", 6, 246, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }

    /// <summary>The words a debug trace shows for a tactical task.</summary>
    static string Reason(TacticTask task) => task switch
    {
        TacticTask.Chase => "chase the dump",
        TacticTask.Outlet => "forecheck: take the outlet",
        TacticTask.High => "forecheck: sit high",
        TacticTask.Pinch => "forecheck: pinch",
        TacticTask.Angle => "trap: angle",
        TacticTask.Back => "trap: stay back",
        TacticTask.Wall => "trap: wall",
        _ => "tactics",
    };

    static string Name(Forecheck f) => f switch
    {
        Forecheck.Aggressive => "2-1-2",
        Forecheck.Trap => "TRAP",
        _ => "BAL",
    };

    static List<Player> Skaters(World world, Side side)
    {
        return [.. world.OnIce(side).Where(p => !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)).OrderBy(p => p.Id)];
    }

    static Side Other(Side side) => side == Side.Home ? Side.Away : Side.Home;

    static Vector2 SafeNormal(Vector2 v) => v.LengthSquared() > 1e-6f ? Vector2.Normalize(v) : Vector2.UnitX;

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
}

/// <summary>A dump on its way: the side and skater that dumped it, until when it is chased, and whether it has left his stick.</summary>
internal sealed record ChaseWatch(Side Side, int Dumper, float Until, bool Released);

/// <summary>A dump decided on: the carrier and his run with the puck, and the side of the ice he takes it wide on.</summary>
internal sealed record DumpPlan(int Player, float GotAt, float Wide);
