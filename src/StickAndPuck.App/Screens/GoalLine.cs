using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>A goal for the results screen.</summary>
    internal sealed record GoalLine(Side Side, string Period, string Clock, string Scorer);
}
