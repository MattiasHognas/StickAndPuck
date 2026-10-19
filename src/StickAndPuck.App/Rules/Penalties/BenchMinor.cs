using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>A bench minor: a penalty on the club rather than a skater, too many men on the ice
    /// (<see cref="RuleSet.TooManyMen"/>) for one. It is signalled like any call (<see cref="PenaltyCalls.Signal"/>):
    /// the arm goes up and the whistle comes when the club touches the puck. A skater serves it: the one
    /// the call is about when he can (<see cref="DelayOfGame.Server"/>, so a goalie's goes to a skater), else
    /// the skater of the club on the ice nearest him who is not in the box.</summary>
    public static class BenchMinor
    {
        /// <summary>Signals a bench minor for <paramref name="infraction"/> against the club of <paramref name="offenderId"/>;
        /// false when no call was made (penalties off, out of play, the infraction's rule off, nobody to serve it).</summary>
        public static bool Signal(World world, int offenderId, string infraction)
        {
            if (offenderId < 0 || offenderId >= world.Players.Count)
            {
                return false;
            }

            Player offender = world.Players[offenderId];
            if (!Server(world, offender).TryGet(out Player server))
            {
                return false;
            }

            if (!PenaltyCalls.Signal(world, server.Id, infraction))
            {
                return false;
            }

            PenaltyBook.Log(
                world,
                $"bench minor, {infraction}: {world.Setup.Club(offender.Side).ShortName}{(server.Id == offender.Id ? "" : $", served by {PenaltyCalls.Name(world, server)}")}"
            );
            return true;
        }

        /// <summary>Gets who serves a bench minor the call on <paramref name="offender"/> brings: him, or for a goalie
        /// or a skater already in the box the nearest skater of his club on the ice who is not.</summary>
        public static Maybe<Player> Server(World world, Player offender)
        {
            return DelayOfGame.Server(world, offender).TryGet(out Player server)
                ? server
                : DelayOfGame.Teammate(world, offender);
        }
    }
}
