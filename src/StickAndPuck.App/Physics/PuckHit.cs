namespace StickAndPuck.App.Physics;

/// <summary>What the puck hit during a step.</summary>
public enum PuckHit
{
    /// <summary>The boards.</summary>
    Boards,

    /// <summary>The glass above the boards.</summary>
    Glass,

    /// <summary>A post or the crossbar.</summary>
    Post,

    /// <summary>The net mesh or frame.</summary>
    Net,

    /// <summary>A player's body.</summary>
    Body,

    /// <summary>The ice, landing from the air.</summary>
    Ice,
}
