namespace StickAndPuck.App.AI.Difficulty
{
    /// <summary>The numbers of <see cref="HumanEdge"/>.</summary>
    public static class HumanEdgeTuning
    {
        /// <summary>Strength of an AI check on the human's skater: easy and normal (hard hits in full).</summary>
        public const float CheckEasy = 0.7f,
            CheckNormal = 0.9f;

        /// <summary>The most a hit can knock a skater down, as a chance, in every other case.</summary>
        public const float KnockdownCap = 0.95f;

        /// <summary>The most a hit can knock the human's skater down: easy and normal.</summary>
        public const float KnockdownCapEasy = 0.6f,
            KnockdownCapNormal = 0.8f;

        /// <summary>Share of the usual hit chance of an AI skater pressing the human's carrier: easy and normal.</summary>
        public const float HitEasy = 0.5f,
            HitNormal = 0.8f;

        /// <summary>Least chance a mistimed deke by the human's carrier gets through: easy and normal (hard has none).</summary>
        public const float DekeFloorEasy = 0.55f,
            DekeFloorNormal = 0.4f;
    }
}
