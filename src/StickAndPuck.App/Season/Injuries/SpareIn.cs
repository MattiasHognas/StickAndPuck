using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>A spare (W86) who dresses for a game in the roster slot of a player who is out (<see cref="AbsenceTable.StepIns"/>).</summary>
    /// <param name="Spare">The spare, as the club file has him.</param>
    /// <param name="For">The absent player whose slot, and position, he takes.</param>
    public sealed record SpareIn(PlayerData Spare, PlayerData For);
}
