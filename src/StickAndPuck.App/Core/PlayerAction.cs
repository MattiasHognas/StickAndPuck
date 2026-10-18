namespace StickAndPuck.App.Core
{
    /// <summary>What a player's body is doing right now; drives the animation clip.</summary>
    public enum PlayerAction
    {
        /// <summary>Skating or standing.</summary>
        None,

        /// <summary>Shooting.</summary>
        Shoot,

        /// <summary>Passing.</summary>
        Pass,

        /// <summary>Throwing a check.</summary>
        Check,

        /// <summary>Knocked down.</summary>
        Fallen,

        /// <summary>Fighting.</summary>
        Fight,

        /// <summary>Celebrating a goal.</summary>
        Celebrate,
    }
}
