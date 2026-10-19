namespace StickAndPuck.App.Core
{
    /// <summary>What hurt an injured player.</summary>
    public enum InjuryCause
    {
        /// <summary>No injury: the cause of an absence that is not one (a suspension).</summary>
        None,

        /// <summary>A hard body check.</summary>
        Check,

        /// <summary>A slap shot blocked with the body.</summary>
        BlockedShot,

        /// <summary>A fight.</summary>
        Fight,
    }
}
