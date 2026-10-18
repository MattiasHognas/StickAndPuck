using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Players.Movement
{
    /// <summary>The stance a skater holds at a faceoff until the puck drops: the two centres (the skater of each club
    /// nearest the dot, as <see cref="FaceoffDraw.Centre"/> picks them) lowest over the dot, everyone else set in a
    /// crouch. Read only: the match flow's state is looked at, never changed.</summary>
    internal static class FaceoffPose
    {
        /// <summary>A skater slower than this has reached its spot; faster ones are still skating into position.</summary>
        private const float SetSpeed = 0.4f;

        /// <summary>Gets the clip a skater holds before the drop: faceoff_centre for a centre, faceoff_crouch for the
        /// rest, nothing once the puck is down, outside a faceoff, or while the skater still skates in.</summary>
        public static Maybe<string> Clip(World world, Player p)
        {
            if (
                world.Match.Phase != MatchPhase.Faceoff
                || !MatchFlow.IsRunning(world)
                || p.Speed > SetSpeed
            )
            {
                return Maybe.None<string>();
            }

            FlowState s = MatchFlow.State(world);
            return s.Draw.Dropped
                ? Maybe.None<string>()
                : (
                    (Maybe<string>)(
                        FaceoffDraw.Centre(world, s.Dot, p.Side).Is(c => c == p)
                            ? "faceoff_centre"
                            : "faceoff_crouch"
                    )
                );
        }
    }
}
