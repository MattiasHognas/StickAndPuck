namespace StickAndPuck.App.Season.News
{
    /// <summary>Which next game a news card can be drawn before.</summary>
    public enum NewsVenue
    {
        /// <summary>Any game.</summary>
        Any,

        /// <summary>Only before a home game (a fan night).</summary>
        Home,

        /// <summary>Only before an away game (a travel delay).</summary>
        Away,
    }
}
