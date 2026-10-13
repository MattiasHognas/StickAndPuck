using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>One thing that happened in a match. The hierarchy is closed: every kind is a sealed record
/// below, carrying only its own fields, so consumers switch over the types.</summary>
public abstract record MatchEvent
{
    /// <summary>Initializes a new instance of the <see cref="MatchEvent"/> class.</summary>
    private protected MatchEvent(float time)
    {
        Time = time;
    }

    /// <summary>Gets the match time in seconds (<see cref="World.Time"/>) when it happened.</summary>
    public float Time { get; init; }
}

/// <summary>A goal was scored.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The scoring club.</param>
/// <param name="ScorerId">The scorer: the last player to touch the puck (on an own goal, a player of
/// the other club), or, when the last touch is no valid player, the nearest on-ice skater of the scoring
/// club (any of its players as a last resort). Always a valid id: with no player of the scoring club at
/// all, no goal is published.</param>
/// <param name="Position">Where the puck crossed the line.</param>
/// <param name="Speed">Puck speed in metres per second.</param>
public sealed record GoalScored(float Time, Side Side, int ScorerId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The puck was dropped.</summary>
public sealed record PuckDropped(float Time, Vector3 Dot) : MatchEvent(Time);

/// <summary>Play stopped for offside.</summary>
public sealed record OffsideCalled(float Time, Side Attacker, Vector3 Position) : MatchEvent(Time);

/// <summary>Play stopped for icing.</summary>
public sealed record IcingCalled(float Time, Side Offender, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A penalty was called.</summary>
public sealed record PenaltyCalled(float Time, Side Side, int PlayerId, Vector3 Position, float Seconds) : MatchEvent(Time);

/// <summary>A fight started.</summary>
public sealed record FightStarted(float Time, Side Side, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A goalie stopped a shot.</summary>
public sealed record ShotSaved(float Time, Side Side, int GoalieId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A line or goalie went on.</summary>
public sealed record LineChanged(float Time, Side Side, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A period ended.</summary>
public sealed record PeriodEnded(float Time, int Period) : MatchEvent(Time);

/// <summary>The match ended: <see cref="MatchWon"/> or <see cref="MatchTied"/>.</summary>
public abstract record MatchEnded(float Time) : MatchEvent(Time);

/// <summary>The match ended with a winner.</summary>
public sealed record MatchWon(float Time, Side Winner) : MatchEnded(Time);

/// <summary>The match ended level.</summary>
public sealed record MatchTied(float Time) : MatchEnded(Time);

/// <summary>A shot was taken.</summary>
public sealed record ShotTaken(float Time, Side Side, int ShooterId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A pass was made.</summary>
public sealed record PassMade(float Time, Side Side, int PasserId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A body check (or a punch in a fight) landed.</summary>
public sealed record CheckLanded(float Time, Side Side, int CheckerId, Vector3 Position, float Strength) : MatchEvent(Time);

/// <summary>The puck hit the boards or glass.</summary>
public sealed record PuckHitBoards(float Time, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The puck hit a post or crossbar.</summary>
public sealed record PuckHitPost(float Time, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The referee blew the whistle.</summary>
public sealed record WhistleBlown(float Time, Vector3 Position) : MatchEvent(Time);

/// <summary>The events of the current step. Rules, physics and players publish; HUD, camera, referee,
/// season and audio read. The list is cleared at the start of every step, so read it in
/// Update of a system that runs after the publisher.</summary>
public sealed class MatchEvents
{
    readonly List<MatchEvent> current = [];

    /// <summary>Gets the events published so far this step.</summary>
    public IReadOnlyList<MatchEvent> Current => current;

    /// <summary>Publishes an event to every system that runs after this one.</summary>
    public void Publish(MatchEvent e)
    {
        current.Add(e);
    }

    /// <summary>Gets this step's events of one type.</summary>
    public IEnumerable<T> Of<T>()
        where T : MatchEvent
    {
        return current.OfType<T>();
    }

    /// <summary>Clears the list. Only Game.cs calls this, at the start of each step.</summary>
    public void Clear()
    {
        current.Clear();
    }
}
