using StickAndPuck.App.Core;

namespace StickAndPuck.App.Momentum
{
    /// <summary>A short team boost after a goal, a fight won or a big hit. The boost tops up the energy of the
    /// club's skaters on the ice (a second wind: skating and shots already read energy), drains over
    /// about twenty seconds of live play, swells the crowd and shows on the HUD. A goal ends the other
    /// club's boost. Others read it through <see cref="Of"/>.</summary>
    public static class MomentumMeter
    {
        /// <summary>Gets a club's boost.</summary>
        public static float Of(World world, Side side)
        {
            return world.Momentum.TryGet(out MomentumState s) ? s.Level[(int)side] : 0f;
        }

        /// <summary>Gets the club with the bigger boost, or None when neither has one.</summary>
        public static SideOrNone Leader(World world)
        {
            float home = Of(world, Side.Home),
                away = Of(world, Side.Away);
            return (home <= 0f && away <= 0f)
                ? SideOrNone.None
                : ((home >= away) ? SideOrNone.Home : SideOrNone.Away);
        }

        /// <summary>Gets a match's momentum, made on first use.</summary>
        public static MomentumState State(World world)
        {
            return world.Momentum.Get(() => new MomentumState());
        }

        /// <summary>Gets the boost each cause gives.</summary>
        public static float Size(MomentumCause cause)
        {
            return cause switch
            {
                MomentumCause.Goal => MomentumTuning.Goal,
                MomentumCause.FightWon => MomentumTuning.FightWon,
                MomentumCause.BigHit => MomentumTuning.BigHit,
                MomentumCause.None => 0f,
                _ => 0f,
            };
        }

        /// <summary>Gives a club a boost. A goal fills it and ends the other club's; a fight or a hit adds to it, up to full.</summary>
        public static void Boost(World world, Side side, MomentumCause cause)
        {
            MomentumState s = State(world);
            int i = (int)side;
            float before = s.Level[i];
            s.Level[i] =
                cause == MomentumCause.Goal
                    ? Math.Max(before, Size(cause))
                    : Math.Min(1f, before + Size(cause));
            if (cause == MomentumCause.Goal)
            {
                s.Level[1 - i] = 0f;
            }

            if (before <= 0f || cause >= s.Cause[i])
            {
                s.Cause[i] = cause;
            }

            s.Since[i] = world.Time;
            s.Boosts[i]++;
        }
    }
}
