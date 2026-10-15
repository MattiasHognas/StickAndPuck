using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>How the difficulty eases the AI club's play against the human's carrier: softer checks, fewer
/// knockdowns, fewer hits tried, and a mistimed deke that still gets through more often. Only in a match
/// with a single AI club facing a human (<see cref="DifficultyScale.AiSide"/>), and only on the skater the
/// human controls; versus and all-AI matches play as they always did.</summary>
public static class HumanEdge
{
    /// <summary>Gets the difficulty easing the AI's play on this skater: the match's difficulty when the skater is
    /// human-controlled and plays against the AI club, or None.</summary>
    public static Maybe<Difficulty> Against(World world, Player target)
    {
        if (target.Slot < 0 || !DifficultyScale.AiSide(world.Setup).TryGetSide(out var ai) || target.Side == ai) return Maybe<Difficulty>.None;
        return world.Settings.Difficulty;
    }

    /// <summary>Gets how hard an AI check lands on the human's skater, as a share of its full strength.</summary>
    public static float CheckStrength(World world, Player checker, Player target)
    {
        if (checker.Slot >= 0 || !Against(world, target).TryGet(out var level)) return 1f;
        return level switch
        {
            Difficulty.Easy => HumanEdgeTuning.CheckEasy,
            Difficulty.Normal => HumanEdgeTuning.CheckNormal,
            _ => 1f,
        };
    }

    /// <summary>Gets the most a hit can knock the human's skater down, as a chance.</summary>
    public static float KnockdownCap(World world, Player target)
    {
        if (!Against(world, target).TryGet(out var level)) return HumanEdgeTuning.KnockdownCap;
        return level switch
        {
            Difficulty.Easy => HumanEdgeTuning.KnockdownCapEasy,
            Difficulty.Normal => HumanEdgeTuning.KnockdownCapNormal,
            _ => HumanEdgeTuning.KnockdownCap,
        };
    }

    /// <summary>Gets the share of its usual chance an AI skater pressing the human's carrier decides to hit.</summary>
    public static float HitChance(World world, Player carrier)
    {
        if (!Against(world, carrier).TryGet(out var level)) return 1f;
        return level switch
        {
            Difficulty.Easy => HumanEdgeTuning.HitEasy,
            Difficulty.Normal => HumanEdgeTuning.HitNormal,
            _ => 1f,
        };
    }

    /// <summary>Gets the least chance a mistimed deke by the human's carrier beats the defender, or 0.</summary>
    public static float DekeFloor(World world, Player carrier)
    {
        if (!Against(world, carrier).TryGet(out var level)) return 0f;
        return level switch
        {
            Difficulty.Easy => HumanEdgeTuning.DekeFloorEasy,
            Difficulty.Normal => HumanEdgeTuning.DekeFloorNormal,
            _ => 0f,
        };
    }
}

/// <summary>The numbers of <see cref="HumanEdge"/>.</summary>
public static class HumanEdgeTuning
{
    /// <summary>Strength of an AI check on the human's skater: easy and normal (hard hits in full).</summary>
    public const float CheckEasy = 0.7f, CheckNormal = 0.9f;

    /// <summary>The most a hit can knock a skater down, as a chance, in every other case.</summary>
    public const float KnockdownCap = 0.95f;

    /// <summary>The most a hit can knock the human's skater down: easy and normal.</summary>
    public const float KnockdownCapEasy = 0.6f, KnockdownCapNormal = 0.8f;

    /// <summary>Share of the usual hit chance of an AI skater pressing the human's carrier: easy and normal.</summary>
    public const float HitEasy = 0.5f, HitNormal = 0.8f;

    /// <summary>Least chance a mistimed deke by the human's carrier gets through: easy and normal (hard has none).</summary>
    public const float DekeFloorEasy = 0.55f, DekeFloorNormal = 0.4f;
}
