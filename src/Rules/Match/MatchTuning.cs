namespace StickAndPuck.Rules;

/// <summary>Every pause in the match flow, in real seconds. Period length itself is <see cref="Core.MatchSetup.PeriodSeconds"/>.</summary>
public static class MatchTuning
{
    /// <summary>Intro before the opening faceoff (W18 dims the lights here).</summary>
    public const float IntroSeconds = 2f;

    /// <summary>Players lined up, puck on the dot, before it drops (unless the referee drops it).</summary>
    public const float FaceoffSeconds = 1f;

    /// <summary>Celebration after a goal before the centre faceoff (W9's replay fits in here).</summary>
    public const float GoalSeconds = 4f;

    /// <summary>After a whistle (offside, icing, a frozen puck) before the faceoff.</summary>
    public const float StoppageSeconds = 1.5f;

    /// <summary>A goalie holding the puck this long freezes it for a whistle (the goalie plays it out at 2.5 s otherwise).</summary>
    public const float FreezeSeconds = 1.2f;

    /// <summary>Between periods.</summary>
    public const float IntermissionSeconds = 4f;

    /// <summary>Length of an SHL 3 on 3 overtime period, for W7c to pass to <see cref="MatchFlow.StartPeriod"/>.</summary>
    public const float OvertimeSeconds = 5 * 60;
}
