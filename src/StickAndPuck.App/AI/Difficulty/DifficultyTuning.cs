namespace StickAndPuck.App.AI.Difficulty
{
    /// <summary>How far easy moves each skater trait down, on the 0 to 100 scale (hard moves them up by <see cref="HardShare"/> of that), and how far each difficulty takes the AI goalie down.</summary>
    public static class DifficultyTuning
    {
        /// <summary>Hockey IQ: easy reads the play worse and fumbles more often.</summary>
        public const int HockeyIq = 20;

        /// <summary>Shot accuracy.</summary>
        public const int ShotAccuracy = 15;

        /// <summary>Shot power.</summary>
        public const int ShotPower = 10;

        /// <summary>Puck control: keeping and receiving the puck.</summary>
        public const int PuckControl = 12;

        /// <summary>Top speed and acceleration.</summary>
        public const int Skating = 8;

        /// <summary>Checking.</summary>
        public const int Checking = 10;

        /// <summary>How far easy takes every trait of the AI goalie down: a human's shot on target goes in about a third of the time.</summary>
        public const int GoalieEasy = 45;

        /// <summary>How far normal takes every trait of the AI goalie down: a human's shot on target goes in about one time in six.</summary>
        public const int GoalieNormal = 15;

        /// <summary>How far hard takes every trait of the AI goalie down: about one shot on target in nine goes in.</summary>
        public const int GoalieHard = 5;

        /// <summary>Hard moves each trait up by this share of easy's step, so hard is a sharper club rather than a club of stars.</summary>
        public const float HardShare = 0.75f;
    }
}
