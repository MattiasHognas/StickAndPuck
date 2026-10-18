namespace StickAndPuck.App.Core
{
    /// <summary>Which games of a best of seven playoff series the higher seed hosts. The best of three
    /// play-in always has the lower seed host game 1 and the higher seed games 2 and 3.</summary>
    public enum PlayoffHomeIce
    {
        /// <summary>Home ice alternates: the higher seed hosts games 1, 3, 5 and 7 (SHL).</summary>
        Alternating,

        /// <summary>2-2-1-1-1: the higher seed hosts games 1, 2, 5 and 7 (NHL).</summary>
        TwoTwoOneOneOne,
    }
}
