namespace StickAndPuck.App.Core
{
    /// <summary>The coach's pre-game talk a season match is played with (W125): the human's club's side and
    /// the tone picked. It rides on <see cref="MatchSetup.Talk"/>, so a replay stores it and plays the same
    /// match. Its effects are scaled by <see cref="RuleSet.TalkScale"/> and touch only the club that heard
    /// it: <see cref="PenaltyScale"/> (the referee's call chance on its skaters), <see cref="HitScale"/>
    /// (how often its AI skaters line up a hit), <see cref="PassIq"/> (hockey sense added to its passes'
    /// accuracy) and <see cref="MomentumStart"/> (the boost it starts the match with).</summary>
    /// <param name="Side">The side of the club the coach talked to.</param>
    /// <param name="Tone">The tone picked.</param>
    public sealed record CoachTalk(Side Side, TalkTone Tone)
    {
        /// <summary>The factor on penalty calls after a calm talk, at normal strength.</summary>
        public const float CalmPenalties = 0.85f;

        /// <summary>The hockey sense a calm talk adds to every pass, at normal strength.</summary>
        public const int CalmPassIq = 12;

        /// <summary>The factor on penalty calls after firing the club up, at normal strength.</summary>
        public const float FireUpPenalties = 1.3f;

        /// <summary>The factor on lining up hits after firing the club up, at normal strength.</summary>
        public const float FireUpHits = 1.5f;

        /// <summary>The momentum boost (0 to 1) a fired-up club starts with, at normal strength.</summary>
        public const float FireUpMomentum = 0.35f;

        /// <summary>The factor on penalty calls after demanding discipline, at normal strength.</summary>
        public const float DisciplinePenalties = 0.65f;

        /// <summary>The factor on lining up hits after demanding discipline, at normal strength.</summary>
        public const float DisciplineHits = 0.85f;

        /// <summary>Gets the factor on the referee's call chance for a foul by a side's skater: 1 without a talk.</summary>
        public static float PenaltyScale(MatchSetup setup, Side side)
        {
            return !Heard(setup, side).TryGet(out TalkTone tone)
                ? 1f
                : tone switch
                {
                    TalkTone.Calm => Scaled(CalmPenalties, setup.Rules),
                    TalkTone.FireUp => Scaled(FireUpPenalties, setup.Rules),
                    TalkTone.Discipline => Scaled(DisciplinePenalties, setup.Rules),
                    _ => 1f,
                };
        }

        /// <summary>Gets the factor on how often a side's AI skater lines up a hit: 1 without a talk.</summary>
        public static float HitScale(MatchSetup setup, Side side)
        {
            return !Heard(setup, side).TryGet(out TalkTone tone)
                ? 1f
                : tone switch
                {
                    TalkTone.FireUp => Scaled(FireUpHits, setup.Rules),
                    TalkTone.Discipline => Scaled(DisciplineHits, setup.Rules),
                    TalkTone.Calm => 1f,
                    _ => 1f,
                };
        }

        /// <summary>Gets the hockey sense added to a side's passes: 0 without a calm talk.</summary>
        public static int PassIq(MatchSetup setup, Side side)
        {
            return Heard(setup, side).Is(t => t == TalkTone.Calm)
                ? (int)
                    MathF.Round(CalmPassIq * setup.Rules.TalkScale, MidpointRounding.AwayFromZero)
                : 0;
        }

        /// <summary>Gets the momentum boost a side starts the match with from the talk: 0 unless fired up.</summary>
        public static float MomentumStart(MatchSetup setup, Side side)
        {
            return Heard(setup, side).Is(t => t == TalkTone.FireUp)
                ? FireUpMomentum * setup.Rules.TalkScale
                : 0f;
        }

        /// <summary>The tone a side heard, or None when it heard none or the rules turn the talk off.</summary>
        private static Maybe<TalkTone> Heard(MatchSetup setup, Side side)
        {
            return
                setup.Talk.TryGet(out CoachTalk talk)
                && talk.Side == side
                && setup.Rules.TalkScale > 0f
                ? talk.Tone
                : Maybe.None<TalkTone>();
        }

        private static float Scaled(float factor, RuleSet rules)
        {
            return 1f + ((factor - 1f) * rules.TalkScale);
        }
    }
}
