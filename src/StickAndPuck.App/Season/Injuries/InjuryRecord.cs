using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>One injury of the season as it happened, kept after the player is back (<see cref="Stats.SeasonStats.Injuries"/>)
    /// so the season recap (W136) can count them; <see cref="AbsenceTable"/> only holds the ones still running.</summary>
    /// <param name="Game">The credited game's key ("g12", "p0.1.2").</param>
    /// <param name="Club">The club id.</param>
    /// <param name="Number">The jersey number.</param>
    /// <param name="Name">The last name.</param>
    /// <param name="Cause">What hurt him.</param>
    /// <param name="Games">The games he was given.</param>
    public sealed record InjuryRecord(
        string Game,
        string Club,
        int Number,
        string Name,
        InjuryCause Cause,
        int Games
    );
}
