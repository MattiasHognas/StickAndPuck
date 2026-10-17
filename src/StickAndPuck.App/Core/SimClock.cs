namespace StickAndPuck.App.Core;

/// <summary>How many fixed 60 Hz steps each drawn frame runs. The match only ever sees whole steps
/// of <see cref="Game.Step"/>, counted in <see cref="World.Tick"/>, so it plays the same however
/// fast the screen draws: in real time the frame's length is banked and spent in whole steps;
/// captures and headless checks run exactly one step per frame; a replay running to its SAP_AT
/// moment runs many steps per frame.</summary>
public sealed class SimClock
{
    /// <summary>Steps per drawn frame while fast-forwarding.</summary>
    public const int FastSteps = 600;

    /// <summary>The longest frame that is caught up on; a longer stall (a breakpoint, a dragged window) is dropped.</summary>
    const float MaxFrame = 0.25f;

    float pending;

    /// <summary>Gets the fraction between the last two simulation poses; captures use the current pose.</summary>
    public float Alpha { get; private set; } = 1;

    /// <summary>Gets the steps to run for a frame.</summary>
    public int Steps(float frameSeconds, bool lockstep, bool fast)
    {
        if (fast || lockstep)
        {
            pending = 0;
            Alpha = 1;
            return fast ? FastSteps : 1;
        }
        pending += Math.Min(frameSeconds, MaxFrame);
        int steps = (int)(pending / Game.Step);
        pending -= steps * Game.Step;
        Alpha = Math.Clamp(pending / Game.Step, 0, 1);
        return steps;
    }
}
