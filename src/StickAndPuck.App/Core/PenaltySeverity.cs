namespace StickAndPuck.App.Core
{
    /// <summary>Which penalty lengths the referee hands out.</summary>
    public enum PenaltySeverity
    {
        /// <summary>Minors, double minors, majors with a game misconduct and misconducts (SHL, NHL).</summary>
        Full,

        /// <summary>Every call is a 2 minute minor (Arcade).</summary>
        MinorsOnly,
    }
}
