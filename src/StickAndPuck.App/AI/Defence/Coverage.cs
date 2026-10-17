using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

/// <summary>Where the puck carrier is, seen from the defending side.</summary>
public enum DefenceZone
{
    /// <summary>The carrier is in the defending side's own zone.</summary>
    Own,

    /// <summary>Between the blue lines.</summary>
    Neutral,

    /// <summary>The carrier is deep in his own zone: the defending side forechecks.</summary>
    Forecheck,
}

/// <summary>The maths behind the defensive reads, with no state and no Raylib, so tests can ask where
/// a defender ought to stand. All spots are on the ice as (x, z); <c>net</c> is the net the
/// defending side protects.
/// <list type="bullet">
/// <item>Contain: the skater on the carrier stays goal side at a gap that closes the deeper the
/// carrier gets, stick on the puck, instead of skating through him.</item>
/// <item>Mark: every other defender takes an attacker, standing between him and the net and
/// leaning into the passing lane from the carrier. Dangerous men near the net are marked tight and
/// goal side; men far out are covered by sitting in the lane.</item>
/// <item>Home: the defenceman away from the puck stays in front of his net, goal side of whoever
/// is in the slot.</item>
/// </list></summary>
public static class Coverage
{
    /// <summary>How close to a passing lane a defender's body has to be to cover it, in metres.</summary>
    public const float LaneReach = 1.6f;

    /// <summary>How far a marker may leave his shape spot in the own zone, in metres.</summary>
    public const float OwnLeash = 1f;

    /// <summary>How far a marker may leave his shape spot between the blue lines, in metres.</summary>
    public const float NeutralLeash = 4.5f;

    /// <summary>About how many checks a side throws in a match.</summary>
    public const float HitsPerMatch = 25f;

    /// <summary>How far markers keep from the carrier, in metres.</summary>
    public const float CarrierRoom = 4.5f;

    const float G = RinkSpec.GoalLineX;
    const float Blue = RinkSpec.BlueLineX;

    /// <summary>Gets the net a side defends.</summary>
    public static Vector2 OwnNet(int attackEnd)
    {
        return new Vector2(-attackEnd * G, 0);
    }

    /// <summary>Gets the zone the carrier is in, as the defending side sees it.</summary>
    public static DefenceZone ZoneOf(int attackEnd, Vector2 carrier)
    {
        float a = attackEnd * carrier.X;
        return a switch
        {
            < -Blue => DefenceZone.Own,
            > Blue => DefenceZone.Forecheck,
            _ => DefenceZone.Neutral,
        };
    }

    /// <summary>Gets how dangerous an attacker without the puck is: high in the slot and in front of the net, low at the points and behind the net.</summary>
    public static float Danger(Vector2 net, Vector2 attacker)
    {
        float out_ = MathF.Sign(-net.X) * (attacker.X - net.X);   // metres out in front of the goal line
        if (out_ < 0.5f) return 0.1f;                              // behind the net
        float d = Vector2.Distance(net, attacker);
        float angle = SimMath.Atan2(MathF.Abs(attacker.Y), out_);
        float range = Math.Clamp(1f - (d - 2f) / 22f, 0f, 1f);
        float open = Math.Clamp(1f - angle / 1.4f, 0f, 1f);
        return 0.1f + range * (0.4f + 0.7f * open);
    }

    /// <summary>Gets the spot that marks an attacker: goal side of him, pulled toward the lane the carrier
    /// would pass along. The more dangerous the man, the tighter and more goal side the mark.</summary>
    public static Vector2 MarkSpot(DefenceZone zone, Vector2 net, Vector2 carrier, Vector2 attacker)
    {
        float danger = Danger(net, attacker);
        float toCarrier = Vector2.Distance(attacker, carrier);
        var goalSide = attacker + Dir(attacker, net) * (zone == DefenceZone.Own ? 1.1f : 2.6f);
        var inLane = attacker + Dir(attacker, carrier) * Math.Min(2.2f, toCarrier * 0.4f);
        float lean = zone == DefenceZone.Own ? Math.Clamp(0.85f - 0.55f * danger, 0.25f, 0.8f) : 0.45f;
        return TeamShape.Inside(SimMath.Lerp(goalSide, inLane, lean));
    }

    /// <summary>Gets a marker's spot kept within reach of his place in the team shape: he covers the man and
    /// the lane in his own area, but never leaves the slot open to follow a man across the ice.</summary>
    public static Vector2 Leash(Vector2 shape, Vector2 mark, DefenceZone zone)
    {
        float reach = zone == DefenceZone.Own ? OwnLeash : NeutralLeash;
        var off = mark - shape;
        float d = off.Length();
        return d <= reach ? mark : shape + off / d * reach;
    }

    /// <summary>Gets where the stay-home defenceman stands: in front of the net, goal side of the most dangerous man near it.</summary>
    public static Vector2 HomeSpot(Vector2 net, Vector2 carrier, Maybe<Vector2> threat)
    {
        float outward = MathF.Sign(-net.X);
        var crease = new Vector2(net.X + outward * 3.2f, Math.Clamp(carrier.Y * 0.15f, -1.2f, 1.2f));
        if (!threat.TryGet(out var t)) return crease;
        var spot = t + Dir(t, net) * 1.0f;
        // never further out than the hash marks, never inside the crease
        float o = outward * (spot.X - net.X);
        spot.X = net.X + outward * Math.Clamp(o, 2.2f, 5f);
        spot.Y = Math.Clamp(spot.Y, -2.5f, 2.5f);
        return spot;
    }

    /// <summary>Gets where the skater on the carrier stands while containing him: goal side, at a gap that
    /// shrinks as the carrier gets closer to the net, a little ahead of where he is going.</summary>
    public static Vector2 ContainSpot(DefenceZone zone, Vector2 net, Vector2 carrier)
    {
        float gap = zone switch { DefenceZone.Own => 1.2f, DefenceZone.Neutral => 2.6f, _ => 1.8f };
        return TeamShape.Inside(carrier + Dir(carrier, net) * gap, SkatingModel.BodyRadius + 0.2f);
    }

    /// <summary>Gets the chance the skater on the carrier plays the body on this approach, rather than only
    /// containing: likeliest on a carrier in front of the net, then along the boards and on the
    /// forecheck, rarely in open ice; a better checking trait raises it.</summary>
    public static float EngageChance(DefenceZone zone, Vector2 net, Vector2 carrier, int checking)
    {
        bool boards = MathF.Abs(carrier.Y) > RinkSpec.HalfWidth - 3.5f || MathF.Abs(carrier.X) > G - 1f;
        float where = zone switch
        {
            DefenceZone.Own => 0.08f + 0.45f * Math.Max(0f, Danger(net, carrier) - 0.45f),
            DefenceZone.Forecheck => 0.09f,
            _ => 0.04f,
        };
        return Math.Clamp(where * (boards ? 1.6f : 1f) * Traits.Scale(checking, 0.7f, 1.3f), 0f, 1f);
    }

    /// <summary>Gets how much a side's appetite for the body is scaled so a match ends with about
    /// <see cref="HitsPerMatch"/> hits: more willing when behind that pace, less when ahead.
    /// The ratio is cubed; a plain ratio settled near 18 hits a side, too weak to pull the
    /// engage odds up to the pace, so one match in ten fell under 15.</summary>
    public static float HitPace(int hits, float played)
    {
        float expected = HitsPerMatch * Math.Clamp(played, 0f, 1f);
        float behind = (expected + 3f) / (hits + 3f);
        return Math.Clamp(behind * behind * behind, 0.2f, 5f);
    }

    /// <summary>Keeps a spot at least <see cref="CarrierRoom"/> from the carrier, so only the man on the puck goes to him.</summary>
    public static Vector2 Room(Vector2 spot, Vector2 carrier)
    {
        var off = spot - carrier;
        float d = off.Length();
        if (d >= CarrierRoom) return spot;
        return TeamShape.Inside(carrier + (d > 1e-3f ? off / d : new Vector2(1, 0)) * CarrierRoom);
    }

    /// <summary>Gets whether a passing lane from the carrier to a teammate has a defender's body on it.</summary>
    /// <returns>True when a defender is within <see cref="LaneReach"/> of the lane, away from the passer's blade.</returns>
    public static bool LaneCovered(Vector2 carrier, Vector2 receiver, IEnumerable<Vector2> defenders)
    {
        float length = Vector2.Distance(carrier, receiver);
        if (length < 1.5f) return true;
        var from = carrier + Dir(carrier, receiver) * 1.2f;
        return defenders.Any(d => Choices.SegmentDistance(from, receiver, d) < LaneReach);
    }

    static Vector2 Dir(Vector2 from, Vector2 to)
    {
        var d = to - from;
        float l = d.Length();
        return l > 1e-4f ? d / l : Vector2.Zero;
    }
}
