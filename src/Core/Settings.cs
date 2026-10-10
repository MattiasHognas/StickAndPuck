namespace StickAndPuck.Core;

/// <summary>How control moves between players.</summary>
public enum ControlSwitching
{
    /// <summary>Default: you control the puck carrier, control follows passes, on defence it goes to the player nearest the puck.</summary>
    Auto,

    /// <summary>Control follows passes, but on defence only the switch button changes player.</summary>
    PassesOnly,

    /// <summary>Only the switch button changes player.</summary>
    Manual,
}

/// <summary>How lines change.</summary>
public enum LineChanges
{
    /// <summary>Default: tired lines change on their own; a button changes early.</summary>
    Automatic,

    /// <summary>Lines change only on the button.</summary>
    Manual,
}

/// <summary>Player options from the start screen, with the defaults from the plan's Decisions.</summary>
public sealed record Settings
{
    /// <summary>Gets how control moves between players.</summary>
    public ControlSwitching Switching { get; init; } = ControlSwitching.Auto;

    /// <summary>Gets how lines change.</summary>
    public LineChanges Lines { get; init; } = LineChanges.Automatic;

    /// <summary>Gets a value indicating whether the gameplay camera zooms with speed. Off by default: zooming softens the ad board text.</summary>
    public bool CameraZoom { get; init; }

    /// <summary>Gets a value indicating whether fights can start.</summary>
    public bool Fights { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls penalties.</summary>
    public bool Penalties { get; init; } = true;

    /// <summary>Gets the sound effect volume, 0 to 1.</summary>
    public float Volume { get; init; } = 0.8f;
}
