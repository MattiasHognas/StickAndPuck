using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>Kinds of match event.</summary>
public enum MatchEventKind
{
    /// <summary>A goal was scored.</summary>
    Goal,

    /// <summary>The puck was dropped.</summary>
    Faceoff,

    /// <summary>Play stopped for offside.</summary>
    Offside,

    /// <summary>Play stopped for icing.</summary>
    Icing,

    /// <summary>A penalty was called.</summary>
    Penalty,

    /// <summary>A fight started.</summary>
    Fight,

    /// <summary>A goalie stopped a shot.</summary>
    Save,

    /// <summary>A line or goalie went on.</summary>
    LineChange,

    /// <summary>A period ended.</summary>
    PeriodEnd,

    /// <summary>The match ended.</summary>
    MatchEnd,

    /// <summary>A shot was taken.</summary>
    Shot,

    /// <summary>A pass was made.</summary>
    Pass,

    /// <summary>A body check landed.</summary>
    Check,

    /// <summary>The puck hit the boards or glass.</summary>
    PuckBoards,

    /// <summary>The puck hit a post or crossbar.</summary>
    PuckPost,

    /// <summary>The referee blew the whistle.</summary>
    Whistle,
}

/// <summary>One thing that happened in a match.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Time">Match time in seconds (<see cref="World.Time"/>) when it happened.</param>
/// <param name="Side">The club it concerns, if any.</param>
/// <param name="PlayerId">The player it concerns, or -1.</param>
/// <param name="Position">Where on the ice.</param>
/// <param name="Strength">How hard: shot speed, hit force; 0 when it does not apply.</param>
public readonly record struct MatchEvent(
    MatchEventKind Kind, float Time, Side? Side = null, int PlayerId = -1, Vector3 Position = default, float Strength = 0);

/// <summary>
/// The events of the current step. Rules, physics and players publish; HUD, camera, referee,
/// season and audio read. The list is cleared at the start of every step, so read it in
/// Update of a system that runs after the publisher.
/// </summary>
public sealed class MatchEvents
{
    readonly List<MatchEvent> current = [];

    /// <summary>Gets the events published so far this step.</summary>
    public IReadOnlyList<MatchEvent> Current => current;

    /// <summary>Publishes an event to every system that runs after this one.</summary>
    /// <param name="e">The event.</param>
    public void Publish(MatchEvent e)
    {
        current.Add(e);
    }

    /// <summary>Gets this step's events of one kind.</summary>
    /// <param name="kind">The kind to look for.</param>
    /// <returns>The matching events.</returns>
    public IEnumerable<MatchEvent> Of(MatchEventKind kind)
    {
        return current.Where(e => e.Kind == kind);
    }

    /// <summary>Clears the list. Only Game.cs calls this, at the start of each step.</summary>
    public void Clear()
    {
        current.Clear();
    }
}
