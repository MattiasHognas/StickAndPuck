namespace StickAndPuck.App.Season.Board
{
    /// <summary>Where the human's club stands against the board's goal right now (<see cref="BoardExpectation.Standing"/>).</summary>
    public enum BoardStanding
    {
        /// <summary>Placed where a harder goal would ask: better than the board hoped.</summary>
        Ahead,

        /// <summary>Meeting the goal.</summary>
        OnTrack,

        /// <summary>Below the goal, but not by much.</summary>
        UnderPressure,

        /// <summary>Far below the goal: this late in the season, with the hot seat on, the board fires you.</summary>
        HotSeat,
    }
}
