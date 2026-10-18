namespace StickAndPuck.App.Core
{
    /// <summary>How readily the referee calls a foul.</summary>
    public enum RefereeStrictness
    {
        /// <summary>About half as many calls.</summary>
        Lenient,

        /// <summary>Default: about eight per 60 minutes.</summary>
        Normal,

        /// <summary>About half again as many calls.</summary>
        Strict,
    }
}
