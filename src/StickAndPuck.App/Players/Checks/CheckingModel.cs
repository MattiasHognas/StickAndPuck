using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>The numbers behind a body check. Pure maths with no Raylib, so AI, fights, the referee
/// and tests can ask how hard a check would land without running a frame.
/// <list type="bullet">
/// <item>Pressing check lunges the skater at the opponent they would hit (an opponent within
/// <see cref="Contact"/> not behind them, so one directly alongside counts), else forward; the first
/// one in reach in the first <see cref="Window"/> seconds is hit. A press late in the recover is
/// remembered and starts the next check as soon as it can (<see cref="Buffer"/>).</item>
/// <item>How hard: the closing speed plus the lunge, scaled by the checker's checking trait, against
/// the target's resistance from their own checking trait (and puck control when they carry the puck).
/// Both scale down a little as energy runs out.</item>
/// <item>A hit of strength around 1 is an average solid check; the harder it is, the likelier the
/// target goes down. A target who goes down loses the puck and slides for a moment.</item>
/// </list></summary>
public static class CheckingModel
{
    /// <summary>Body centre to body centre at which a check connects, in metres (but see <see cref="Meet"/>).</summary>
    public const float Contact = 2.2f;

    /// <summary>Body centre to body centre at which bodies coming together meet. A check started at someone
    /// closing on the checker fast enough to get this near within <see cref="Window"/> (a carrier
    /// rushing at a defender) lands when they meet rather than at full reach, which leaves a
    /// carrier's deke its moment to beat it; see <see cref="Waits"/>.</summary>
    public const float Meet = 1.5f;

    /// <summary>Half the angle in front of the checker where a check can land: wider than a right angle, so a skater alongside can be hit.</summary>
    public const float Cone = 100f * MathF.PI / 180f;

    /// <summary>How far an AI skater's check reaches: <see cref="Contact"/> and <see cref="Cone"/>, whatever the difficulty.</summary>
    public static readonly CheckReach AiReach = new(Contact, Cone);

    /// <summary>How far a human's check reaches at each difficulty: Easy as far as the AI's, Hard asks for a square, close hit.</summary>
    static readonly CheckReach EasyReach = new(2.2f, 100f * MathF.PI / 180f), NormalReach = new(1.9f, 90f * MathF.PI / 180f), HardReach = new(1.6f, 65f * MathF.PI / 180f);

    /// <summary>Metres a target's angle off the checker's way adds to their distance when picking whom to hit, per unit of 1 - cos(angle): one alongside counts as this much further.</summary>
    public const float AnglePenalty = 1f;

    /// <summary>Seconds before a check ends in which a press is kept and starts the next check, so mashing check is not lost in the recover.</summary>
    public const float Buffer = 0.15f;

    /// <summary>Seconds into the check during which it can connect.</summary>
    public const float Window = 0.3f;

    /// <summary>Seconds a check lasts, whiff or hit, before the next one.</summary>
    public const float Recover = 0.6f;

    /// <summary>The burst of the lunge, at the target when there is one in reach, in metres per second.</summary>
    public const float Lunge = 3f;

    /// <summary>A hit at least this strong is a hard check: fights may start from one.</summary>
    public const float HardHit = 1.5f;

    /// <summary>The impact of strength 1: an average checker closing at 4 m/s, lunge included. Kept at what it
    /// was before the lunge grew, so the bigger lunge makes checks harder rather than the scale moving with it.</summary>
    const float AverageImpact = 7f;

    /// <summary>Gets how hard a checker hits: lunge plus closing speed, scaled by checking and energy.</summary>
    public static float Impact(Player checker, float closing)
    {
        return (Lunge + Math.Max(0, closing)) * (0.5f + Traits.Unit(checker.Data.Traits.Checking)) * SkatingModel.EnergyFactor(checker.Energy);
    }

    /// <summary>Gets how well a target takes a hit: from checking, plus puck control when carrying.</summary>
    public static float Resistance(Player target, bool carrying)
    {
        var t = target.Data.Traits;
        float r = 0.5f + Traits.Unit(t.Checking);
        if (carrying) r += 0.3f * (Traits.Unit(t.PuckControl) - 0.5f);
        return r * SkatingModel.EnergyFactor(target.Energy);
    }

    /// <summary>Gets a hit's strength: about 1 for an average solid check, <see cref="HardHit"/> and up is hard.</summary>
    public static float Strength(Player checker, Player target, float closing, bool carrying)
    {
        return Impact(checker, closing) / Resistance(target, carrying) / AverageImpact;
    }

    /// <summary>Gets how a check's strength is scaled: 1 for an AI checker, for a human by the difficulty (Easy hits harder, Hard softer).</summary>
    public static float StrengthScale(Player checker, Difficulty difficulty)
    {
        if (checker.Slot < 0) return 1f;
        return difficulty switch
        {
            Difficulty.Easy => 1.1f,
            Difficulty.Hard => 0.85f,
            _ => 1f,
        };
    }

    /// <summary>Gets whether a check started at a target waits for the bodies to meet: when they are further apart than <see cref="Meet"/> but closing fast enough to get there within <see cref="Window"/>.</summary>
    public static bool Waits(float distance, float closing)
    {
        return distance > Meet && closing * Window >= distance - Meet;
    }

    /// <summary>Gets the chance a hit of some strength knocks the target down.</summary>
    public static float KnockdownChance(float strength)
    {
        return KnockdownChance(strength, AI.HumanEdgeTuning.KnockdownCap);
    }

    /// <summary>Gets the chance a hit of some strength knocks the target down, never above <paramref name="cap"/>.</summary>
    public static float KnockdownChance(float strength, float cap)
    {
        return Math.Clamp((strength - 0.5f) / 0.8f, 0f, cap);
    }

    /// <summary>Gets how long a knocked-down player stays down, in seconds.</summary>
    public static float DownTime(float strength)
    {
        return 0.9f + 0.5f * Math.Clamp(strength - 1f, 0f, 1.2f);
    }

    /// <summary>Gets the shove a hit gives the target, in metres per second.</summary>
    public static float Shove(float strength)
    {
        return Math.Min(6f, 1.5f + 2f * strength);
    }

    /// <summary>Gets how far a check reaches: <see cref="AiReach"/> for an AI checker, the difficulty's for a human.</summary>
    public static CheckReach Reach(Player checker, Difficulty difficulty)
    {
        if (checker.Slot < 0) return AiReach;
        return difficulty switch
        {
            Difficulty.Easy => EasyReach,
            Difficulty.Hard => HardReach,
            _ => NormalReach,
        };
    }

    /// <summary>Gets the opponent a check would hit now: one within reach (<see cref="Reach"/>) and not
    /// behind the checker (within its cone of the way they mean), the nearest counting their angle off that way
    /// (<see cref="AnglePenalty"/>), so the one most in front and nearest is picked.</summary>
    public static Maybe<Player> Target(World world, Player checker)
    {
        var facing = PuckHandling.Intent(checker);
        var reach = Reach(checker, world.Settings.Difficulty);
        float cone = SimMath.Cos(reach.Cone);
        var best = Maybe<Player>.None;
        float bestScore = float.MaxValue;
        foreach (var p in world.Players)
        {
            if (p.Side == checker.Side || !p.OnIce || p.IsGoalie || p.Action == PlayerAction.Fallen) continue;
            var off = new Vector2(p.Position.X - checker.Position.X, p.Position.Z - checker.Position.Z);
            float d = off.Length();
            if (d > reach.Contact) continue;
            float cos = d > 0.01f ? Vector2.Dot(off / d, facing) : 1f;
            if (cos < cone) continue;
            float score = d + (AnglePenalty * (1f - cos));
            if (score >= bestScore) continue;
            best = p;
            bestScore = score;
        }
        return best;
    }
}

/// <summary>How far a check reaches.</summary>
/// <param name="Contact">Body centre to body centre at which it connects, in metres.</param>
/// <param name="Cone">Half the angle around the checker's way where it can land, in radians.</param>
public readonly record struct CheckReach(float Contact, float Cone);
