using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>
/// W4c: the numbers behind a body check. Pure maths with no Raylib, so AI, fights, the referee
/// and tests can ask how hard a check would land without running a frame.
/// <list type="bullet">
/// <item>Pressing check lunges the skater forward; the first opponent within
/// <see cref="Contact"/> in front of them in the first <see cref="Window"/> seconds is hit.</item>
/// <item>How hard: the closing speed plus the lunge, scaled by the checker's checking trait, against
/// the target's resistance from their own checking trait (and puck control when they carry the puck).
/// Both scale down a little as energy runs out.</item>
/// <item>A hit of strength around 1 is an average solid check; the harder it is, the likelier the
/// target goes down. A target who goes down loses the puck and slides for a moment.</item>
/// </list>
/// </summary>
public static class CheckingModel
{
    /// <summary>Body centre to body centre at which a check connects, in metres.</summary>
    public const float Contact = 1.5f;

    /// <summary>Half the angle in front of the checker where a check can land.</summary>
    public const float Cone = 65f * MathF.PI / 180f;

    /// <summary>Seconds into the check during which it can connect.</summary>
    public const float Window = 0.3f;

    /// <summary>Seconds a check lasts, whiff or hit, before the next one.</summary>
    public const float Recover = 0.6f;

    /// <summary>The forward burst of the lunge, in metres per second.</summary>
    public const float Lunge = 2f;

    /// <summary>A hit at least this strong is a hard check: fights (W15) may start from one.</summary>
    public const float HardHit = 1.5f;

    /// <summary>The impact of an average checker hitting an average target at 5 m/s: strength 1.</summary>
    const float AverageImpact = (Lunge + 5f) * 1f;

    /// <summary>Gets how hard a checker hits: lunge plus closing speed, scaled by checking and energy.</summary>
    /// <param name="checker">The checker.</param>
    /// <param name="closing">The speed toward the target, in metres per second.</param>
    /// <returns>The impact.</returns>
    public static float Impact(Player checker, float closing)
    {
        return (Lunge + Math.Max(0, closing)) * (0.5f + Traits.Unit(checker.Data.Traits.Checking)) * SkatingModel.EnergyFactor(checker.Energy);
    }

    /// <summary>Gets how well a target takes a hit: from checking, plus puck control when carrying.</summary>
    /// <param name="target">The player hit.</param>
    /// <param name="carrying">Whether they carry the puck.</param>
    /// <returns>The resistance; 1 for an average fresh player without the puck.</returns>
    public static float Resistance(Player target, bool carrying)
    {
        var t = target.Data.Traits;
        float r = 0.5f + Traits.Unit(t.Checking);
        if (carrying) r += 0.3f * (Traits.Unit(t.PuckControl) - 0.5f);
        return r * SkatingModel.EnergyFactor(target.Energy);
    }

    /// <summary>Gets a hit's strength: about 1 for an average solid check, <see cref="HardHit"/> and up is hard.</summary>
    /// <param name="checker">The checker.</param>
    /// <param name="target">The player hit.</param>
    /// <param name="closing">The speed toward the target.</param>
    /// <param name="carrying">Whether the target carries the puck.</param>
    /// <returns>The strength.</returns>
    public static float Strength(Player checker, Player target, float closing, bool carrying)
    {
        return Impact(checker, closing) / Resistance(target, carrying) / AverageImpact;
    }

    /// <summary>Gets the chance a hit of some strength knocks the target down.</summary>
    /// <param name="strength">The hit's strength.</param>
    /// <returns>0 to 0.95.</returns>
    public static float KnockdownChance(float strength)
    {
        return Math.Clamp((strength - 0.5f) / 0.8f, 0f, 0.95f);
    }

    /// <summary>Gets how long a knocked-down player stays down, in seconds.</summary>
    /// <param name="strength">The hit's strength.</param>
    /// <returns>The time on the ice.</returns>
    public static float DownTime(float strength)
    {
        return 0.9f + 0.5f * Math.Clamp(strength - 1f, 0f, 1.2f);
    }

    /// <summary>Gets the shove a hit gives the target, in metres per second.</summary>
    /// <param name="strength">The hit's strength.</param>
    /// <returns>The speed added along the hit.</returns>
    public static float Shove(float strength)
    {
        return Math.Min(6f, 1.5f + 2f * strength);
    }

    /// <summary>Gets the opponent a check would hit now: the nearest one within reach in front of the checker.</summary>
    /// <param name="world">The match.</param>
    /// <param name="checker">The checker.</param>
    /// <returns>The target, or null.</returns>
    public static Player? Target(World world, Player checker)
    {
        var facing = PuckHandling.Intent(checker);
        Player? best = null;
        float bestDistance = Contact;
        foreach (var p in world.Players)
        {
            if (p.Side == checker.Side || !p.OnIce || p.IsGoalie || p.Action == PlayerAction.Fallen) continue;
            var off = new Vector2(p.Position.X - checker.Position.X, p.Position.Z - checker.Position.Z);
            float d = off.Length();
            if (d > bestDistance) continue;
            if (d > 0.01f && Vector2.Dot(off / d, facing) < SimMath.Cos(Cone)) continue;
            best = p;
            bestDistance = d;
        }
        return best;
    }
}
