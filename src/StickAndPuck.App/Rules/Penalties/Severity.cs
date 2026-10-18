namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>How much a call costs: what the club serves and what the offender sits out.</summary>
    public enum Severity
    {
        /// <summary>Two minutes; a power play goal ends it.</summary>
        Minor,

        /// <summary>Two and two; a power play goal ends the half that is running.</summary>
        DoubleMinor,

        /// <summary>Five minutes served in full, a goal or not; the offender comes back after.</summary>
        Major,

        /// <summary>Five minutes served in full by a teammate; the offender is ejected and leaves the squad.</summary>
        MajorAndGameMisconduct,

        /// <summary>Two minutes for the club, served by a teammate; the offender sits ten more, the club not short.</summary>
        MinorAndMisconduct,

        /// <summary>Ten minutes for the offender alone; the club plays at full strength.</summary>
        Misconduct,

        /// <summary>The offender is ejected; the club plays at full strength.</summary>
        GameMisconduct,
    }
}
