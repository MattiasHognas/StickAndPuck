namespace StickAndPuck.App.Rules.Offside
{
    /// <summary>Offside's own state for one world.</summary>
    internal sealed class OffsideState
    {
        /// <summary>Gets or sets the zone the puck is in (-1, 0, +1).</summary>
        public int Zone { get; set; }

        /// <summary>Gets or sets the zone with an offside pending, or 0.</summary>
        public int PendingEnd { get; set; }

        /// <summary>Gets or sets the last touch when the puck entered, so a new attacking touch can be told apart.</summary>
        public int EntryTouch { get; set; } = -1;
    }
}
