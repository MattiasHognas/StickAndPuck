namespace StickAndPuck.App.Core;

/// <summary>
/// W67: how many fixed 60 Hz steps each drawn frame runs. The match only ever sees whole steps
/// of <see cref="Game.Step"/>, counted in <see cref="World.Tick"/>, so it plays the same however
/// fast the screen draws: in real time the frame's length is banked and spent in whole steps;
/// captures and headless checks run exactly one step per frame; a replay running to its SAP_AT
/// moment runs many steps per frame.
/// </summary>
public sealed class SimClock
{
    /// <summary>Steps per drawn frame while fast-forwarding.</summary>
    public const int FastSteps = 600;

    /// <summary>The longest frame that is caught up on; a longer stall (a breakpoint, a dragged window) is dropped.</summary>
    const float MaxFrame = 0.25f;

    float pending;

    /// <summary>Gets the steps to run for a frame.</summary>
    /// <param name="frameSeconds">The real length of the last frame.</param>
    /// <param name="lockstep">One step per frame, for captures and headless checks that must repeat.</param>
    /// <param name="fast">Run <see cref="FastSteps"/> steps, for a replay running to its moment.</param>
    /// <returns>The number of steps.</returns>
    public int Steps(float frameSeconds, bool lockstep, bool fast)
    {
        if (fast) return FastSteps;
        if (lockstep) return 1;
        pending += Math.Min(frameSeconds, MaxFrame);
        int steps = (int)(pending / Game.Step);
        pending -= steps * Game.Step;
        return steps;
    }
}
