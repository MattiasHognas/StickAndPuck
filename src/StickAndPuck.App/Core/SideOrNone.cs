namespace StickAndPuck.App.Core
{
    /// <summary>A side, or neither: a winner, a power play, a pending offside and the like, before one is decided.</summary>
    public enum SideOrNone
    {
        /// <summary>Neither side (the default).</summary>
        None,

        /// <summary>The home club.</summary>
        Home,

        /// <summary>The visiting club.</summary>
        Away,
    }
}
