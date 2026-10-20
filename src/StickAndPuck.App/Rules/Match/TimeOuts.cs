using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Icing;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>One 30 second time-out per club per game, under <see cref="RuleSet.TimeOut"/>. A club calls it at a
    /// stoppage: while play is stopped after a whistle, or at the faceoff before the puck drops. Play then
    /// waits <see cref="MatchTuning.TimeOutSeconds"/> before the faceoff, the skaters on the ice rest (the lines
    /// system reads <see cref="Running"/>) and the HUD shows the banner with its clock. A human calls it with the
    /// time-out button or from the pause menu; an AI club calls it late in a close game when the skaters it
    /// has on the ice are tired, above all after an icing that keeps them there (see <see cref="AiWants"/>).
    /// Each call publishes <see cref="TimeOutCalled"/>.</summary>
    public static class TimeOuts
    {
        /// <summary>An AI club only calls its time-out with this many seconds or fewer left in the last period or an overtime.</summary>
        public const float AiLateSeconds = 180f;

        /// <summary>The late window as a share of the period, for short periods: 3 of 20 minutes.</summary>
        public const float AiLateShare = 0.15f;

        /// <summary>An AI club calls it when its skaters on the ice are this tired on average.</summary>
        public const float AiTiredBelow = 0.5f;

        /// <summary>After an icing that holds its line on the ice, an AI club calls it already when they are this tired.</summary>
        public const float AiIcedTiredBelow = 0.8f;

        /// <summary>Gets a value indicating whether a club may call its time-out now.</summary>
        public static bool CanCall(World world, Side side)
        {
            if (!world.Setup.Rules.TimeOut || !MatchFlow.IsRunning(world))
            {
                return false;
            }

            FlowState s = MatchFlow.State(world);
            MatchPhase phase = world.Match.Phase;
            bool stoppage =
                (phase == MatchPhase.Stopped && !s.Celebration.HasValue)
                || (phase == MatchPhase.Faceoff && !s.Draw.Dropped);
            return stoppage
                && !s.TimeOut.Used[(int)side]
                && !s.TimeOut.Running.HasValue
                && !Penalties.PenaltyShot.Running(world);
        }

        /// <summary>Calls a club's time-out, if it may: play stays stopped for the time-out, then the faceoff is set up at the same dot.</summary>
        /// <returns>True when the time-out started.</returns>
        public static bool Call(World world, Side side)
        {
            if (!CanCall(world, side))
            {
                return false;
            }

            FlowState s = MatchFlow.State(world);
            s.TimeOut.Used[(int)side] = true;
            s.TimeOut.Running = side;
            world.Match.Phase = MatchPhase.Stopped;
            world.Puck.Live = false;
            s.Timer = MatchTuning.TimeOutSeconds;
            world.Events.Publish(new TimeOutCalled(world.Time, side));
            if (s.Log)
            {
                MatchState m = world.Match;
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: time-out {world.Setup.Club(side).ShortName}: {m.Score[0]}-{m.Score[1]}, P{m.Period} {MatchFlow.ClockText(m.Clock)}"
                    )
                );
            }

            return true;
        }

        /// <summary>Gets the club whose time-out is running, or None.</summary>
        public static Maybe<Side> Running(World world)
        {
            return world.Flow.TryGet(out FlowState s) && world.Match.Phase == MatchPhase.Stopped
                ? s.TimeOut.Running
                : Maybe.None<Side>();
        }

        /// <summary>Gets the seconds left of the running time-out, or 0.</summary>
        public static float SecondsLeft(World world)
        {
            return Running(world).HasValue ? Math.Max(0, MatchFlow.State(world).Timer) : 0;
        }

        /// <summary>Gets a value indicating whether a club has used its time-out.</summary>
        public static bool Used(World world, Side side)
        {
            return world.Flow.TryGet(out FlowState s) && s.TimeOut.Used[(int)side];
        }

        /// <summary>Gets a value indicating whether an AI club wants its time-out now: late in the last period or an
        /// overtime, tied or a goal down, with tired skaters on the ice (or an icing holding them there).</summary>
        public static bool AiWants(World world, Side side)
        {
            MatchState m = world.Match;
            float late = Math.Min(AiLateSeconds, world.Setup.PeriodSeconds * AiLateShare);
            if (m.Period < world.Setup.Periods || m.Clock > late)
            {
                return false;
            }

            int lead = m.Score[(int)side] - m.Score[1 - (int)side];
            if (lead is not (0 or -1))
            {
                return false;
            }

            List<Player> skaters = [.. world.OnIce(side).Where(p => !p.IsGoalie)];
            if (skaters.Count == 0)
            {
                return false;
            }

            float energy = skaters.Average(p => p.Energy);
            return energy < AiTiredBelow
                || (IcingRule.HoldsLines(world, side) && energy < AiIcedTiredBelow);
        }

        /// <summary>At a stoppage: a human's time-out button, then each AI club's call. Called by the match rules.</summary>
        /// <returns>True when a time-out started this step.</returns>
        internal static bool Update(World world, FlowState s)
        {
            if (!world.Setup.Rules.TimeOut || s.TimeOut.Running.HasValue)
            {
                return false;
            }

            foreach (Player p in world.Players)
            {
                if (p.Slot >= 0 && p.Command.TimeOut && Call(world, p.Side))
                {
                    return true;
                }
            }

            foreach (Side side in (ReadOnlySpan<Side>)[Side.Home, Side.Away])
            {
                if (
                    !world.Players.Exists(p => p.Side == side && p.Slot >= 0)
                    && CanCall(world, side)
                    && AiWants(world, side)
                    && Call(world, side)
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
