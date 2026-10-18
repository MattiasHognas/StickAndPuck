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

        /// <summary>Gets or sets the body checks the club's skaters threw.</summary>
        public int Hits { get; set; }

        /// <summary>Gets or sets the shots the club's skaters let go with the puck on the stick inside the attacking zone.</summary>
        public int Shots { get; set; }

        /// <summary>Gets or sets those shots from further out than <see cref="LongShot"/> metres from the net.</summary>
        public int LongShots { get; set; }

        /// <summary>Gets or sets the seconds the other side carried the puck inside the club's own zone.</summary>
        public float DefendSeconds { get; set; }

        /// <summary>Gets or sets the skater-seconds the club had within <see cref="House"/> metres of its own net meanwhile.</summary>
        public float HouseSkaterSeconds { get; set; }

        /// <summary>How far from the net, in metres, a shot counts as from distance: the slap shot range.</summary>
        public const float LongShot = 13f;

        /// <summary>How near its own net, in metres, a defending skater counts as in the house.</summary>
        public const float House = 9f;

        /// <summary>Gets the share of the club's shots taken from distance, 0 to 1.</summary>
        public float LongShare => Shots == 0 ? 0f : (float)LongShots / Shots;

        /// <summary>Gets how many of the club's skaters stood near its own net, on average, while the other side had the puck in its zone.</summary>
        public float Collapsed => DefendSeconds <= 0 ? 0f : HouseSkaterSeconds / DefendSeconds;

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
