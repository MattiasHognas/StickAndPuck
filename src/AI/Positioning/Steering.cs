using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;

namespace StickAndPuck.AI;

/// <summary>
/// W6a: turns "skate to this spot" into a stick direction for <see cref="PlayerCommand.Move"/>,
/// using the skater's own top speed, so it works with W4a's skating rather than moving anyone
/// directly. The skater heads for the spot, eases off inside the arrival radius, steers out any
/// sideways drift, and digs in for a hockey stop when it overshoots. Pure maths, no state.
/// </summary>
public static class Steering
{
    /// <summary>Distance inside which a skater counts as on its spot and stops pushing.</summary>
    public const float OnSpot = 0.35f;

    /// <summary>The default distance over which a skater eases off toward a spot.</summary>
    public const float Arrive = 2.5f;

    /// <summary>Spots further than this are worth a sprint when sprinting is allowed.</summary>
    public const float SprintFrom = 8f;

    const float Correct = 0.8f;   // how hard the stick fights drift away from the wanted velocity
    const float Dead = 0.12f;     // stick lengths below this are let go, so a skater on its spot glides still

    /// <summary>Gets the command that takes a skater toward a spot.</summary>
    /// <param name="p">The skater.</param>
    /// <param name="target">The spot on the ice (x, z).</param>
    /// <param name="arrive">The distance over which to ease off; smaller arrives at full speed.</param>
    /// <param name="sprint">Whether the skater may sprint when far away.</param>
    /// <returns>A command with only Move and Sprint set.</returns>
    public static PlayerCommand Toward(Player p, Vector2 target, float arrive = Arrive, bool sprint = false)
    {
        var pos = new Vector2(p.Position.X, p.Position.Z);
        var v = new Vector2(p.Velocity.X, p.Velocity.Z);
        var to = target - pos;
        float d = to.Length();
        if (d < OnSpot && v.Length() < 0.6f) return default;

        bool flatOut = sprint && d > SprintFrom;
        float top = SkatingModel.TopSpeed(p.Data.Traits, p.Energy) * (flatOut ? 1.15f : 1f);
        var want = d > 1e-4f ? to / d * top * Math.Min(1f, d / Math.Max(arrive, 0.01f)) : Vector2.Zero;
        var move = (want + (want - v) * Correct) / top;
        float len = move.Length();
        if (len > 1f) move /= len;
        else if (len < Dead) move = Vector2.Zero;
        return new PlayerCommand { Move = move, Sprint = flatOut };
    }

    /// <summary>
    /// Gets where to skate first on the way to a spot so the skater goes round a net rather than
    /// into its cage: the spot itself when the way is clear, otherwise a point beside the nearer post
    /// (<see cref="Cycle.AroundNet"/>), checked for the net at his end of the rink first.
    /// </summary>
    /// <param name="from">The skater (x, z).</param>
    /// <param name="to">The spot (x, z).</param>
    /// <returns>The spot, or the point to skate to first (x, z).</returns>
    public static Vector2 AroundNets(Vector2 from, Vector2 to)
    {
        int near = from.X >= 0 ? 1 : -1;
        var way = Cycle.AroundNet(from, to, near);
        return way != to ? way : Cycle.AroundNet(from, to, -near);
    }

    /// <summary>Gets the point a skater should aim its body at so its blade meets a point, facing it from where the skater is.</summary>
    /// <param name="p">The skater.</param>
    /// <param name="point">Where the blade should be (x, z).</param>
    /// <returns>Where the body should go (x, z).</returns>
    public static Vector2 BodyForBlade(Player p, Vector2 point)
    {
        var to = point - new Vector2(p.Position.X, p.Position.Z);
        float heading = to.LengthSquared() > 1e-4f ? MathF.Atan2(to.X, to.Y) : p.Heading;
        var blade = PuckHandling.Blade(Vector3.Zero, heading);
        return point - new Vector2(blade.X, blade.Z);
    }
}
