using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>Club tactics' state for a match, and the tallies other code may read.</summary>
    public static class TacticsState
    {
        /// <summary>Gets a side's tactics, from its club file.</summary>
        public static ClubTactics Of(World world, Side side)
        {
            return world.Setup.Club(side).Tactics;
        }

        /// <summary>Gets how much likelier a side's skater on the carrier is to line up a hit when he weighs
        /// one: more than 1 for a physical club, else 1.</summary>
        public static float HitScale(World world, Side side)
        {
            return Of(world, side).Style == PlayStyle.Physical ? TacticsSystem.PhysicalHits : 1f;
        }

        /// <summary>Gets a club's tally for this match.</summary>
        public static TacticsTally Tally(World world, Side side)
        {
            return Board(world).Tally[(int)side];
        }

        /// <summary>Gets what tactics has a skater doing this step, for tests and the debug line.</summary>
        public static TacticTask TaskOf(World world, Player p)
        {
            return Board(world).Tasks.GetValueOrDefault(p.Id);
        }

        internal static TacticsBoard Board(World world)
        {
            return world.Tactics.Get(() => new TacticsBoard());
        }
    }
}
