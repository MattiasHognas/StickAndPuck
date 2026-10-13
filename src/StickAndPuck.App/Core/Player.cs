using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>What a player's body is doing right now; drives the animation clip.</summary>
public enum PlayerAction
{
    /// <summary>Skating or standing.</summary>
    None,

    /// <summary>Shooting.</summary>
    Shoot,

    /// <summary>Passing.</summary>
    Pass,

    /// <summary>Throwing a check.</summary>
    Check,

    /// <summary>Knocked down.</summary>
    Fallen,

    /// <summary>Fighting.</summary>
    Fight,

    /// <summary>Celebrating a goal.</summary>
    Celebrate,
}

/// <summary>One player on the ice (or on the bench).</summary>
public sealed class Player
{
    bool onIce = true;
    Maybe<PlayerList> roster;

    /// <summary>Gets the index of this player in <see cref="World.Players"/>.</summary>
    public required int Id { get; init; }

    /// <summary>Gets the player's club side.</summary>
    public required Side Side { get; init; }

    /// <summary>Gets the player's roster entry: name, number, position, traits.</summary>
    public required PlayerData Data { get; init; }

    /// <summary>Gets a value indicating whether this player is a goalie.</summary>
    public bool IsGoalie => Data.Position == PlayingPosition.G;

    /// <summary>Gets or sets the position on the ice (y is 0 on the ice).</summary>
    public Vector3 Position { get; set; }

    /// <summary>Gets or sets the velocity in metres per second.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>Gets or sets the heading in radians; 0 faces +z, toward the camera.</summary>
    public float Heading { get; set; }

    /// <summary>Gets or sets a value indicating whether the player is on the ice (false on the bench).</summary>
    public bool OnIce
    {
        get => onIce;
        set
        {
            if (onIce == value) return;
            onIce = value;
            if (roster.TryGet(out var list)) list.Changed();
        }
    }

    /// <summary>Gets or sets the human input slot controlling this player, or -1 when the AI does.</summary>
    public int Slot { get; set; } = -1;

    /// <summary>Gets or sets what this player wants this step; written by input or AI, read by the systems that act.</summary>
    public PlayerCommand Command { get; set; }

    /// <summary>Gets or sets the body action the animation shows.</summary>
    public PlayerAction Action { get; set; }

    /// <summary>Gets or sets the seconds since <see cref="Action"/> started.</summary>
    public float ActionTime { get; set; }

    /// <summary>Gets or sets energy from 1 (fresh) to 0 (spent); Lines scales traits by it.</summary>
    public float Energy { get; set; } = 1f;

    /// <summary>Gets the id drawn into the mask pass, so each player gets its own outline (1 to 255).</summary>
    public int MaskId => Id + 1;

    /// <summary>Gets the speed over the ice.</summary>
    public float Speed => new Vector2(Velocity.X, Velocity.Z).Length();

    /// <summary>Starts a body action from its first frame.</summary>
    public void Begin(PlayerAction action)
    {
        Action = action;
        ActionTime = 0;
    }

    /// <summary>Notes the list this player is in, so going on or off the ice counts as a change to it.</summary>
    internal void Join(PlayerList list) => roster = list;
}
