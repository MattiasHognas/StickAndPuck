namespace StickAndPuck.App.Season.Board
{
    /// <summary>The board's verdict on a regular season (<see cref="BoardExpectation.Verdict"/>), for the season recap.</summary>
    public enum BoardVerdict
    {
        /// <summary>Finished below the goal, or fired.</summary>
        Missed,

        /// <summary>Finished where the goal asked.</summary>
        Met,

        /// <summary>Finished where a harder goal would have asked.</summary>
        Exceeded,
    }
}
