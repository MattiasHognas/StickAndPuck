namespace StickAndPuck.App.Core
{
    /// <summary>How control moves between players.</summary>
    public enum ControlSwitching
    {
        /// <summary>Default: you control the puck carrier, control follows passes, on defence it goes to the player nearest the puck.</summary>
        Auto,

        /// <summary>Control follows passes, but on defence only the switch button changes player.</summary>
        PassesOnly,

        /// <summary>Only the switch button changes player.</summary>
        Manual,
    }
}
