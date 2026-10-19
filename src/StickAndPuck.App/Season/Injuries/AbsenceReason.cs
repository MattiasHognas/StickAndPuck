namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>Why a season player misses games (<see cref="Absence"/>).</summary>
    public enum AbsenceReason
    {
        /// <summary>Hurt in a game (W128).</summary>
        Injury,

        /// <summary>Suspended by the league (W129).</summary>
        Suspension,
    }
}
