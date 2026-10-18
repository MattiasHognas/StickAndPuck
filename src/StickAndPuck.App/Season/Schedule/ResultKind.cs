namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>How a match was decided, which sets the SHL points.</summary>
    public enum ResultKind
    {
        /// <summary>Decided in regulation: 3 points to the winner.</summary>
        Regulation,

        /// <summary>Decided in 3 on 3 overtime: 2 points to the winner, 1 to the loser.</summary>
        Overtime,

        /// <summary>Decided in the shootout: 2 points to the winner, 1 to the loser.</summary>
        Shootout,
    }
}
