using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

internal sealed partial class PositioningSystem
{
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
    static Maybe<int> Quickest(World world, Side side)
    {
        var best = Maybe<Player>.None;
        float bestTime = float.MaxValue;
        foreach (var p in world.OnIce(side))
        {
            if (p.IsGoalie || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            float t = TimeTo(p, world.Puck);
            if (t < bestTime) { bestTime = t; best = p; }
        }
        return best.TryGet(out var quickest) && quickest.Slot < 0 ? quickest.Id : Maybe<int>.None;
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
    static Maybe<int> Nearest(World world, Side side, Player carrier)
    {
        return world.OnIce(side).Where(p => !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight))
            .MinBy(p => Vector3.DistanceSquared(p.Position, carrier.Position)) is { Slot: < 0 } best ? best.Id : Maybe<int>.None;
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

    /// <summary>Until decisions take over, an AI carrier skates the puck into the slot, slipping
    /// sideways away from a defender in his path.</summary>
    static Vector2 CarryTarget(World world, Player p, int end)
    {
        float pa = end * p.Position.X, pz = p.Position.Z;
        var spot = pa > RinkSpec.GoalLineX - 9f
            ? new Vector2(RinkSpec.GoalLineX - 8f, pz > 0 ? -2.5f : 2.5f)
            : new Vector2(Math.Min(pa + 10f, RinkSpec.GoalLineX - 8f), pz * 0.5f);
        if (world.Players
            .Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie)
            .Select(o => (o, a: end * o.Position.X - pa, d: Vector3.Distance(o.Position, p.Position)))
            .Where(x => x.a > 0 && x.d < 5f)
            .OrderBy(x => x.d)
            .Select(x => x.o)
            .FirstOrDefault() is { } ahead)
        {
            spot.Y += ahead.Position.Z > pz ? -3f : 3f;
        }

        return TeamShape.Inside(new Vector2(end * spot.X, spot.Y));
    }
}
