namespace StickAndPuck.App.Core
{
    /// <summary>Where the match is in its flow.</summary>
    public enum MatchPhase
    {
        /// <summary>Intro, before the first faceoff.</summary>
        Intro,

        /// <summary>Waiting for the puck drop.</summary>
        Faceoff,

        /// <summary>Puck in play, clock running.</summary>
        Play,

        /// <summary>Whistle: play stopped, clock stopped.</summary>
        Stopped,

        /// <summary>Goal scored: celebration and replay.</summary>
        Goal,

        /// <summary>Between periods.</summary>
        Intermission,

        /// <summary>3 on 3 overtime.</summary>
        Overtime,

        /// <summary>Shootout.</summary>
        Shootout,

        /// <summary>Match over.</summary>
        Final,
    }
}
