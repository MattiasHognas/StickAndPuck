using StickAndPuck.App.Core;

namespace StickAndPuck.App.Benches;

/// <summary>The figures in the penalty boxes behind the near boards (arena.glb's PenaltyBox part):
/// each club's skaters off the ice and in the box, not ejected, sitting side by side on its seat
/// out from the door. A skater who skates in appears at the door and walks to his seat; seated,
/// he watches the puck, as on the bench, facing the other way.</summary>
public sealed partial class BenchCrowd
{
    readonly List<BenchFigure> boxed = [];
    readonly List<BenchFigure> boxShown = [];

    /// <summary>Gets the figures in the penalty boxes this step.</summary>
    public IReadOnlyList<BenchFigure> InBoxes => boxShown;

    void Box(World world, Side side, float dt)
    {
        int seat = 0;
        boxed.RemoveAll(f => f.Player.Side == side && !Sits(world, f.Player));
        foreach (var p in world.Players)
        {
            if (p.Side != side || !Sits(world, p) || boxed.Exists(f => f.Player == p)) continue;
            bool skatedIn = cameOff.GetValueOrDefault(p.Id);
            var f = new BenchFigure(p, BenchLayout.BoxDoorX(side), Phase(p)) { TurnRate = 2f + 3f * Hash01(p, 7), InBox = true, Heading = MathF.PI };
            f.Pose = skatedIn ? BenchPose.Return : BenchPose.Watch;
            f.Spent = skatedIn ? p.Energy : 1f;
            if (!skatedIn) f.X = BenchLayout.BoxSeatX(side, boxed.Count(o => o.Player.Side == side));
            boxed.Add(f);
        }
        // in the order they came in: the first one farthest along the seat
        foreach (var f in boxed.Where(f => f.Player.Side == side))
        {
            f.SatDown = false;
            BoxMove(world, f, BenchLayout.BoxSeatX(side, seat++), dt);
            boxShown.Add(f);
        }
    }

    void BoxMove(World world, BenchFigure f, float target, float dt)
    {
        float gap = target - f.X, step = WalkSpeed * dt;
        if (MathF.Abs(gap) > step)
        {
            f.X += MathF.Sign(gap) * step;
            f.Walking = true;
            f.Heading = MathF.Sign(gap) * MathF.PI / 2f;
            return;
        }
        f.X = target;
        f.Walking = false;
        if (f.Pose == BenchPose.Return)
        {
            f.Pose = BenchPose.Watch;
            f.SatDown = true;
            f.TiredUntil = world.Time + 4f + 2f * Hash01(f.Player, 11);
        }

        // seated: follow the puck, turned from facing the ice (heading pi) at most as far as on the bench
        var puck = world.Puck.Position;
        float rel = Wrap(MathF.Atan2(puck.X - f.X, puck.Z - BenchLayout.BoxSeatZ) - MathF.PI);
        float want = Math.Clamp(rel, -MaxTurn, MaxTurn);
        float now = Wrap(f.Heading - MathF.PI);
        f.Heading = MathF.PI + now + ((want - now) * MathF.Min(1f, f.TurnRate * dt));
    }

    /// <summary>Gets whether a player sits in his club's box: off the ice, in the box and not thrown out of the match.</summary>
    static bool Sits(World world, Player p) => !p.IsGoalie && !p.OnIce && Rules.Penalties.InBox(world, p.Id) && !Rules.Penalties.Ejected(world, p.Id);

    static float Wrap(float a)
    {
        while (a > MathF.PI) a -= 2 * MathF.PI;
        while (a < -MathF.PI) a += 2 * MathF.PI;
        return a;
    }
}
