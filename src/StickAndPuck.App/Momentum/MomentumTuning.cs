namespace StickAndPuck.App.Momentum
{
    /// <summary>The momentum numbers.</summary>
    public static class MomentumTuning
    {
        /// <summary>The boost a goal gives: full.</summary>
        public const float Goal = 1f;

        /// <summary>The boost a fight won gives.</summary>
        public const float FightWon = 0.7f;

        /// <summary>The boost a big hit adds.</summary>
        public const float BigHit = 0.35f;

        /// <summary>The check strength that counts as a big hit (the same 1.5 that can start a fight).</summary>
        public const float BigHitStrength = 1.5f;

        /// <summary>The boost a club with full team morale (100) starts a season game with, at normal morale strength.</summary>
        public const float MoraleStart = 0.5f;

        /// <summary>Seconds of live play a full boost lasts.</summary>
        public const float Seconds = 20f;

        /// <summary>Energy per second a full boost gives each skater on the ice; about three times what skating drains.</summary>
        public const float Recover = 0.05f;

        /// <summary>Seconds between crowd swells while a boost lasts.</summary>
        public const float SwellGap = 2.5f;

        /// <summary>The crowd's volume at a full boost, before the player's volume; a goal's crowd plays at 0.35.</summary>
        public const float CrowdVolume = 0.9f;

        /// <summary>The boost below which the crowd stops swelling.</summary>
        public const float SwellBelow = 0.25f;
    }
}
