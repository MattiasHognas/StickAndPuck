namespace StickAndPuck.App.Core
{
    /// <summary>How long the Zamboni break between periods (and before a playoff overtime) lasts.</summary>
    public enum IntermissionLength
    {
        /// <summary>Standard (SHL, NHL): the Zamboni's whole drive, about half a minute.</summary>
        Standard,

        /// <summary>Quick (Arcade): the Zamboni drives at double pace, so the break takes half as long.</summary>
        Quick,

        /// <summary>No Zamboni and no break before a playoff overtime: the next period follows a short pause.</summary>
        Off,
    }
}
