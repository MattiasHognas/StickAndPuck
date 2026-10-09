namespace StickAndPuck.Goalie;

/// <summary>
/// The numbers that decide how a goalie plays. Metres, seconds, metres per second. Where a pair
/// is given, the first is a goalie with reflexes 0 and the second one with reflexes 100.
/// </summary>
public static class GoalieTuning
{
    /// <summary>How far out from the goal line a goalie stands when the puck is close.</summary>
    public const float DepthNear = 0.35f;

    /// <summary>How far out a goalie comes to cut the angle when the puck is in the slot or beyond.</summary>
    public const float DepthFar = 1.05f;

    /// <summary>How far inside the post a goalie's centre stands when hugging it, with the puck behind the goal line.</summary>
    public const float PostHug = 0.3f;

    /// <summary>Crease shuffle speed while following the puck.</summary>
    public const float ShuffleSpeed = 3.2f;

    /// <summary>Time to see a shot and start moving: slow and fast reflexes.</summary>
    public const float ReactSlow = 0.26f, ReactFast = 0.09f;

    /// <summary>Speed of the push or slide toward a shot once reacting: slow and fast reflexes.</summary>
    public const float SaveSpeedSlow = 3.2f, SaveSpeedFast = 6.5f;

    /// <summary>A puck heading at the net faster than this is a shot the goalie reacts to.</summary>
    public const float ShotSpeed = 4f;

    /// <summary>How wide of the posts a shot may be headed and still draw a reaction.</summary>
    public const float ShotMargin = 0.4f;

    /// <summary>Half the width the body covers standing up, at any height up to the shoulders.</summary>
    public const float BodyHalfWidth = 0.38f;

    /// <summary>Half the width pads cover along the ice in the butterfly.</summary>
    public const float ButterflyHalfWidth = 0.85f;

    /// <summary>Extra reach of the glove or blocker at mid height.</summary>
    public const float HandReach = 0.3f;

    /// <summary>Shots below this height are low: pads and butterfly.</summary>
    public const float LowShot = 0.45f;

    /// <summary>Shots above this height go over the shoulders.</summary>
    public const float ShoulderHeight = 1.3f;

    /// <summary>Chance a covered shot is stopped: slow and fast reflexes. The rest squeeze through.</summary>
    public const float CleanSlow = 0.87f, CleanFast = 0.99f;

    /// <summary>Above this speed a shot is harder to stop cleanly; the chance falls toward <see cref="HardShotPenalty"/> at 45 m/s.</summary>
    public const float HardShot = 28f;

    /// <summary>The most a very hard shot takes off the clean-stop chance.</summary>
    public const float HardShotPenalty = 0.12f;

    /// <summary>Chance to catch rather than leave a rebound: slow and fast reflexes, before height and speed.</summary>
    public const float CatchSlow = 0.4f, CatchFast = 0.75f;

    /// <summary>Share of the shot speed a rebound keeps: big rebounds from slow reflexes, soft ones from fast.</summary>
    public const float ReboundSlow = 0.42f, ReboundFast = 0.22f;

    /// <summary>A loose puck slower than this, near a goalie, is smothered.</summary>
    public const float SmotherSpeed = 2.5f;

    /// <summary>How close a slow puck must be to be smothered.</summary>
    public const float SmotherReach = 0.6f;

    /// <summary>Seconds a goalie holds a frozen puck before playing it out, if no whistle comes.</summary>
    public const float HoldSeconds = 2.5f;

    /// <summary>Speed a goalie plays a held puck out to the corner.</summary>
    public const float PlayOutSpeed = 9f;

    /// <summary>Seconds after a save or release during which the goalie ignores the puck.</summary>
    public const float IgnoreAfterSave = 0.35f;

    /// <summary>Seconds a goalie stays down in the butterfly after dropping.</summary>
    public const float ButterflySeconds = 0.8f;
}
