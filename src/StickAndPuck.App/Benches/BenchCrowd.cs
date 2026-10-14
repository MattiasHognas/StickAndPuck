using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Benches;

/// <summary>What a figure on the bench is doing.</summary>
public enum BenchPose
{
    /// <summary>Sitting in his seat, watching the puck.</summary>
    Watch,

    /// <summary>Up for the next change: standing and sliding along the bench to the door.</summary>
    Ready,

    /// <summary>Back from a shift: walking from the door to his seat.</summary>
    Return,
}

/// <summary>One figure on a bench: the player, where along the bench he is, which way he faces and what he is doing.</summary>
public sealed class BenchFigure(Player player, float x, float phase)
{
    /// <summary>Gets the player.</summary>
    public Player Player { get; } = player;

    /// <summary>Gets the figure's spot along the bench (x).</summary>
    public float X { get; internal set; } = x;

    /// <summary>Gets the heading in radians, as <see cref="Core.Player.Heading"/>: 0 faces the ice.</summary>
    public float Heading { get; internal set; }

    /// <summary>Gets what the figure is doing.</summary>
    public BenchPose Pose { get; internal set; }

    /// <summary>Gets the figure's own offset into its clip, in seconds, so the bench does not move as one.</summary>
    public float Phase { get; } = phase;

    /// <summary>Gets the game time (<see cref="World.Time"/>) until which a skater just back from a shift sits tired.</summary>
    public float TiredUntil { get; internal set; }

    /// <summary>Gets how quickly this figure turns after the puck (per second).</summary>
    internal float TurnRate { get; init; } = 3f;

    /// <summary>Gets a value indicating whether the figure is walking along the bench this step.</summary>
    public bool Walking { get; internal set; }

    /// <summary>Gets a value indicating whether the figure stands (up for a change or walking).</summary>
    public bool Standing => Pose != BenchPose.Watch || Walking;
}

/// <summary>Both benches' figures, made from the squads and moved along by what the match does: the
/// skaters and goalies of each club who are off the ice and not in the penalty box. Render only:
/// it reads the world and never changes it, and draws no dice from the match's streams.
/// <list type="bullet">
/// <item>A club's change on the fly stands the incoming skaters up; they slide to the door and wait
/// there; the match swaps each one on at the door, and his figure goes as he steps onto the ice.</item>
/// <item>A skater coming off (on the fly or at a faceoff) appears at the door and walks to his seat.</item>
/// <item>The seats close up and open out as units go on and come off (<see cref="BenchLayout.Seats"/>),
/// and the figures walk along the bench to their new seats.</item>
/// <item>Seated figures turn toward the puck, each at his own pace.</item>
/// </list></summary>
public sealed class BenchCrowd
{
    /// <summary>How fast a figure walks along the bench (m/s).</summary>
    public const float WalkSpeed = 2.4f;

    /// <summary>How far a seated figure turns from facing the ice (radians).</summary>
    const float MaxTurn = 1.2f;

    readonly Dictionary<int, BenchFigure> figures = [];
    readonly Dictionary<int, bool> wasOnIce = [];
    readonly List<BenchFigure> shown = [];
    readonly int seed;

    /// <summary>Initializes a new crowd for a match.</summary>
    /// <param name="seed">The variety seed (<see cref="Seeds.For"/> of the match seed's "bench" stream).</param>
    public BenchCrowd(int seed)
    {
        this.seed = seed;
    }

    /// <summary>Gets the figures on the benches this step.</summary>
    public IReadOnlyList<BenchFigure> Shown => shown;

    /// <summary>Gets the figure of a player, if he is on a bench this step.</summary>
    public Maybe<BenchFigure> Of(Player p) => shown.FirstOrNone(f => f.Player == p);

    /// <summary>Moves every figure one step on from the world.</summary>
    public void Update(World world, float dt)
    {
        shown.Clear();
        foreach (var side in (ReadOnlySpan<Side>)[Side.Home, Side.Away]) Club(world, side, dt);
    }

    /// <summary>Forgets every figure, so the next update seats everyone where he belongs.</summary>
    public void Clear()
    {
        figures.Clear();
        wasOnIce.Clear();
        shown.Clear();
    }

    void Club(World world, Side side, float dt)
    {
        if (!Bench.SquadOf(world, side).TryGet(out var squad)) return;
        var b = Bench.Board(world);
        var others = world.Players.Where(p => p.Side == side && !p.IsGoalie);
        var seats = BenchLayout.Seats(squad, others, b.PulledGoalie[(int)side], p => BenchLayout.OnBench(world, p));
        var incoming = b.Changing[(int)side].TryGet(out var change)
            ? change.Swaps.Where(s => !s.Done && !s.In.OnIce).Select(s => s.In).ToList()
            : [];
        int[] queued = new int[2];
        foreach (var p in world.Players)
        {
            if (p.Side != side) continue;
            bool on = p.OnIce, before = wasOnIce.GetValueOrDefault(p.Id, on);
            wasOnIce[p.Id] = on;
            if (!seats.TryGetValue(p.Id, out float seat))
            {
                // on the ice or in the box
                figures.Remove(p.Id);
                continue;
            }
            if (!figures.TryGetValue(p.Id, out var f))
            {
                // new on the bench: back from the ice at the door, or seated from the start
                f = new BenchFigure(p, before ? BenchLayout.DoorX(side) : seat, Phase(p)) { TurnRate = 2f + 3f * Hash01(p, 7) };
                f.Pose = before ? BenchPose.Return : BenchPose.Watch;
                figures[p.Id] = f;
            }
            float target = seat;
            if (incoming.Contains(p))
            {
                var group = Squad.GroupOf(p);
                target = BenchLayout.QueueX(side, group, queued[(int)group]++);
                f.Pose = BenchPose.Ready;
            }
            else if (f.Pose == BenchPose.Ready)
            {
                f.Pose = BenchPose.Return;   // the change stopped (icing holds the lines): back to his seat
            }
            Move(world, f, target, dt);
            shown.Add(f);
        }
    }

    void Move(World world, BenchFigure f, float target, float dt)
    {
        float gap = target - f.X, step = WalkSpeed * dt;
        if (MathF.Abs(gap) > step)
        {
            f.X += MathF.Sign(gap) * step;
            f.Walking = true;
            f.Heading = MathF.Sign(gap) * MathF.PI / 2f;   // walking along the bench
            return;
        }
        f.X = target;
        f.Walking = false;
        if (f.Pose == BenchPose.Return)
        {
            f.Pose = BenchPose.Watch;
            f.TiredUntil = world.Time + 4f + 2f * Hash01(f.Player, 11);   // sits tired for 4 to 6 s first
        }

        if (f.Player.IsGoalie)
        {
            f.Heading = MathF.Sign(BenchLayout.DoorX(f.Player.Side) - f.X) * MathF.PI / 2f;   // a goalie sits side on, his stick along the bench
            return;
        }

        // seated or waiting at the door: follow the puck, each at his own pace
        var puck = world.Puck.Position;
        float z = f.Standing ? BenchLayout.StandZ : BenchLayout.SeatZ;
        float want = Math.Clamp(MathF.Atan2(puck.X - f.X, puck.Z - z), -MaxTurn, MaxTurn);
        float turn = want - f.Heading;
        if (MathF.Abs(turn) > MathF.PI) f.Heading = want;   // straight from walking
        else f.Heading += turn * MathF.Min(1f, f.TurnRate * dt);
    }

    float Phase(Player p) => Hash01(p, 3) * 4f;

    /// <summary>A number in [0, 1) fixed for a player and a salt, from the variety seed.</summary>
    float Hash01(Player p, int salt)
    {
        uint h = (uint)seed ^ ((uint)p.Id * 0x9E3779B1u) ^ ((uint)salt * 0x85EBCA6Bu);
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
}
