namespace StickAndPuck.App.Core
{
    public static partial class Text
    {
        public static partial class Keys
        {
            /// <summary>HOT SEAT (the rule)</summary>
            public const string RuleHotSeat = nameof(RuleHotSeat);

            /// <summary>WIN THE REGULAR SEASON</summary>
            public const string BoardGoalWin = nameof(BoardGoalWin);

            /// <summary>FINISH IN THE TOP {0}</summary>
            public const string BoardGoalTop = nameof(BoardGoalTop);

            /// <summary>MAKE THE PLAYOFFS</summary>
            public const string BoardGoalPlayoffs = nameof(BoardGoalPlayoffs);

            /// <summary>STAY OUT OF THE BOTTOM {0}</summary>
            public const string BoardGoalAvoidBottom = nameof(BoardGoalAvoidBottom);

            /// <summary>TARGET: {0} (the board's goal, before the first game)</summary>
            public const string BoardTarget = nameof(BoardTarget);

            /// <summary>TARGET: {0}. PLACE {1} OF {2}, {3}</summary>
            public const string BoardTrack = nameof(BoardTrack);

            /// <summary>AHEAD OF TARGET</summary>
            public const string BoardAhead = nameof(BoardAhead);

            /// <summary>ON TRACK</summary>
            public const string BoardOnTrack = nameof(BoardOnTrack);

            /// <summary>UNDER PRESSURE</summary>
            public const string BoardPressure = nameof(BoardPressure);

            /// <summary>ON THE HOT SEAT</summary>
            public const string BoardHotSeat = nameof(BoardHotSeat);

            /// <summary>FIRED (the screen's heading)</summary>
            public const string Fired = nameof(Fired);

            /// <summary>THE {0} BOARD HAS SEEN ENOUGH</summary>
            public const string FiredBoard = nameof(FiredBoard);

            /// <summary>THE TARGET WAS: {0}</summary>
            public const string FiredTarget = nameof(FiredTarget);

            /// <summary>PLACE {0} OF {1} AFTER {2} GAMES</summary>
            public const string FiredPlace = nameof(FiredPlace);

            /// <summary>YOUR SEASON IS OVER</summary>
            public const string FiredOver = nameof(FiredOver);

            /// <summary>SEE THE BOARD (the results screen's way on to the fired screen)</summary>
            public const string SeeTheBoard = nameof(SeeTheBoard);

            /// <summary>{0}: FIRED BY THE BOARD (the start screen's line for a season that ended with the sack)</summary>
            public const string StartFired = nameof(StartFired);
        }
    }
}
