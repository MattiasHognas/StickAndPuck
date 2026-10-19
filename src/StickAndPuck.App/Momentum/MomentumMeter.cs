using StickAndPuck.App.Core;

namespace StickAndPuck.App.Momentum
{
    /// <summary>A short team boost after a goal, a fight won or a big hit. The boost tops up the energy of the
    /// club's skaters on the ice (a second wind: skating and shots already read energy), drains over
    /// about twenty seconds of live play, swells the crowd and shows on the HUD. In a season game a club in
    /// good spirits starts with some boost already (<see cref="StartLevel"/>), more after a fired-up pre-game talk (<see cref="CoachTalk"/>). A goal ends the other
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

        /// <summary>Gets a match's momentum, made on first use: a season game starts each club's boost from its team morale.</summary>
        public static MomentumState State(World world)
        {
            return world.Momentum.Get(() => Fresh(world.Setup));
        }

        /// <summary>Gets the boost a club starts a match with from its team morale (0 to 100): none at 50 or
        /// below, rising to <see cref="MomentumTuning.MoraleStart"/> at 100, scaled by the morale strength.</summary>
        public static float StartLevel(int morale, RuleSet rules)
        {
            float above = Math.Clamp((morale - 50) / 50f, 0f, 1f);
            return Math.Clamp(above * MomentumTuning.MoraleStart * rules.MoraleScale, 0f, 1f);
        }

        private static MomentumState Fresh(MatchSetup setup)
        {
            MomentumState s = new();
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                float morale = setup.Morale.TryGet(out MatchMorale m)
                    ? StartLevel(m.Of(side), setup.Rules)
                    : 0f;
                s.Level[(int)side] = Math.Clamp(
                    morale + CoachTalk.MomentumStart(setup, side),
                    0f,
                    1f
                ); // a fired-up club starts with a head start
            }

            return s;
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
