namespace StickAndPuck.App.Core
{
    /// <summary>How the referee calls too many men on the ice: a bench minor when a rushed human line change
    /// steps on while the skater going off is still out in the play. The AI never rushes a change.</summary>
    public enum TooManyMenCall
    {
        /// <summary>Never called (Arcade): a rushed change is free.</summary>
        Off,

        /// <summary>Default (SHL, NHL): called only when the skater going off is well away from the bench, a few metres.</summary>
        Lenient,

        /// <summary>The rulebook's five feet: called whenever the skater going off is not at the bench.</summary>
        Strict,
    }
}
