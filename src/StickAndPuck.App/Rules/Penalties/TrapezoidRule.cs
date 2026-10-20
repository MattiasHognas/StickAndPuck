using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>The trapezoid (W72, <see cref="RuleSet.Trapezoid"/>): a goalie who plays the puck behind his own goal
    /// line in a corner, outside the trapezoid (<see cref="RinkSpec.OutsideTrapezoid"/>), takes a delay-of-game
    /// minor at once (<see cref="DelayOfGame"/>), served by the skater of his club nearest him, with the draw at his
    /// end-zone dot. A goalie plays the puck when he touches it (it becomes his last touch: judged where the puck lay at the step
    /// before, since physics has moved it on since) or while he carries it.
    /// Only a human-controlled goalie can break it: the AI goalie keeps its puck play behind the net inside the
    /// trapezoid (Goalie/GoalieSystem.PlayingPuck.cs). Runs in play, after the goalie, physics and the match rules.</summary>
    internal static class TrapezoidRule
    {
        /// <summary>Calls the minor for a goalie playing the puck outside the trapezoid this step; false when none was called.</summary>
        public static bool Watch(World world, PenaltyBook book)
        {
            Puck pk = world.Puck;
            int touch = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
            bool carried = pk.Carrier >= 0;
            bool played = touch != book.TrapezoidTouch || carried;
            // a touch this step was made where the puck lay before physics moved it on; a carried puck is where it is
            System.Numerics.Vector3 at = carried ? pk.Position : book.TrapezoidPuck;
            book.TrapezoidTouch = touch;
            book.TrapezoidPuck = pk.Position;
            if (
                !world.Setup.Rules.Trapezoid
                || !world.Settings.Penalties
                || !MatchFlow.IsRunning(world)
                || !played
                || touch < 0
                || touch >= world.Players.Count
                || !world.Players[touch].IsGoalie
                || !Illegal(world, world.Players[touch], at)
            )
            {
                return false;
            }

            Player goalie = world.Players[touch];
            if (!DelayOfGame.Server(world, goalie).HasValue)
            {
                return false;
            }

            world.Events.Publish(new TrapezoidViolated(world.Time, goalie.Side, goalie.Id, at));
            return DelayOfGame.Call(world, goalie.Id, "puck played outside the trapezoid");
        }

        /// <summary>Gets a value indicating whether a goalie playing the puck at <paramref name="at"/> breaks the trapezoid:
        /// behind his own goal line, outside the trapezoid.</summary>
        public static bool Illegal(World world, Player goalie, System.Numerics.Vector3 at)
        {
            return at.X * FaceoffFormation.DefendedEnd(world.Match, goalie.Side) > 0
                && RinkSpec.OutsideTrapezoid(at);
        }
    }
}
