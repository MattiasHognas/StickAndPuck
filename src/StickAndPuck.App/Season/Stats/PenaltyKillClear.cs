using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Tells a penalty-kill clear from a shot: a club a man short that fires the puck from its own
    /// half is getting it out of trouble, not shooting at the net, so the stats credit a clear instead.</summary>
    public static class PenaltyKillClear
    {
        /// <summary>Gets whether a shot is a clear: the other club is on the power play, the shooter's club
        /// has fewer skaters on the ice, and the shot left from the shooter's own half.</summary>
        public static bool IsClear(World w, ShotTaken shot)
        {
            Side other = shot.Side == Side.Home ? Side.Away : Side.Home;
            return PenaltyCalls.PowerPlay(w).Is(other)
                && Skaters(w, shot.Side) < Skaters(w, other)
                && PuckHandling.AttackEnd(w, shot.Side) * shot.Position.X <= 0;
        }

        private static int Skaters(World w, Side side)
        {
            return w.OnIce(side).Count(p => !p.IsGoalie);
        }
    }
}
