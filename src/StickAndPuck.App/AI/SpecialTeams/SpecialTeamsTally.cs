namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>What one club's special teams came to in a match, for tests and the HUD line.</summary>
    public sealed class SpecialTeamsTally
    {
        /// <summary>Gets or sets the power plays this club had.</summary>
        public int PowerPlays { get; set; }

        /// <summary>Gets or sets how often this club's power play set up in the zone.</summary>
        public int Entries { get; set; }

        /// <summary>Gets or sets the seconds this club played its power play shape in the zone.</summary>
        public float Seconds { get; set; }

        /// <summary>Gets or sets the seconds of those in the umbrella.</summary>
        public float Umbrella { get; set; }

        /// <summary>Gets or sets the seconds of those in the 1-3-1.</summary>
        public float OneThreeOne { get; set; }

        /// <summary>Gets or sets the power play seconds weighted by the share of skaters within 2.5 m of their spot.</summary>
        public float OnSpot { get; set; }

        /// <summary>Gets or sets the seconds this club killed a penalty in its shape.</summary>
        public float KillSeconds { get; set; }

        /// <summary>Gets or sets the seconds of those in the box.</summary>
        public float Box { get; set; }

        /// <summary>Gets or sets the seconds of those in the diamond.</summary>
        public float Diamond { get; set; }

        /// <summary>Gets or sets the kill seconds weighted by the share of killers on their spot or pressing the carrier.</summary>
        public float KillOnSpot { get; set; }

        /// <summary>Gets or sets the shots this club's power play let go on its own read.</summary>
        public int Shots { get; set; }

        /// <summary>Gets or sets the clears this club fired while short-handed.</summary>
        public int Clears { get; set; }

        /// <summary>Gets or sets the clears flipped high over sticks.</summary>
        public int Flips { get; set; }

        /// <summary>Gets or sets the clears that left the zone within three seconds.</summary>
        public int ClearedOut { get; set; }

        /// <summary>Gets the share of power play time skaters held their spots, 0 to 1.</summary>
        public float Shape => Seconds <= 0 ? 0 : OnSpot / Seconds;

        /// <summary>Gets the share of kill time the killers held their spots or pressed, 0 to 1.</summary>
        public float KillShape => KillSeconds <= 0 ? 0 : KillOnSpot / KillSeconds;
    }
}
