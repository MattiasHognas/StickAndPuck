namespace StickAndPuck.App.Season.Endings
{
    /// <summary>How the season ended for the user's club, each with its own closing scene. One season is the
    /// whole game, so this scene is the game's ending.</summary>
    public enum EndingKind
    {
        /// <summary>Won the final: the captain lifts the trophy at centre ice.</summary>
        Champions,

        /// <summary>Lost the final: the other club celebrates, one of ours down on a knee.</summary>
        RunnersUp,

        /// <summary>Lost the semi-final: the handshake line.</summary>
        SemiFinal,

        /// <summary>Out in the first round, missed the playoffs or stayed up: the empty arena.</summary>
        SeasonOver,

        /// <summary>Went down: outside the arena at night, in the rain.</summary>
        Relegated,

        /// <summary>The coach lost his job: out of the dressing room with a box.</summary>
        Fired,
    }
}
