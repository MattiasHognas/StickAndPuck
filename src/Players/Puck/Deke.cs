using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>
/// W32: the numbers of a deke, with no Raylib and no state, so AI and tests can ask what a deke
/// would do. A deke is a quick sidestep with the puck pulled across the body. Pressed while a
/// defender closes in front (the sweet spot is <see cref="SweetNear"/> to <see cref="SweetFar"/>
/// metres), it usually sells the fake: the defender bites, lunging where the puck was, and the
/// carrier slips past. Pressed too early or too late it usually fails and the puck comes loose.
/// Puck control raises the odds either way. Auto-deke (<see cref="Settings.AutoDeke"/>) dekes for
/// the human when a defender reaches the sweet spot, at worse odds than a well-timed press, so
/// deking by hand stays the better way.
/// </summary>
public static class DekeModel
{
    /// <summary>A defender further away than this, in metres, is nobody to beat.</summary>
    public const float Range = 3.6f;

    /// <summary>The near edge of the sweet spot, in metres between the bodies.</summary>
    public const float SweetNear = 1.3f;

    /// <summary>The far edge of the sweet spot, in metres between the bodies.</summary>
    public const float SweetFar = 2.6f;

    /// <summary>A defender this close has already closed the gap: no timing left.</summary>
    public const float TooClose = 0.6f;

    /// <summary>Seconds the sidestep lasts.</summary>
    public const float Duration = 0.3f;

    /// <summary>Seconds after a deke before the same player can deke again.</summary>
    public const float Cooldown = 0.9f;

    /// <summary>A defender within this angle of the way the carrier is going is in front, in radians.</summary>
    public const float FrontCone = 75f * MathF.PI / 180f;

    /// <summary>Gets the way a player is going: their motion when skating, else the way they face.</summary>
    /// <param name="p">The player.</param>
    /// <returns>A unit vector on the ice (x, z).</returns>
    public static Vector2 Travel(Player p)
    {
        var v = new Vector2(p.Velocity.X, p.Velocity.Z);
        return v.Length() > 1f ? Vector2.Normalize(v) : PuckHandling.Facing(p);
    }

    /// <summary>
    /// Finds the defender a deke would beat: the nearest opponent skater on the ice, on their feet,
    /// within <see cref="Range"/> and in front of the way the carrier is going.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="carrier">The puck carrier.</param>
    /// <returns>The defender and the distance between the bodies, or null.</returns>
    public static (Player Defender, float Distance)? Threat(World world, Player carrier)
    {
        var travel = Travel(carrier);
        (Player, float)? best = null;
        float bestDistance = Range;
        foreach (var p in world.Players)
        {
            if (p.Side == carrier.Side || !p.OnIce || p.IsGoalie || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            var off = new Vector2(p.Position.X - carrier.Position.X, p.Position.Z - carrier.Position.Z);
            float d = off.Length();
            if (d > bestDistance) continue;
            if (d > 0.01f && Vector2.Dot(off / d, travel) < SimMath.Cos(FrontCone)) continue;
            best = (p, d);
            bestDistance = d;
        }
        return best;
    }

    /// <summary>Gets how well timed a deke is against a defender at a distance: 1 in the sweet spot, falling to 0 at <see cref="TooClose"/> and <see cref="Range"/>.</summary>
    /// <param name="distance">Metres between the bodies.</param>
    /// <returns>0 to 1.</returns>
    public static float Timing(float distance)
    {
        if (distance <= TooClose || distance >= Range) return 0f;
        if (distance < SweetNear) return (distance - TooClose) / (SweetNear - TooClose);
        if (distance > SweetFar) return (Range - distance) / (Range - SweetFar);
        return 1f;
    }

    /// <summary>Gets the chance a pressed deke beats the defender: mostly timing, then puck control.</summary>
    /// <param name="timing">How well timed, 0 to 1 (see <see cref="Timing"/>).</param>
    /// <param name="puckControl">The carrier's puck control.</param>
    /// <returns>0 to 1.</returns>
    public static float Chance(float timing, int puckControl)
    {
        float pc = Traits.Unit(puckControl);
        float timed = 0.75f + 0.25f * pc, mistimed = 0.1f + 0.3f * pc;
        return Math.Clamp(timing * timed + (1f - timing) * mistimed, 0.02f, 0.97f);
    }

    /// <summary>Gets the chance an auto-deke beats the defender: well below a well-timed press, so deking by hand stays the better way.</summary>
    /// <param name="puckControl">The carrier's puck control.</param>
    /// <returns>0 to 1.</returns>
    public static float AutoChance(int puckControl)
    {
        return 0.3f + 0.3f * Traits.Unit(puckControl);
    }

    /// <summary>Gets how fast the carrier steps sideways during a deke, in metres per second: wider with better puck control, half as wide when it fails.</summary>
    /// <param name="puckControl">The carrier's puck control.</param>
    /// <param name="beaten">Whether the deke beat the defender.</param>
    /// <returns>Metres per second across the carrier's path.</returns>
    public static float StepSpeed(int puckControl, bool beaten)
    {
        float s = 3f + 1.5f * Traits.Unit(puckControl);
        return beaten ? s : s * 0.5f;
    }

    /// <summary>Gets the side a carrier steps to: away from the defender, or toward the backhand when the defender is dead ahead.</summary>
    /// <param name="carrier">The carrier.</param>
    /// <param name="defender">The defender, or null.</param>
    /// <returns>A unit vector across the carrier's path.</returns>
    public static Vector2 StepSide(Player carrier, Player? defender)
    {
        var travel = Travel(carrier);
        var left = new Vector2(-travel.Y, travel.X);
        if (defender != null)
        {
            var off = new Vector2(defender.Position.X - carrier.Position.X, defender.Position.Z - carrier.Position.Z);
            float across = Vector2.Dot(off, left);
            if (MathF.Abs(across) > 0.15f) return across > 0 ? -left : left;
        }
        // the blade sits on the right of the body (see PuckHandling.Blade): pull it across to the left
        return left;
    }
}
