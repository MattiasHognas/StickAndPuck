namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Where a forward plays in the attacking-zone cycle.</summary>
    public enum Station
    {
        /// <summary>In front of the net, a few metres out: the shooting spot.</summary>
        Slot,

        /// <summary>Low in the corner on the puck side, along the boards.</summary>
        Corner,

        /// <summary>Behind the net.</summary>
        BehindNet,
    }
}
