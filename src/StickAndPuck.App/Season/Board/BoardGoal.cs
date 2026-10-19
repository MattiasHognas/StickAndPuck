namespace StickAndPuck.App.Season.Board
{
    /// <summary>What the board expects of the human's club in the regular season, set at season start from how
    /// strong the club is against the rest of the league (<see cref="BoardExpectation.Pick"/>) and saved
    /// with the season. The table place each goal asks for depends on the league
    /// (<see cref="BoardExpectation.Line"/>).</summary>
    public enum BoardGoal
    {
        /// <summary>Finish first in the regular season table (the strongest clubs).</summary>
        WinLeague,

        /// <summary>Finish near the top: the top 4 of the SHL, the top 8 of the NHL.</summary>
        Top,

        /// <summary>Make the league's own playoffs: top 10 in the SHL (play-in included), a division or wild card spot in the NHL.</summary>
        MakePlayoffs,

        /// <summary>Stay out of the bottom of the table: the last 2 of the SHL, the last 4 of the NHL (the weakest clubs).</summary>
        AvoidBottom,
    }
}
