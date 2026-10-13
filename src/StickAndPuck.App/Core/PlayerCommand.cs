using System.Numerics;

namespace StickAndPuck.App.Core;

/// <summary>
/// What one player wants to do this step. Keyboard and mouse, gamepad and AI all produce it,
/// so the systems that act on it never know who is in control.
/// </summary>
public struct PlayerCommand
{
    /// <summary>Gets or sets the stick direction on the rink: x along the rink, y toward the camera (+z). Length 0 to 1.</summary>
    public Vector2 Move { get; set; }

    /// <summary>Gets or sets a value indicating whether the player wants to skate flat out.</summary>
    public bool Sprint { get; set; }

    /// <summary>Gets or sets a value indicating whether shoot is held (a longer hold is a harder shot).</summary>
    public bool Shoot { get; set; }

    /// <summary>Gets or sets a value indicating whether pass was pressed this step.</summary>
    public bool Pass { get; set; }

    /// <summary>Gets or sets a value indicating whether check was pressed this step.</summary>
    public bool Check { get; set; }

    /// <summary>Gets or sets a value indicating whether deke is held (W32: the puck handling system acts on the press).</summary>
    public bool Deke { get; set; }

    /// <summary>Gets or sets a value indicating whether the human asked to switch to another player.</summary>
    public bool SwitchPlayer { get; set; }

    /// <summary>Gets or sets a value indicating whether the human asked for an early line change.</summary>
    public bool ChangeLine { get; set; }

    /// <summary>Gets or sets a value indicating whether punch was pressed this step (fights).</summary>
    public bool Punch { get; set; }

    /// <summary>Gets or sets a value indicating whether block is held (fights).</summary>
    public bool Block { get; set; }

    /// <summary>Gets or sets a value indicating whether the player accepts a fight that was offered.</summary>
    public bool AcceptFight { get; set; }

    /// <summary>Gets or sets a value indicating whether pause was pressed this step.</summary>
    public bool Pause { get; set; }
}
