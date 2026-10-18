namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>One side's cycle: whether it runs, each forward's station and when it next turns.</summary>
    internal sealed class CycleState
    {
        public bool Active { get; set; }

        public float NextTurn { get; set; }

        public float GraceUntil { get; set; } = float.NegativeInfinity;

        public int LastCarrier { get; set; } = -1;

        /// <summary>Gets or sets when the carrier last skated at pace or turned, to spot a pinned carrier.</summary>
        public float MovingAt { get; set; }

        /// <summary>Gets or sets the side of the ice (z sign) the corner station is on.</summary>
        public float CornerSide { get; set; } = 1f;

        /// <summary>Gets or sets how many turns this cycle has made, to alternate the kinds of turn.</summary>
        public int Count { get; set; }

        public Dictionary<int, Station> Stations { get; } = [];

        public CycleTally Tally { get; } = new();
    }
}
