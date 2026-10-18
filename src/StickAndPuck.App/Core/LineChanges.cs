namespace StickAndPuck.App.Core
{
    /// <summary>How lines change.</summary>
    public enum LineChanges
    {
        /// <summary>Default: tired lines change on their own; a button changes early.</summary>
        Automatic,

        /// <summary>Lines change only on the button.</summary>
        Manual,
    }
}
