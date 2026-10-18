namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>What club tactics has a skater doing this step.</summary>
    public enum TacticTask
    {
        /// <summary>Left to positioning, defence and decisions.</summary>
        None,

        /// <summary>Aggressive forecheck: the second forward in, on the carrier's outlet.</summary>
        Outlet,

        /// <summary>Aggressive forecheck: the third forward, high in the zone.</summary>
        High,

        /// <summary>Aggressive forecheck: a defenceman holding the blue line.</summary>
        Pinch,

        /// <summary>Trap: the first forward, angling at the blue line.</summary>
        Angle,

        /// <summary>Trap: one of the three across the red line.</summary>
        Wall,

        /// <summary>Trap: the defenceman staying back.</summary>
        Back,

        /// <summary>Dump and chase: a forward going in after the dump.</summary>
        Chase,
    }
}
