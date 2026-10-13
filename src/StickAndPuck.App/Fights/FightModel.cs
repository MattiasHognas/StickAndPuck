using StickAndPuck.App.Core;

namespace StickAndPuck.App.Fights;

/// <summary>The numbers of a fight, as pure functions of the fighters' traits, so tests and the AI share them.</summary>
public static class FightModel
{
    /// <summary>
    /// Gets the chance an AI skater accepts a fight: tough checkers say yes more often, a club behind
    /// wants a spark, a club well ahead stays out of the box.
    /// </summary>
    /// <param name="world">The match, for the score.</param>
    /// <param name="p">The skater asked.</param>
    /// <returns>A chance from 0.05 to 0.9.</returns>
    public static float AcceptChance(World world, Player p)
    {
        float chance = 0.15f + (0.6f * Traits.Unit(p.Data.Traits.Checking));
        int own = world.Match.Score[(int)p.Side], other = world.Match.Score[1 - (int)p.Side];
        if (own < other) chance += 0.15f;
        else if (own - other >= 2) chance -= 0.2f;
        return Math.Clamp(chance, 0.05f, 0.9f);
    }

    /// <summary>Gets how well a punch was timed: 1 in the middle of the bar, 0 at the edge of the sweet spot, below 0 a miss.</summary>
    /// <param name="sweep">Where the marker was when the punch was thrown, 0 to 1.</param>
    /// <returns>The timing quality.</returns>
    public static float Quality(float sweep)
    {
        return 1f - (MathF.Abs(sweep - 0.5f) / FightTuning.SweetHalf);
    }

    /// <summary>Gets the balance a landed punch takes: better timing and a heavier hitter hurt more, a tougher target takes less.</summary>
    /// <param name="puncher">Who threw it.</param>
    /// <param name="target">Who took it.</param>
    /// <param name="quality">Its timing, from <see cref="Quality"/>, above 0.</param>
    /// <returns>Balance lost.</returns>
    public static float Damage(Player puncher, Player target, float quality)
    {
        float power = 0.8f + (0.4f * Traits.Unit(puncher.Data.Traits.Checking));
        float toughness = 0.8f + (0.4f * Traits.Unit(target.Data.Traits.Checking));
        return FightTuning.Damage * (0.5f + (0.5f * Math.Clamp(quality, 0f, 1f))) * power / toughness;
    }

    /// <summary>Gets where on the bar an AI fighter throws this exchange's punch: around the middle, closer for better checkers.</summary>
    /// <param name="p">The fighter.</param>
    /// <param name="rng">Dice.</param>
    /// <returns>The sweep position, 0 to 1.</returns>
    public static float AiPunchAt(Player p, Random rng)
    {
        float spread = 0.24f - (0.14f * Traits.Unit(p.Data.Traits.Checking));
        float noise = rng.NextSingle() + rng.NextSingle() + rng.NextSingle() - 1.5f;     // roughly normal, -1.5 to 1.5
        return Math.Clamp(0.5f + (noise * spread), 0.02f, 0.98f);
    }

    /// <summary>Gets the chance an AI fighter sees a wind-up in time and gets the guard up.</summary>
    /// <param name="p">The fighter.</param>
    /// <returns>A chance.</returns>
    public static float AiBlockChance(Player p)
    {
        return 0.25f + (0.25f * Traits.Unit(p.Data.Traits.Checking));
    }
}
