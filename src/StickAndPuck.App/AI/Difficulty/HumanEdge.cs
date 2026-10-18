using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Difficulty
{
    /// <summary>How the difficulty eases the AI club's play against the human's carrier: softer checks, fewer
    /// knockdowns, fewer hits tried, and a mistimed deke that still gets through more often. Only in a match
    /// with a single AI club facing a human (<see cref="DifficultyScale.AiSide"/>), and only on the skater the
    /// human controls; versus and all-AI matches play as they always did.</summary>
    public static class HumanEdge
    {
        /// <summary>Gets the difficulty easing the AI's play on this skater: the match's difficulty when the skater is
        /// human-controlled and plays against the AI club, or None.</summary>
        public static Maybe<Core.Difficulty> Against(World world, Player target)
        {
            return
                target.Slot < 0
                || !DifficultyScale.AiSide(world.Setup).TryGetSide(out Side ai)
                || target.Side == ai
                ? Maybe.None<Core.Difficulty>()
                : (Maybe<Core.Difficulty>)world.Settings.Difficulty;
        }

        /// <summary>Gets how hard an AI check lands on the human's skater, as a share of its full strength.</summary>
        public static float CheckStrength(World world, Player checker, Player target)
        {
            return checker.Slot >= 0 || !Against(world, target).TryGet(out Core.Difficulty level)
                ? 1f
                : level switch
                {
                    Core.Difficulty.Easy => HumanEdgeTuning.CheckEasy,
                    Core.Difficulty.Normal => HumanEdgeTuning.CheckNormal,
                    Core.Difficulty.Hard => 1f,
                    _ => throw new NotSupportedException("Difficulty level not supported"),
                };
        }

        /// <summary>Gets the most a hit can knock the human's skater down, as a chance.</summary>
        public static float KnockdownCap(World world, Player target)
        {
            return !Against(world, target).TryGet(out Core.Difficulty level)
                ? HumanEdgeTuning.KnockdownCap
                : level switch
                {
                    Core.Difficulty.Easy => HumanEdgeTuning.KnockdownCapEasy,
                    Core.Difficulty.Normal => HumanEdgeTuning.KnockdownCapNormal,
                    Core.Difficulty.Hard => HumanEdgeTuning.KnockdownCap,
                    _ => throw new NotSupportedException("Difficulty level not supported"),
                };
        }

        /// <summary>Gets the share of its usual chance an AI skater pressing the human's carrier decides to hit.</summary>
        public static float HitChance(World world, Player carrier)
        {
            return !Against(world, carrier).TryGet(out Core.Difficulty level)
                ? 1f
                : level switch
                {
                    Core.Difficulty.Easy => HumanEdgeTuning.HitEasy,
                    Core.Difficulty.Normal => HumanEdgeTuning.HitNormal,
                    Core.Difficulty.Hard => 1f,
                    _ => throw new NotSupportedException("Difficulty level not supported"),
                };
        }

        /// <summary>Gets the least chance a mistimed deke by the human's carrier beats the defender, or 0.</summary>
        public static float DekeFloor(World world, Player carrier)
        {
            return !Against(world, carrier).TryGet(out Core.Difficulty level)
                ? 0f
                : level switch
                {
                    Core.Difficulty.Easy => HumanEdgeTuning.DekeFloorEasy,
                    Core.Difficulty.Normal => HumanEdgeTuning.DekeFloorNormal,
                    Core.Difficulty.Hard => 0f,
                    _ => throw new NotSupportedException("Difficulty level not supported"),
                };
        }
    }
}
