namespace StickAndPuck.Referee;

/// <summary>How the referee skates and where it stands, in metres and seconds.</summary>
public static class RefereeTuning
{
    /// <summary>Top skating speed.</summary>
    public const float TopSpeed = 8.5f;

    /// <summary>Acceleration and braking.</summary>
    public const float Acceleration = 9f;

    /// <summary>Turning speed in radians per second.</summary>
    public const float TurnRate = 9f;

    /// <summary>The referee stands this far behind the dot (toward the far boards) to drop the puck, so it never hides the draw from the camera.</summary>
    public const float DropOffset = 1.7f;

    /// <summary>Close enough to the drop spot to count as there.</summary>
    public const float Arrived = 0.15f;

    /// <summary>Standing still at the dot this long before the drop.</summary>
    public const float SetSeconds = 0.4f;

    /// <summary>A faceoff never waits longer than this: if the referee has not made it, the puck drops anyway (a safety net; the self test expects it never fires).</summary>
    public const float MaxWaitSeconds = 8f;

    /// <summary>After the drop the referee backs straight off toward the far boards for this long before following play.</summary>
    public const float BackOffSeconds = 0.6f;

    /// <summary>In play the referee trails the puck by this much along the rink, on the centre-ice side.</summary>
    public const float TrailX = 5f;

    /// <summary>In play the referee follows along the far boards at this z.</summary>
    public const float FollowZ = -(Core.RinkSpec.HalfWidth - 1.5f);

    /// <summary>The referee stays this far inside the goal lines, out of the corners and the crease.</summary>
    public const float MaxX = 20f;

    /// <summary>Keeps this much room around players and the puck while in play or skating to a dot.</summary>
    public const float AvoidRadius = 2.5f;

    /// <summary>How hard the referee steers away from a player or the puck inside <see cref="AvoidRadius"/>: at 1 m away, this many m/s per metre inside.</summary>
    public const float AvoidGain = 4f;

    /// <summary>The referee's starting spot before the intro: centre ice at the far boards.</summary>
    public static readonly System.Numerics.Vector3 Start = new(0, 0, FollowZ);
}
