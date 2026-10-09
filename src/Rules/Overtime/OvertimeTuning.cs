namespace StickAndPuck.Rules;

/// <summary>The numbers for overtime and the shootout. Real seconds and metres unless said.</summary>
public static class OvertimeTuning
{
    /// <summary>Skaters per club in overtime (SHL: 3 on 3).</summary>
    public const int OvertimeSkaters = 3;

    /// <summary>The break after regulation before the overtime faceoff, and before the shootout.</summary>
    public const float BreakSeconds = 4f;

    /// <summary>Shooters per club before the shootout goes to sudden death rounds.</summary>
    public const int Rounds = 5;

    /// <summary>The shooter waits at centre with the puck this long before the attempt starts.</summary>
    public const float ReadySeconds = 1.5f;

    /// <summary>The pause after an attempt before the next shooter comes out.</summary>
    public const float ResultSeconds = 1.5f;

    /// <summary>An attempt still going after this long is a miss.</summary>
    public const float AttemptSeconds = 12f;

    /// <summary>Once the puck has left the shooter's stick, the attempt ends a miss after this long.</summary>
    public const float LooseSeconds = 3f;

    /// <summary>A loose puck slower than this (metres per second) is dead: a miss.</summary>
    public const float DeadSpeed = 1.5f;

    /// <summary>An AI shooter skates in at this share of their top speed.</summary>
    public const float SkateIn = 0.85f;

    /// <summary>An AI shooter shoots from between these distances to the goal line.</summary>
    public const float ShootNear = 3.5f, ShootFar = 9f;

    /// <summary>An AI shooter's lane: up to this far either side of the middle of the rink.</summary>
    public const float LaneWidth = 2.5f;

    /// <summary>Chance an AI shooter dekes instead of shooting from distance: worst and best puck control.</summary>
    public const float DekeChance = 0.25f, DekeChanceBest = 0.75f;

    /// <summary>A deke starts cutting across this far from the goal line.</summary>
    public const float DekeFrom = 6f;

    /// <summary>A deke shoots once it is this far past the middle of the net.</summary>
    public const float DekeShootZ = 0.5f;
}
