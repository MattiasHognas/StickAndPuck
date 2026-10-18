using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>The shootout so far, for the HUD and the season: every attempt, who is shooting now and the winner once decided.</summary>
    public sealed class ShootoutState
    {
        private readonly List<ShootoutShot> shots = [];

        /// <summary>Gets every attempt so far, in order.</summary>
        public IReadOnlyList<ShootoutShot> Shots => shots;

        /// <summary>Gets the player shooting now, or -1 between attempts.</summary>
        public int ShooterId { get; internal set; } = -1;

        /// <summary>Gets the club shooting now (or last).</summary>
        public Side Shooting { get; internal set; }

        /// <summary>Gets the shooters per club before sudden death, from the match's rules.</summary>
        public int Rounds { get; internal set; } = 3;

        /// <summary>Gets the winner once the shootout is decided.</summary>
        public SideOrNone Winner { get; internal set; }

        /// <summary>Gets the goals a club has scored in the shootout.</summary>
        public int Goals(Side side)
        {
            return shots.Count(s => s.Side == side && s.Scored);
        }

        /// <summary>Gets the attempts a club has taken.</summary>
        public int Taken(Side side)
        {
            return shots.Count(s => s.Side == side);
        }

        /// <summary>Gets the winner if the attempts so far decide it: one club out of reach within the rounds, or ahead after an even sudden death round.</summary>
        public SideOrNone Decided(int rounds)
        {
            int hg = Goals(Side.Home),
                ag = Goals(Side.Away),
                ht = Taken(Side.Home),
                at = Taken(Side.Away);
            return ht <= rounds && at <= rounds
                ? (hg > ag + (rounds - at))
                    ? SideOrNone.Home
                    : ((ag > hg + (rounds - ht)) ? SideOrNone.Away : SideOrNone.None)
                : (ht != at || hg == ag)
                    ? SideOrNone.None
                    : ((hg > ag) ? SideOrNone.Home : SideOrNone.Away);
        }

        internal void Add(ShootoutShot shot)
        {
            shots.Add(shot);
        }
    }
}
