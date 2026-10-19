namespace StickAndPuck.App.Core
{
    /// <summary>An action a key or gamepad button can be bound to on the key-mapping page.</summary>
    public enum GameAction
    {
        /// <summary>Skate up the screen.</summary>
        SkateUp,

        /// <summary>Skate down the screen.</summary>
        SkateDown,

        /// <summary>Skate left.</summary>
        SkateLeft,

        /// <summary>Skate right.</summary>
        SkateRight,

        /// <summary>Sprint while held.</summary>
        Sprint,

        /// <summary>Shoot (hold to wind up); punches in a fight.</summary>
        Shoot,

        /// <summary>Pass.</summary>
        Pass,

        /// <summary>Body check.</summary>
        Check,

        /// <summary>Deke past a defender with the puck.</summary>
        Deke,

        /// <summary>Switch player.</summary>
        Switch,

        /// <summary>Change line.</summary>
        LineChange,

        /// <summary>Block in a fight.</summary>
        Block,

        /// <summary>Accept a fight.</summary>
        AcceptFight,

        /// <summary>Swap the kits.</summary>
        Kit,

        /// <summary>Toggle the camera zoom.</summary>
        Zoom,

        /// <summary>Call the club's time-out at a stoppage.</summary>
        TimeOut,

        /// <summary>Pause the match.</summary>
        Pause,
    }
}
