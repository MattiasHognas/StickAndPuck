namespace StickAndPuck.App.Audio
{
    /// <summary>The music tracks in art/audio/music/.</summary>
    internal enum MusicTrack
    {
        /// <summary>No music: live play, the pause screen, the test scenes.</summary>
        None,

        /// <summary>The menus, looped.</summary>
        Menu,

        /// <summary>The Zamboni break between periods, looped.</summary>
        Intermission,

        /// <summary>The results screen at the end of a game, played once.</summary>
        Final,
    }
}
