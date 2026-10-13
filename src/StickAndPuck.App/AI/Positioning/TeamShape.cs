using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>Whether a side is attacking (it has the puck, or touched it last) or defending.</summary>
public enum TeamPhase
{
    /// <summary>Our puck: spread out, support the carrier, stay onside.</summary>
    Attack,

    /// <summary>Their puck: get goal side, protect the slot, cover the points.</summary>
    Defend,
}

/// <summary>The ideal spot for each role, from where the puck is. Pure maths with no state and no
/// Raylib, so decisions and tests can ask where a skater ought to be. All spots are on the
/// ice as (x, z). Inside the maths, "a" runs toward the net a side attacks (a = end * x), so one
/// set of numbers serves both ends.
/// <list type="bullet">
/// <item>Attack: forwards ahead of the puck in three lanes and never over the blue line before the
/// puck (onside); in the zone the strong side wing goes low, the weak side wing to the back door,
/// the centre to the high slot and the defence to the points.</item>
/// <item>Defence: everyone goal side of the puck; in the zone the strong side defenceman takes the
/// puck side of the net, the other the net front, the centre the low slot and the wings the points.</item>
/// </list></summary>
public static class TeamShape
{
    /// <summary>How far spots stay from the boards.</summary>
    public const float BoardMargin = 1.4f;

    const float G = RinkSpec.GoalLineX;
    const float Blue = RinkSpec.BlueLineX;

    /// <summary>Gets the side of the ice (z sign) a role plays on, for a side attacking an end: left is -z when attacking +x.</summary>
    public static float Lateral(Role role, int end)
    {
        return role switch
        {
            Role.LeftWing or Role.LeftDefence => -end,
            Role.RightWing or Role.RightDefence => end,
            _ => 0,
        };
    }

    /// <summary>Gets the ideal spot for a role.</summary>
    public static Vector2 Spot(Role role, TeamPhase phase, int end, Vector2 puck)
    {
        float pa = end * puck.X, pz = puck.Y;
        float s = Lateral(role, end);
        bool strong = s * pz >= 0;
        var (a, z) = phase == TeamPhase.Attack ? Attack(role, pa, pz, s, strong) : Defend(role, pa, pz, s, strong);
        return Inside(new Vector2(end * a, z));
    }

    /// <summary>Gets where a role lines up for a faceoff on a dot, for the side attacking an end.</summary>
    public static Vector2 Faceoff(Role role, int end, Vector2 dot)
    {
        float da = end * dot.X, dz = dot.Y;
        float s = Lateral(role, end);
        var (a, z) = role switch
        {
            Role.Centre => (da - 0.8f, dz),
            Role.LeftWing or Role.RightWing => (da - 1.5f, dz + s * 5.5f),
            _ => (Math.Max(da - 8f, -G + 2.5f), dz * 0.5f + s * 3.5f),
        };
        return Inside(new Vector2(end * a, z));
    }

    /// <summary>Keeps a spot on the ice: in front of both goal lines and clear of the boards and their rounded corners.</summary>
    public static Vector2 Inside(Vector2 p, float margin = BoardMargin)
    {
        float hx = RinkSpec.HalfLength - margin, hz = RinkSpec.HalfWidth - margin, r = RinkSpec.CornerRadius - margin;
        float x = Math.Clamp(p.X, -hx, hx), z = Math.Clamp(p.Y, -hz, hz);
        float cx = hx - r, cz = hz - r;
        if (MathF.Abs(x) > cx && MathF.Abs(z) > cz)
        {
            var corner = new Vector2(MathF.Sign(x) * cx, MathF.Sign(z) * cz);
            var off = new Vector2(x, z) - corner;
            if (off.Length() > r) off = Vector2.Normalize(off) * r;
            return corner + off;
        }
        return new Vector2(x, z);
    }

    static (float A, float Z) Attack(Role role, float pa, float pz, float s, bool strong)
    {
        bool inZone = pa > Blue + 1f;
        float a, z;
        switch (role)
        {
            case Role.Centre:
                (a, z) = inZone ? (G - 9.5f, pz * 0.25f) : (Math.Clamp(pa + 2.5f, -G + 6f, G - 9.5f), pz * 0.35f);
                break;
            case Role.LeftWing or Role.RightWing:
                if (inZone) (a, z) = strong ? (G - 4.5f, s * 6f) : (G - 3.5f, s * 2.8f);
                else (a, z) = (Math.Clamp(pa + 6f, -G + 8f, G - 6f), s * 8f + pz * 0.15f);
                break;
            default:
                if (inZone) (a, z) = (Blue + 1.6f, s * 5.5f + pz * 0.2f);
                else (a, z) = (Math.Max(Math.Min(Math.Clamp(pa - 7f, -G + 4f, Blue - 1.5f), pa - 3f), -G + 2.5f), s * 3.5f + pz * 0.25f);
                return (a, z);
        }

        // forwards wait at the blue line until the puck is in the zone, so they stay onside
        if (pa <= Blue) a = Math.Min(a, Blue - 0.8f);
        return (a, z);
    }

    static (float A, float Z) Defend(Role role, float pa, float pz, float s, bool strong)
    {
        bool inZone = pa < -Blue - 1f;
        switch (role)
        {
            case Role.Centre:
                return inZone ? (-G + 6.5f, pz * 0.3f) : (Math.Clamp(pa - 3f, -G + 6.5f, G - 10f), pz * 0.5f);
            case Role.LeftWing or Role.RightWing:
                return inZone ? (-Blue - 1.5f, s * 6.5f + pz * 0.15f) : (Math.Clamp(pa - 1.5f, -G + 9f, G - 9f), s * 7f + pz * 0.1f);
            default:
                if (!inZone) return (Math.Max(Math.Min(Math.Clamp(pa - 6f, -G + 4f, Blue), pa - 2.5f), -G + 2.5f), s * 3.5f + pz * 0.35f);
                if (!strong) return (-G + 3.5f, s * 1.6f);
                // between the puck and the near post, a little nearer the puck
                var net = new Vector2(-G + 1.5f, s * 0.6f);
                var spot = Vector2.Lerp(net, new Vector2(pa, pz), 0.55f);
                return (spot.X, spot.Y);
        }
    }
}
