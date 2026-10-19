namespace StickAndPuck.App.Audio
{
    /// <summary>The sound effects, one per file in art/audio/effects/ (written by tools/sfx/sfx.py).</summary>
    internal enum Sfx
    {
        /// <summary>A shot leaving the stick.</summary>
        Shot,

        /// <summary>A pass leaving the stick.</summary>
        Pass,

        /// <summary>The puck landing on a stick or glancing off something.</summary>
        Stick,

        /// <summary>The puck hitting the boards or glass.</summary>
        Boards,

        /// <summary>The puck ringing off a post or the crossbar.</summary>
        Post,

        /// <summary>The referee's whistle.</summary>
        Whistle,

        /// <summary>The goal and period horn.</summary>
        Horn,

        /// <summary>The goal fanfare.</summary>
        Goal,

        /// <summary>A goalie stopping the puck.</summary>
        Save,

        /// <summary>A body check.</summary>
        Check,

        /// <summary>The puck dropped at a faceoff.</summary>
        Faceoff,

        /// <summary>The crowd rising.</summary>
        Crowd,

        /// <summary>The sting after a penalty call.</summary>
        Penalty,

        /// <summary>A fight starting.</summary>
        Fight,

        LineChange,

        /// <summary>The end-of-match cadence.</summary>
        Final,

        /// <summary>The home fans' clap chant: clap, clap, clap-clap-clap, hey.</summary>
        ChantClap,

        /// <summary>The home fans' "let's go" chant: two sung syllables and two claps.</summary>
        ChantLetsGo,

        /// <summary>The home end's goal song swelling up after a home goal.</summary>
        GoalSong,

        /// <summary>The away section's thinner chant.</summary>
        ChantAway,
    }
}
