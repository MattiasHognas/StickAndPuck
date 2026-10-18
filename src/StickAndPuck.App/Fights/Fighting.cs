using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Fights
{
    /// <summary>Fights. A hard check (<see cref="Players.Checks.CheckingModel.HardHit"/> and up) may drop the gloves:
    /// the checker and the skater he hit are offered a fight. An AI skater accepts or declines from his
    /// checking trait and the score (<see cref="FightModel.AcceptChance"/>); a human accepts for his club's
    /// skater with the accept button (F, or Y on a pad) inside <see cref="FightTuning.OfferSeconds"/>, or
    /// lets it pass. When both say yes the referee whistles, the clock stops and the two square off.
    /// The fight is a run of exchanges: a marker sweeps a bar, and a punch thrown inside the sweet spot in
    /// the middle lands (harder the closer to the middle) unless the other fighter gets his guard up just
    /// in time (inside <see cref="FightTuning.BlockFresh"/> of the punch landing). A fighter whose balance
    /// runs out goes down; after <see cref="FightTuning.MaxExchanges"/> the linesmen step in and the one
    /// with less balance loses. The penalty follows the rule set: the loser's two minutes (Arcade), or
    /// coincidental 5 minute majors for both, with a game misconduct under SHL. Fights happen in regulation
    /// only, never involve goalies, and never put a club three short.</summary>
    public static class Fighting
    {
        /// <summary>Gets the fight on the ice, or None.</summary>
        public static Maybe<Fight> Current(World world)
        {
            return world.Fight.TryGet(out FightState s) ? s.Fight : Maybe.None<Fight>();
        }

        /// <summary>Gets the fight waiting to be accepted, or None.</summary>
        public static Maybe<FightOffer> Pending(World world)
        {
            return world.Fight.TryGet(out FightState s) ? s.Offer : Maybe.None<FightOffer>();
        }

        /// <summary>Gets the fights started this match.</summary>
        public static int Started(World world)
        {
            return world.Fight.TryGet(out FightState s) ? s.Started : 0;
        }

        /// <summary>Gets whether a skater is in a fight right now.</summary>
        public static bool InFight(World world, int playerId)
        {
            return Current(world).Is(f => f.Of(playerId).HasValue);
        }

        /// <summary>Gets the clip a fighter shows: "grab", "punch", "block", "fall", "celebrate" or "idle",
        /// the seconds since it started, and whether it loops. None for a skater not in a fight.</summary>
        public static Maybe<(string Clip, float Seconds, bool Loop)> Pose(World world, Player p)
        {
            if (!Current(world).TryGet(out Fight fight) || !fight.Of(p.Id).TryGet(out Fighter f))
            {
                return Maybe.None<(string Clip, float Seconds, bool Loop)>();
            }

            bool loop =
                string.Equals(f.Clip, "grab", StringComparison.Ordinal)
                || string.Equals(f.Clip, "celebrate", StringComparison.Ordinal)
                || string.Equals(f.Clip, "idle", StringComparison.Ordinal);
            return (f.Clip, Math.Max(0, world.Time - f.ClipSince), loop);
        }

        /// <summary>Offers a fight between a checker and the skater he hit. Nothing happens if fights are off, play is
        /// not running in regulation, either is a goalie, off the ice, in the box or about to be, the loser's
        /// minor would put a club three short, or another fight or offer is on.</summary>
        public static bool Offer(World world, int checkerId, int victimId)
        {
            FightState s = State(world);
            if (s.Fight.HasValue || s.Offer.HasValue || !CanFight(world, checkerId, victimId))
            {
                return false;
            }

            if (world.Time - s.LastFight < FightTuning.Cooldown)
            {
                return false;
            }

            FightOffer offer = new(checkerId, victimId, world.Time);
            int[] ids = [checkerId, victimId];
            for (int i = 0; i < 2; i++)
            {
                Player p = world.Players[ids[i]];
                offer.Slots[i] = HumanSlot(world, p.Side);
                if (offer.Slots[i] < 0)
                {
                    offer.Accepted[i] = s.ForceAccept.TryGet(out bool forced)
                        ? forced
                        : s.Rng.NextSingle() < FightModel.AcceptChance(world, p);
                }
            }
            s.Offer = offer;
            FightState.Log(
                world,
                $"gloves off: {PenaltyCalls.Name(world, world.Players[checkerId])} and {PenaltyCalls.Name(world, world.Players[victimId])}"
            );
            return true;
        }

        /// <summary>Gets whether two skaters may start a fight now.</summary>
        internal static bool CanFight(World world, int a, int b)
        {
            if (!world.Settings.Fights || !MatchFlow.IsRunning(world))
            {
                return false;
            }

            MatchState m = world.Match;
            if (m.Phase != MatchPhase.Play || m.Period > world.Setup.Periods)
            {
                return false;
            }

            if (a < 0 || b < 0 || a >= world.Players.Count || b >= world.Players.Count || a == b)
            {
                return false;
            }

            Player pa = world.Players[a];
            Player pb = world.Players[b];
            if (pa.Side == pb.Side)
            {
                return false;
            }

            foreach (Player p in new[] { pa, pb })
            {
                if (
                    p.IsGoalie
                    || !p.OnIce
                    || PenaltyCalls.InBox(world, p.Id)
                    || PenaltyCalls.Delayed(world).Any(d => d.PlayerId == p.Id)
                )
                {
                    return false;
                }

                if (
                    ShortsClub(world)
                    && PenaltyCalls.Serving(world, p.Side)
                        + PenaltyCalls.Delayed(world).Count(d => d.Side == p.Side)
                        >= PenaltyTuning.MaxServing
                )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Gets whether the fight penalty can put a club three short: only the loser's minor, and only
        /// when a third penalty does not wait its turn. Coincidental majors are substituted and never do.</summary>
        private static bool ShortsClub(World world)
        {
            RuleSet rules = world.Setup.Rules;
            return rules.FightPenalty == FightPenalty.LoserMinor && !rules.StackPenalties;
        }

        /// <summary>Gets the human slot playing for a club, or -1 when the AI plays it.</summary>
        internal static int HumanSlot(World world, Side side)
        {
            return world.Players.Find(p => p.Side == side && p.Slot >= 0)?.Slot ?? -1;
        }

        internal static FightState State(World world)
        {
            return world.Fight.Get(() => new FightState());
        }
    }
}
