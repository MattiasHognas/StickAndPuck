namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>What one club's play came to in a match, measured the same way whatever its tactics, so
    /// clubs can be compared.</summary>
    public sealed class TacticsTally
    {
        /// <summary>Gets or sets the times the club got the puck over the other side's blue line.</summary>
        public int Entries { get; set; }

        /// <summary>Gets or sets the entries made with a skater carrying the puck over the line.</summary>
        public int Carried { get; set; }

        /// <summary>Gets or sets the dump-ins the club's tactics fired into the corner.</summary>
        public int Dumps { get; set; }

        /// <summary>Gets or sets the dumps the club got back: one of its skaters was next on the puck.</summary>
        public int Recovered { get; set; }

        /// <summary>Gets or sets the seconds the other side carried the puck in its own zone.</summary>
        public float BreakoutSeconds { get; set; }

        /// <summary>Gets or sets the skater-seconds the club had inside the other side's zone during those breakouts.</summary>
        public float DeepSkaterSeconds { get; set; }

        /// <summary>Gets or sets the skater-seconds the club had between the far blue line and its own blue line, waiting, during those breakouts.</summary>
        public float NeutralSkaterSeconds { get; set; }

        /// <summary>Gets or sets the times the other side carried the puck out over its own blue line.</summary>
        public int Exits { get; set; }

        /// <summary>Gets or sets the club's skaters back, goal side of the carrier, summed over those exits.</summary>
        public int BackAtExits { get; set; }

        /// <summary>Gets how many of the club's skaters were back, goal side of the puck, on average, as the other side carried it out of its zone.</summary>
        public float Back => Exits == 0 ? 0f : (float)BackAtExits / Exits;

        /// <summary>Gets the share of entries carried in, 0 to 1.</summary>
        public float CarryShare => Entries == 0 ? 0f : (float)Carried / Entries;

        /// <summary>Gets how many of the club's skaters were in on the forecheck, on average, while the other side broke out.</summary>
        public float Forecheckers =>
            BreakoutSeconds <= 0 ? 0f : DeepSkaterSeconds / BreakoutSeconds;

        /// <summary>Gets how many of the club's skaters waited in the neutral zone, on average, while the other side broke out.</summary>
        public float Trappers => BreakoutSeconds <= 0 ? 0f : NeutralSkaterSeconds / BreakoutSeconds;
    }
}
