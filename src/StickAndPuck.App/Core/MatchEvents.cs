using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>
/// One thing that happened in a match. The hierarchy is closed: every kind is a sealed record
/// below, carrying only its own fields, so consumers switch over the types.
/// </summary>
public abstract record MatchEvent
{
    /// <summary>Initializes a new instance of the <see cref="MatchEvent"/> class.</summary>
    /// <param name="time">Match time in seconds (<see cref="World.Time"/>) when it happened.</param>
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
/// <param name="ScorerId">The scorer: the last player to touch the puck, or, when that was nobody
/// or a player of the other club, the nearest on-ice skater of the scoring club. Always a valid id.</param>
/// <param name="Position">Where the puck crossed the line.</param>
/// <param name="Speed">Puck speed in metres per second.</param>
public sealed record GoalScored(float Time, Side Side, int ScorerId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The puck was dropped.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Dot">The faceoff dot.</param>
public sealed record PuckDropped(float Time, Vector3 Dot) : MatchEvent(Time);

/// <summary>Play stopped for offside.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Attacker">The club that was offside.</param>
/// <param name="Position">Where the puck was.</param>
public sealed record OffsideCalled(float Time, Side Attacker, Vector3 Position) : MatchEvent(Time);

/// <summary>Play stopped for icing.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Offender">The club that iced the puck.</param>
/// <param name="PlayerId">The player who iced it.</param>
/// <param name="Position">Where the puck was.</param>
public sealed record IcingCalled(float Time, Side Offender, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A penalty was called.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The offender's club.</param>
/// <param name="PlayerId">The offender.</param>
/// <param name="Position">Where the offender was.</param>
/// <param name="Seconds">The penalty length shown.</param>
public sealed record PenaltyCalled(float Time, Side Side, int PlayerId, Vector3 Position, float Seconds) : MatchEvent(Time);

/// <summary>A fight started.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">A fighter's club.</param>
/// <param name="PlayerId">That fighter.</param>
/// <param name="Position">Where the fight is.</param>
public sealed record FightStarted(float Time, Side Side, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A goalie stopped a shot.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The goalie's club.</param>
/// <param name="GoalieId">The goalie.</param>
/// <param name="Position">Where the puck was stopped.</param>
/// <param name="Speed">Puck speed in metres per second.</param>
public sealed record ShotSaved(float Time, Side Side, int GoalieId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A line or goalie went on.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The club changing.</param>
/// <param name="PlayerId">The first player on.</param>
/// <param name="Position">Where that player is.</param>
public sealed record LineChanged(float Time, Side Side, int PlayerId, Vector3 Position) : MatchEvent(Time);

/// <summary>A period ended.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Period">The period that ended.</param>
public sealed record PeriodEnded(float Time, int Period) : MatchEvent(Time);

/// <summary>The match ended: <see cref="MatchWon"/> or <see cref="MatchTied"/>.</summary>
/// <param name="Time">Match time in seconds.</param>
public abstract record MatchEnded(float Time) : MatchEvent(Time);

/// <summary>The match ended with a winner.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Winner">The winning club.</param>
public sealed record MatchWon(float Time, Side Winner) : MatchEnded(Time);

/// <summary>The match ended level.</summary>
/// <param name="Time">Match time in seconds.</param>
public sealed record MatchTied(float Time) : MatchEnded(Time);

/// <summary>A shot was taken.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The shooter's club.</param>
/// <param name="ShooterId">The shooter.</param>
/// <param name="Position">Where the puck left the stick.</param>
/// <param name="Speed">Puck speed in metres per second.</param>
public sealed record ShotTaken(float Time, Side Side, int ShooterId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A pass was made.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The passer's club.</param>
/// <param name="PasserId">The passer.</param>
/// <param name="Position">Where the puck left the stick.</param>
/// <param name="Speed">Puck speed in metres per second.</param>
public sealed record PassMade(float Time, Side Side, int PasserId, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>A body check (or a punch in a fight) landed.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Side">The checker's club.</param>
/// <param name="CheckerId">The checker.</param>
/// <param name="Position">Where the target was.</param>
/// <param name="Strength">How hard it hit.</param>
public sealed record CheckLanded(float Time, Side Side, int CheckerId, Vector3 Position, float Strength) : MatchEvent(Time);

/// <summary>The puck hit the boards or glass.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Position">Where it hit.</param>
/// <param name="Speed">Impact speed in metres per second.</param>
public sealed record PuckHitBoards(float Time, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The puck hit a post or crossbar.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Position">Where it hit.</param>
/// <param name="Speed">Impact speed in metres per second.</param>
public sealed record PuckHitPost(float Time, Vector3 Position, float Speed) : MatchEvent(Time);

/// <summary>The referee blew the whistle.</summary>
/// <param name="Time">Match time in seconds.</param>
/// <param name="Position">Where the puck was.</param>
public sealed record WhistleBlown(float Time, Vector3 Position) : MatchEvent(Time);

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

    /// <summary>Gets this step's events of one type.</summary>
    /// <typeparam name="T">The event type to look for.</typeparam>
    /// <returns>The matching events.</returns>
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
