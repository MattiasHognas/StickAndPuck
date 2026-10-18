namespace StickAndPuck.App.Benches
{
    /// <summary>What a figure on the bench is doing.</summary>
    public enum BenchPose
    {
        /// <summary>Sitting in his seat, watching the puck.</summary>
        Watch,

        /// <summary>Up for the next change: standing and sliding along the bench to the door.</summary>
        Ready,

        /// <summary>Back from a shift: walking from the door to his seat.</summary>
        Return,
    }
}
