namespace StickAndPuck.App.Season.Recap
{
    /// <summary>How the human's season ended (<see cref="SeasonRecap.EndingOf"/>): what an ending scene before the
    /// recap would play (<see cref="Screens.SeasonEnd.PlayEnding"/>), named as the ending scenes are.</summary>
    public enum SeasonEnding
    {
        /// <summary>Won the final.</summary>
        Champions,

        /// <summary>Lost the final.</summary>
        RunnersUp,

        /// <summary>Lost in the semi-finals (the NHL's conference finals).</summary>
        Semifinal,

        /// <summary>Out earlier, missed the playoffs, or stayed up in the playout.</summary>
        SeasonOver,

        /// <summary>Lost the SHL playout and went down (W76).</summary>
        Relegated,

        /// <summary>Fired by the board (W133).</summary>
        Fired,
    }
}
