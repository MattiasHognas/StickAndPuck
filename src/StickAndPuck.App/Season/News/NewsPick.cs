namespace StickAndPuck.App.Season.News
{
    /// <summary>Which skaters a <see cref="NewsEffect.Traits"/> card hits.</summary>
    public enum NewsPick
    {
        /// <summary>Skaters drawn at random.</summary>
        Random,

        /// <summary>The best skaters by their average trait.</summary>
        Best,

        /// <summary>The weakest skaters by their average trait.</summary>
        Weakest,
    }
}
