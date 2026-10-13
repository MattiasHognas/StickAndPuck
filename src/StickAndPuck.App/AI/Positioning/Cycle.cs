using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

/// <summary>Where a forward plays in the attacking-zone cycle.</summary>
public enum Station
{
    /// <summary>In front of the net, a few metres out: the shooting spot.</summary>
    Slot,

    /// <summary>Low in the corner on the puck side, along the boards.</summary>
    Corner,

    /// <summary>Behind the net.</summary>
    BehindNet,
}

/// <summary>What one club's cycle came to in a match, for tests and the aimatch line.</summary>
public sealed class CycleTally
{
    /// <summary>Gets or sets how many times the club settled into the cycle in the attacking zone.</summary>
    public int Entries { get; set; }

    /// <summary>Gets or sets how many times the forwards rotated stations.</summary>
    public int Turns { get; set; }

    /// <summary>Gets or sets the seconds the club cycled.</summary>
    public float Seconds { get; set; }

    /// <summary>Gets or sets the seconds a cycling carrier had the puck behind the attacked goal line.</summary>
    public float BehindNet { get; set; }
}

/// <summary>The attacking-zone cycle. With the puck in the attacking zone, the forwards take three
/// stations (corner, behind the net, slot) while the defence hold the points. Every few seconds
/// they rotate: on one turn the carrier skates the puck on from the corner behind the net, or
/// from behind the net round to the other corner, and the forward there moves out; on the next the
/// two forwards without the puck swap, so the slot is always filled by someone arriving. The
/// carrier keeps looking for a pass or a shot while he goes, so the club passes the puck around
/// until a lane to a good shot opens. A smarter line turns over quicker.</summary>
public static class Cycle
{
    const float Grace = 1.5f;   // seconds a cycle survives a loose puck (a shot, a bobble) before it breaks up

    /// <summary>Gets a station's spot, for the side attacking an end.</summary>
    public static Vector2 Spot(Station station, int end, float cornerSide)
    {
        const float G = RinkSpec.GoalLineX;
        var (a, z) = station switch
        {
            Station.Corner => (G - 1.2f, cornerSide * 9.2f),
            Station.BehindNet => (G + 2.2f, -cornerSide * 0.6f),
            _ => (G - 7f, -cornerSide * 1.2f),
        };
        return TeamShape.Inside(new Vector2(end * a, z), 1.2f);
    }

    /// <summary>Gets whether a spot is far enough into the attacking zone to cycle.</summary>
    public static bool InZone(int end, Vector2 at)
    {
        return end * at.X > RinkSpec.BlueLineX + 1f;
    }

    /// <summary>Gets the next point to skate to on the way to a spot without going through the attacked
    /// net (it is solid): round the side of the cage the skater is on.</summary>
    public static Vector2 AroundNet(Vector2 from, Vector2 to, int end)
    {
        const float lo = RinkSpec.GoalLineX - 0.7f, hi = RinkSpec.GoalLineX + RinkSpec.NetDepth + 0.5f, hz = RinkSpec.NetWidth / 2 + 0.8f;
        var f = new Vector2(end * from.X, from.Y);
        var t = new Vector2(end * to.X, to.Y);
        if (!Crosses(f, t, lo, hi, hz)) return to;
        float side = MathF.Abs(f.Y) > 0.3f ? MathF.Sign(f.Y) : t.Y >= 0 ? 1f : -1f;
        float z = side * (hz + 0.4f);
        Vector2[] corners = [new(hi + 0.4f, z), new(lo - 0.4f, z), new(f.X, z)];
        var way = corners.Where(c => !Crosses(f, c, lo, hi, hz)).OrderBy(c => Vector2.Distance(c, t)).FirstOrDefault(corners[2]);
        return TeamShape.Inside(new Vector2(end * way.X, way.Y), 1.1f);
    }

    /// <summary>Gets whether a pass or a skate from one spot to another would run into either net's cage.</summary>
    public static bool ThroughNet(Vector2 from, Vector2 to)
    {
        const float lo = RinkSpec.GoalLineX - 0.2f, hi = RinkSpec.GoalLineX + RinkSpec.NetDepth + 0.2f, hz = RinkSpec.NetWidth / 2 + 0.25f;
        foreach (int end in new[] { -1, 1 })
            if (Crosses(new Vector2(end * from.X, from.Y), new Vector2(end * to.X, to.Y), lo, hi, hz)) return true;
        return false;
    }

    /// <summary>Whether the segment from f to t crosses the box a in [lo, hi], |z| &lt; hz (Liang-Barsky).</summary>
    static bool Crosses(Vector2 f, Vector2 t, float lo, float hi, float hz)
    {
        float t0 = 0, t1 = 1;
        var d = t - f;
        bool Clip(float p, float q)
        {
            if (MathF.Abs(p) < 1e-6f) return q >= 0;
            float r = q / p;
            if (p < 0)
            {
                if (r > t1) return false;
                t0 = Math.Max(t0, r);
            }
            else
            {
                if (r < t0) return false;
                t1 = Math.Min(t1, r);
            }
            return true;
        }
        return Clip(-d.X, f.X - lo) && Clip(d.X, hi - f.X) && Clip(-d.Y, f.Y + hz) && Clip(d.Y, hz - f.Y);
    }

    /// <summary>Runs one side's cycle for this step: starts it, rotates it, or breaks it up.</summary>
    internal static void Update(World world, PositioningBoard board, Side side, Maybe<Player> onPuck, bool play, Random rng, float dt)
    {
        var state = board.Cycle[(int)side];
        int end = PuckHandling.AttackEnd(world, side);
        var puck = new Vector2(world.Puck.Position.X, world.Puck.Position.Z);
        var forwards = world.OnIce(side).Where(p => !p.IsGoalie && !Roles.IsDefence(board.For(p.Id).Role)).ToList();

        bool ours = onPuck.TryGet(out var holder) && holder.Side == side;
        if (ours) state.GraceUntil = world.Time + Grace;
        bool on = play && forwards.Count >= 2 && InZone(end, puck) && (ours || (!onPuck.HasValue && world.Time < state.GraceUntil));
        if (!on)
        {
            state.Active = false;
            state.Stations.Clear();
            return;
        }

        if (!state.Active) Begin(world, state, forwards, onPuck, end, puck, rng);
        state.Tally.Seconds += dt;
        if (!onPuck.TryGet(out var carrier)) return;
        if (end * carrier.Position.X > RinkSpec.GoalLineX) state.Tally.BehindNet += dt;

        // the corner support goes to the side the puck is worked along
        if (MathF.Abs(puck.Y) > 5f && end * puck.X < RinkSpec.GoalLineX) state.CornerSide = MathF.Sign(puck.Y);

        if (carrier.Id != state.LastCarrier)
        {
            // a pass in the cycle: the new carrier gets a moment before the next turn
            state.LastCarrier = carrier.Id;
            state.MovingAt = world.Time;
            state.NextTurn = Math.Max(state.NextTurn, world.Time + 0.6f * TurnEvery(forwards, rng));
        }
        // behind the net is a place to pass through, not to stay: the carrier comes out soon after he gets there
        if (state.Stations.TryGetValue(carrier.Id, out var spot) && spot == Station.BehindNet && state.Count % 2 == 0
            && Vector2.Distance(new Vector2(carrier.Position.X, carrier.Position.Z), Spot(Station.BehindNet, end, state.CornerSide)) < 1.5f)
        {
            state.NextTurn = Math.Min(state.NextTurn, world.Time + 0.5f);
        }

        // a carrier pinned on his station skates on to the next one rather than stand there
        if (carrier.Speed > 1.5f) state.MovingAt = world.Time;
        bool pinned = world.Time - state.MovingAt > 1.2f && state.Stations.TryGetValue(carrier.Id, out var at) && at != Station.Slot;
        if (pinned && state.Count % 2 == 1) state.Count++;   // so this turn moves the carrier
        if (world.Time < state.NextTurn && !pinned) return;
        state.MovingAt = world.Time;
        Turn(state, forwards, carrier);
        state.NextTurn = world.Time + TurnEvery(forwards, rng);
    }

    static void Begin(World world, CycleState state, List<Player> forwards, Maybe<Player> carrier, int end, Vector2 puck, Random rng)
    {
        state.Active = true;
        state.Tally.Entries++;
        state.CornerSide = MathF.Abs(puck.Y) > 0.5f ? MathF.Sign(puck.Y) : 1f;
        state.LastCarrier = carrier.TryGet(out var holder) ? holder.Id : -1;
        state.NextTurn = world.Time + TurnEvery(forwards, rng);
        state.Stations.Clear();
        var free = new List<Station> { Station.Corner, Station.Slot, Station.BehindNet };
        var order = forwards.OrderBy(p => carrier.Is(c => c == p) ? 0 : 1).ToList();
        foreach (var p in order)
        {
            if (free.Count == 0) break;
            var at = new Vector2(p.Position.X, p.Position.Z);
            var best = free.MinBy(s => Vector2.Distance(at, Spot(s, end, state.CornerSide)));
            state.Stations[p.Id] = best;
            free.Remove(best);
        }
    }

    /// <summary>One rotation: the carrier skates the puck on to the next low station, or the two forwards without it swap.</summary>
    static void Turn(CycleState state, List<Player> forwards, Player carrier)
    {
        state.Tally.Turns++;
        state.Count++;
        var mates = forwards.Where(p => p != carrier && state.Stations.ContainsKey(p.Id)).ToList();
        bool carrierCycles = state.Stations.TryGetValue(carrier.Id, out var own);

        if (carrierCycles && own != Station.Slot && state.Count % 2 == 1)
        {
            // corner -> behind the net, behind the net -> round to the other corner; whoever was there takes the old spot
            var next = own == Station.Corner ? Station.BehindNet : Station.Corner;
            if (own == Station.BehindNet) state.CornerSide = -state.CornerSide;
            foreach (var m in mates.Where(m => state.Stations[m.Id] == next)) state.Stations[m.Id] = own;
            state.Stations[carrier.Id] = next;
            return;
        }

        if (carrierCycles)
        {
            if (mates.Count >= 2) (state.Stations[mates[0].Id], state.Stations[mates[1].Id]) = (state.Stations[mates[1].Id], state.Stations[mates[0].Id]);
            else if (mates.Count == 1) (state.Stations[mates[0].Id], state.Stations[carrier.Id]) = (own, state.Stations[mates[0].Id]);
            return;
        }

        // a defenceman has it at the point: all three forwards rotate slot -> corner -> behind the net -> slot
        foreach (var m in mates)
        {
            state.Stations[m.Id] = state.Stations[m.Id] switch
            {
                Station.Slot => Station.Corner,
                Station.Corner => Station.BehindNet,
                _ => Station.Slot,
            };
        }
    }

    /// <summary>Seconds between turns: about 4 for a line of hockey IQ 0, 2.6 for 100, a little random.</summary>
    static float TurnEvery(List<Player> forwards, Random rng)
    {
        int iq = (int)forwards.Average(p => p.Data.Traits.HockeyIq);
        return Traits.Scale(iq, 4f, 2.6f) * (0.8f + 0.4f * rng.NextSingle());
    }
}

/// <summary>One side's cycle: whether it runs, each forward's station and when it next turns.</summary>
internal sealed class CycleState
{
    public bool Active { get; set; }

    public float NextTurn { get; set; }

    public float GraceUntil { get; set; } = float.NegativeInfinity;

    public int LastCarrier { get; set; } = -1;

    /// <summary>Gets or sets when the carrier last skated at pace or turned, to spot a pinned carrier.</summary>
    public float MovingAt { get; set; }

    /// <summary>Gets or sets the side of the ice (z sign) the corner station is on.</summary>
    public float CornerSide { get; set; } = 1f;

    /// <summary>Gets or sets how many turns this cycle has made, to alternate the kinds of turn.</summary>
    public int Count { get; set; }

    public Dictionary<int, Station> Stations { get; } = [];

    public CycleTally Tally { get; } = new();
}
