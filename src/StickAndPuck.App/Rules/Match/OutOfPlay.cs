using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The puck over the glass (<see cref="PuckOverGlass"/>, under <see cref="RuleSet.PuckOverGlass"/>): play stops.
    /// A skater who shot or cleared it straight out from his own defending zone takes a delay-of-game minor
    /// under <see cref="RuleSet.OverGlassMinor"/> (<see cref="DelayOfGame"/>), the faceoff at his end-zone dot.
    /// Otherwise (a deflection, a shot off the boards or the net, a clear from the neutral or attacking zone)
    /// the faceoff is where the puck was last played, by <see cref="FaceoffSpots.After"/> against the club that put it out.</summary>
    public static class OutOfPlay
    {
        /// <summary>Stops play for a puck that went over the glass this step; false when none did.</summary>
        internal static bool Whistle(World world)
        {
            if (world.Events.Of<PuckOverGlass>().FirstOrDefault() is not { } over)
            {
                return false;
            }

            if (Minor(world, over))
            {
                return true;
            }

            bool known = over.PlayerId >= 0 && over.PlayerId < world.Players.Count;
            SideOrNone by = known ? world.Players[over.PlayerId].Side.OrNone() : SideOrNone.None;
            return MatchFlow.Whistle(
                world,
                FaceoffSpots.After(world, by, known ? over.From : over.Position)
            );
        }

        /// <summary>Gets a value indicating whether a puck over the glass draws the delay-of-game minor: straight out, untouched, from the shooter's own defending zone.</summary>
        public static bool DrawsMinor(World world, PuckOverGlass over)
        {
            if (
                !world.Setup.Rules.OverGlassMinor
                || !over.Direct
                || over.PlayerId < 0
                || over.PlayerId >= world.Players.Count
            )
            {
                return false;
            }

            Side side = world.Players[over.PlayerId].Side;
            return InDefendingZone(world, side, over.From);
        }

        /// <summary>Gets a value indicating whether a spot is in a club's defending zone, behind its blue line.</summary>
        public static bool InDefendingZone(World world, Side side, Vector3 at)
        {
            return FaceoffFormation.DefendedEnd(world.Match, side) * at.X > RinkSpec.BlueLineX;
        }

        private static bool Minor(World world, PuckOverGlass over)
        {
            return DrawsMinor(world, over)
                && DelayOfGame.Call(world, over.PlayerId, "puck over the glass");
        }
    }
}
