namespace StickAndPuck.App.Core
{
    /// <summary>The points a league game gives, read by the standings.</summary>
    public enum TablePoints
    {
        /// <summary>3 for a regulation win, 2 for a win after regulation, 1 for a loss after regulation, 0 for a regulation loss (SHL).</summary>
        ThreeTwoOne,

        /// <summary>2 for any win, 1 for a loss after regulation, 0 for a regulation loss (NHL).</summary>
        TwoTwoOne,
    }
}
