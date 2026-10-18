using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>Where a stoppage's faceoff goes. Under <see cref="RuleSet.SmartDots"/> the dot is chosen against
    /// the club that caused the stoppage: in its own zone the draw stays at its end-zone dot, in the zone
    /// it attacks the draw moves out to the neutral-zone dot outside that zone, and in the neutral zone it
    /// goes to the neutral-zone dot nearer its own goal; always on the puck's side. Without smart dots, or
    /// when nobody is to blame (a fight), it is the dot nearest the puck. Offside, icing and penalties
    /// keep their own dots; a new stoppage type (the puck over the glass, say) should come through here.</summary>
    public static class FaceoffSpots
    {
        /// <summary>The x of the end-zone dots.</summary>
        public const float EndDotX = 20.7f;

        /// <summary>The x of the neutral-zone dots.</summary>
        public const float NeutralDotX = 6.1f;

        /// <summary>How far off the long axis the end-zone and neutral-zone dots lie.</summary>
        public const float DotZ = 6.7f;

        /// <summary>Gets a club's own end-zone dot on one side of the rink (by the sign of <paramref name="z"/>).</summary>
        public static Vector3 EndDot(MatchState match, Side side, float z)
        {
            int own = FaceoffFormation.DefendedEnd(match, side);
            return new Vector3(own * EndDotX, 0, z >= 0 ? DotZ : -DotZ);
        }

        /// <summary>Gets the faceoff dot after a stoppage at <paramref name="at"/> caused by <paramref name="offender"/>.</summary>
        public static Vector3 After(World world, SideOrNone offender, Vector3 at)
        {
            if (!world.Setup.Rules.SmartDots || !offender.TryGetSide(out Side side))
            {
                return MatchFlow.NearestDot(at);
            }

            int own = FaceoffFormation.DefendedEnd(world.Match, side);
            float z = at.Z >= 0 ? DotZ : -DotZ;
            int zone = MathF.Abs(at.X) > RinkSpec.BlueLineX ? Math.Sign(at.X) : 0;
            // in its own zone the draw stays; in the zone it attacks it comes out; in the neutral zone it goes nearer its goal
            float x =
                zone == own ? own * EndDotX
                : zone == -own ? -own * NeutralDotX
                : own * NeutralDotX;
            return new Vector3(x, 0, z);
        }
    }
}
