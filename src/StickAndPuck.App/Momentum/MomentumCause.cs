namespace StickAndPuck.App.Momentum
{
    /// <summary>What started a club's boost.</summary>
    public enum MomentumCause
    {
        /// <summary>No boost.</summary>
        None,

        /// <summary>A big hit: a check of strength 1.5 or more.</summary>
        BigHit,

        /// <summary>A fight won.</summary>
        FightWon,

        /// <summary>A goal scored.</summary>
        Goal,
    }
}
