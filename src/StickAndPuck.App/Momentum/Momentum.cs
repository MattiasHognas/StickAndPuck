using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Momentum;

/// <summary>What started a club's boost.</summary>
public enum MomentumCause
{
    /// <summary>No boost.</summary>
    None,

    /// <summary>A big hit: a check of strength 1.5 or more.</summary>
    BigHit,

    /// <summary>A fight won.</summary>
    FightWon,

    /// <summary>A goal scored.</summary>
    Goal,
}

/// <summary>One match's momentum: each club's boost, 0 (none) to 1 (just scored), and what started it.</summary>
public sealed class MomentumState
{
    /// <summary>Gets each club's boost, home then away, 0 to 1.</summary>
    public float[] Level { get; } = new float[2];

    /// <summary>Gets what started each club's boost.</summary>
    public MomentumCause[] Cause { get; } = new MomentumCause[2];

    /// <summary>Gets the match time each club's latest boost started.</summary>
    public float[] Since { get; } = [float.NegativeInfinity, float.NegativeInfinity];

    /// <summary>Gets how many boosts each club has had this match.</summary>
    public int[] Boosts { get; } = new int[2];

    /// <summary>Gets the energy each club's skaters have got back from their boosts this match.</summary>
    public float[] Energy { get; } = new float[2];

    /// <summary>Gets or sets how many times the crowd has swelled this match.</summary>
    public int Swells { get; set; }

    /// <summary>Gets or sets the volume of the latest crowd swell, 0 to 1.</summary>
    public float CrowdVolume { get; set; }
}

/// <summary>
/// W29: a short team boost after a goal, a fight won or a big hit. The boost tops up the energy of the
/// club's skaters on the ice (a second wind: skating and shots already read energy), drains over
/// about twenty seconds of live play, swells the crowd and shows on the HUD. A goal ends the other
/// club's boost. Others read it through <see cref="Of"/>.
/// </summary>
public static class Momentum
{
    static readonly ConditionalWeakTable<World, MomentumState> States = new();

    /// <summary>Gets a club's boost.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>0 (none) to 1.</returns>
    public static float Of(World world, Side side)
    {
        return States.TryGetValue(world, out var s) ? s.Level[(int)side] : 0f;
    }

    /// <summary>Gets the club with the bigger boost, or None when neither has one.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The side, or None.</returns>
    public static SideOrNone Leader(World world)
    {
        float home = Of(world, Side.Home), away = Of(world, Side.Away);
        if (home <= 0f && away <= 0f) return SideOrNone.None;
        return home >= away ? SideOrNone.Home : SideOrNone.Away;
    }

    /// <summary>Gets a match's momentum, made on first use.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The state.</returns>
    public static MomentumState State(World world)
    {
        return States.GetValue(world, _ => new MomentumState());
    }

    /// <summary>Gets the boost each cause gives.</summary>
    /// <param name="cause">The cause.</param>
    /// <returns>0 to 1.</returns>
    public static float Size(MomentumCause cause) => cause switch
    {
        MomentumCause.Goal => MomentumTuning.Goal,
        MomentumCause.FightWon => MomentumTuning.FightWon,
        MomentumCause.BigHit => MomentumTuning.BigHit,
        _ => 0f,
    };

    /// <summary>Gives a club a boost. A goal fills it and ends the other club's; a fight or a hit adds to it, up to full.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <param name="cause">What happened.</param>
    public static void Boost(World world, Side side, MomentumCause cause)
    {
        var s = State(world);
        int i = (int)side;
        float before = s.Level[i];
        s.Level[i] = cause == MomentumCause.Goal ? Math.Max(before, Size(cause)) : Math.Min(1f, before + Size(cause));
        if (cause == MomentumCause.Goal) s.Level[1 - i] = 0f;
        if (before <= 0f || cause >= s.Cause[i]) s.Cause[i] = cause;
        s.Since[i] = world.Time;
        s.Boosts[i]++;
    }
}

/// <summary>W29: the momentum numbers.</summary>
public static class MomentumTuning
{
    /// <summary>The boost a goal gives: full.</summary>
    public const float Goal = 1f;

    /// <summary>The boost a fight won gives.</summary>
    public const float FightWon = 0.7f;

    /// <summary>The boost a big hit adds.</summary>
    public const float BigHit = 0.35f;

    /// <summary>The check strength that counts as a big hit (the same 1.5 that can start a fight).</summary>
    public const float BigHitStrength = 1.5f;

    /// <summary>Seconds of live play a full boost lasts.</summary>
    public const float Seconds = 20f;

    /// <summary>Energy per second a full boost gives each skater on the ice; about three times what skating drains.</summary>
    public const float Recover = 0.05f;

    /// <summary>Seconds between crowd swells while a boost lasts.</summary>
    public const float SwellGap = 2.5f;

    /// <summary>The crowd's volume at a full boost, before the player's volume; W12 plays a goal's crowd at 0.35.</summary>
    public const float CrowdVolume = 0.9f;

    /// <summary>The boost below which the crowd stops swelling.</summary>
    public const float SwellBelow = 0.25f;
}
