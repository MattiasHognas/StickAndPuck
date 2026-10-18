using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>The decisions' state for a match, and the tallies other code may read.</summary>
    public static class DecisionState
    {
        /// <summary>Gets a club's tally for this match.</summary>
        public static DecisionTally Tally(World world, Side side)
        {
            return Board(world).Tally[(int)side];
        }

        internal static DecisionBoard Board(World world)
        {
            return world.Decisions.Get(() => new DecisionBoard());
        }
    }
}
