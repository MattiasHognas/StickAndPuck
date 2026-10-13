namespace StickAndPuck.App.Core;

/// <summary>
/// W56: the state of the dev scenes' loops, which were statics. A loop runs while its slot is set;
/// a normal match never sets one.
/// </summary>
public sealed class DevScenes
{
    /// <summary>Gets the goalie scene's shot loop and tally.</summary>
    internal Slot<Goalie.GoalieDemo> Goalie { get; } = new();

    /// <summary>Gets the positioning scenes' stand-in for the rest of a match.</summary>
    internal Slot<AI.StandInPlay> StandIn { get; } = new();

    /// <summary>Gets the shot scene's loop.</summary>
    internal Slot<Players.ShotDemo> Shot { get; } = new();

    /// <summary>Gets the puck handling scene's loop.</summary>
    internal Slot<Players.HandlingDemo> Handling { get; } = new();

    /// <summary>Gets the checks scene's loop.</summary>
    internal Slot<Players.ChecksDemo> Checks { get; } = new();

    /// <summary>Gets the match demo's shot loop and camera.</summary>
    internal Slot<Rules.MatchDemo> Match { get; } = new();

    /// <summary>Gets the puck scene's shot loop.</summary>
    internal Slot<Physics.PuckDemo> Puck { get; } = new();

    /// <summary>Gets the line calls scene's plays.</summary>
    internal Slot<Rules.LineCallsDemo> LineCalls { get; } = new();
}
