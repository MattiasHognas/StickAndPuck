using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>A delay-of-game minor called at once, with play stopped for the faceoff at the offending
    /// club's end-zone dot: the puck over the glass from the defending zone (<see cref="RuleSet.OverGlassMinor"/>).
    /// A goalie's minor is served by a skater of his club on the ice (the one nearest him), so a goalie
    /// infraction (a trapezoid, say) can come through here too. Nothing is called with penalties off in
    /// the settings, and then play is not stopped either: the caller stops it its own way.</summary>
    public static class DelayOfGame
    {
        /// <summary>Calls the minor on <paramref name="offenderId"/> and stops play; false when no minor was called.</summary>
        public static bool Call(World world, int offenderId, string reason)
        {
            if (
                !world.Settings.Penalties
                || !MatchFlow.IsRunning(world)
                || offenderId < 0
                || offenderId >= world.Players.Count
            )
            {
                return false;
            }

            Player offender = world.Players[offenderId];
            if (!Server(world, offender).TryGet(out Player server))
            {
                return false;
            }

            PenaltyBook.Log(
                world,
                $"{reason} by {PenaltyCalls.Name(world, offender)}: delay of game{(server.Id == offender.Id ? "" : $", served by {PenaltyCalls.Name(world, server)}")}"
            );
            PenaltyCalls.Give(world, server.Id, Infraction.DelayOfGame, Severity.Minor);
            MatchFlow.Stop(world, PenaltyCalls.FaceoffDot(world, offender.Side));
            return true;
        }

        /// <summary>Gets who serves the offender's minor: the offender, or for a goalie the nearest skater of his club on the ice who is not in the box.</summary>
        public static Maybe<Player> Server(World world, Player offender)
        {
            return offender.IsGoalie ? Teammate(world, offender)
                : PenaltyCalls.InBox(world, offender.Id) ? Maybe.None<Player>()
                : offender;
        }

        private static Maybe<Player> Teammate(World world, Player goalie)
        {
            return
                world
                    .OnIce(goalie.Side)
                    .Where(p => !p.IsGoalie && !PenaltyCalls.InBox(world, p.Id))
                    .MinBy(p =>
                        System.Numerics.Vector3.DistanceSquared(p.Position, goalie.Position)
                    )
                    is { } skater
                ? skater
                : Maybe.None<Player>();
        }
    }
}
