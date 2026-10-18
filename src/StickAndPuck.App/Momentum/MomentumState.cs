namespace StickAndPuck.App.Momentum
{
    /// <summary>One match's momentum: each club's boost, 0 (none) to 1 (just scored), and what started it.</summary>
    public sealed class MomentumState
    {
        /// <summary>Gets each club's boost, home then away, 0 to 1.</summary>
        public float[] Level { get; } = new float[2];

        /// <summary>Gets what started each club's boost.</summary>
        public MomentumCause[] Cause { get; } = new MomentumCause[2];

        /// <summary>Gets the match time each club's latest boost started.</summary>
        public float[] Since { get; } = [float.NegativeInfinity, float.NegativeInfinity];

        /// <summary>Gets how many boosts each club has had this match.</summary>
        public int[] Boosts { get; } = new int[2];

        /// <summary>Gets the energy each club's skaters have got back from their boosts this match.</summary>
        public float[] Energy { get; } = new float[2];

        /// <summary>Gets or sets how many times the crowd has swelled this match.</summary>
        public int Swells { get; set; }

        /// <summary>Gets or sets the volume of the latest crowd swell, 0 to 1.</summary>
        public float CrowdVolume { get; set; }
    }
}
