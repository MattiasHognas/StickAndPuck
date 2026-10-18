using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>The special teams' state for a match, and what other code may read of it.</summary>
    public static class SpecialTeamsState
    {
        /// <summary>Gets a club's special teams tally for this match.</summary>
        public static SpecialTeamsTally Tally(World world, Side side)
        {
            return Board(world).Tally[(int)side];
        }

        /// <summary>Gets the shape a club plays this step.</summary>
        public static Formation FormationOf(World world, Side side)
        {
            return Board(world).Shape[(int)side];
        }

        /// <summary>Gets a skater's spot in his club's special teams shape this step.</summary>
        public static Maybe<Vector2> SpotOf(World world, Player p)
        {
            return Board(world).Spots[(int)p.Side].TryGetValue(p.Id, out Vector2 spot)
                ? spot
                : Maybe.None<Vector2>();
        }

        internal static SpecialTeamsBoard Board(World world)
        {
            return world.SpecialTeams.Get(() => new SpecialTeamsBoard());
        }
    }
}
