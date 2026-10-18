namespace StickAndPuck.App.Pregame
{
    /// <summary>Where the arena flyover is: off (no warm-up, or SAP_FLYOVER=0), flying over the arena,
    /// in close on the home squad, settling to the broadcast view, or done (played out or skipped).</summary>
    public enum FlyoverPhase
    {
        /// <summary>No flyover: the warm-up is not on, or SAP_FLYOVER=0 turned the flyover off.</summary>
        Off,

        /// <summary>The camera sweeps high over the arena toward the home end.</summary>
        Aerial,

        /// <summary>The close-up camera skates along with the home squad.</summary>
        CloseUp,

        /// <summary>The camera eases out to the broadcast view.</summary>
        Settle,

        /// <summary>The flyover has played out or was skipped; the broadcast view holds until start.</summary>
        Done,
    }
}
