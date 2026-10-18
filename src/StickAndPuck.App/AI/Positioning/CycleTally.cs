namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>What one club's cycle came to in a match, for tests and the aimatch line.</summary>
    public sealed class CycleTally
    {
        /// <summary>Gets or sets how many times the club settled into the cycle in the attacking zone.</summary>
        public int Entries { get; set; }

        /// <summary>Gets or sets how many times the forwards rotated stations.</summary>
        public int Turns { get; set; }

        /// <summary>Gets or sets the seconds the club cycled.</summary>
        public float Seconds { get; set; }

        /// <summary>Gets or sets the seconds a cycling carrier had the puck behind the attacked goal line.</summary>
        public float BehindNet { get; set; }
    }
}
