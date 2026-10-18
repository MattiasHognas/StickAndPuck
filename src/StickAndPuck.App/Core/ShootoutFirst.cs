namespace StickAndPuck.App.Core
{
    /// <summary>Who shoots first in a shootout.</summary>
    public enum ShootoutFirst
    {
        /// <summary>The away club shoots first and starts sudden death (SHL).</summary>
        Away,

        /// <summary>The home club shoots first.</summary>
        Home,

        /// <summary>The home club chooses (NHL).</summary>
        HomeChooses,
    }
}
