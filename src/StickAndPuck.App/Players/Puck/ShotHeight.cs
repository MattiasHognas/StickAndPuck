using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>How high a shot goes. The longer shoot is held, the higher the shot: a tap skims the ice,
/// a full wind-up goes for the top of the net (about the crossbar). Shot accuracy sets how far the
/// height strays from that, and a high shot strays more than a low one, so a wild shooter's full
/// wind-up can sail over the net while a sharp shooter's tap stays on the ice.</summary>
public static class ShotHeight
{
    /// <summary>Shoot held this long or less is a tap: the lowest shot.</summary>
    public const float Tap = 0.08f;

    /// <summary>The aimed height at the net of a tap, in metres above the ice.</summary>
    public const float Low = 0.06f;

    /// <summary>The aimed height at the net of a full wind-up: just under the crossbar (the net is 1.22 m).</summary>
    public const float High = 1.05f;

    /// <summary>Upward speed of a tap that is not aimed at the net, in metres per second: it stays on the ice.</summary>
    public const float FlatLift = 0.3f;

    /// <summary>Upward speed of a full wind-up not aimed at the net: it clears sticks and lands far up the ice.</summary>
    public const float FullLift = 5f;

    /// <summary>Gets how much of the full lift a hold of shoot gives, from 0 (a tap) to 1 (a full wind-up).</summary>
    public static float Lift(float held)
    {
        float f = Math.Clamp((held - Tap) / (PuckHandling.FullCharge - Tap), 0f, 1f);
        return MathF.Sqrt(f);   // early hold lifts quickly, the last of the wind-up adds less
    }

    /// <summary>Gets how far a shot's height strays, as a share; 0 for a perfect shooter, 1 for the worst.</summary>
    public static float Stray(int accuracy)
    {
        return 1f - Traits.Unit(accuracy);
    }

    /// <summary>Gets the height a shot at the net crosses the goal line, before it strays.</summary>
    public static float Aim(float held)
    {
        return Low + ((High - Low) * Lift(held));
    }

    /// <summary>Gets the height a shot at the net crosses the goal line: the aim plus a stray set by shot accuracy.</summary>
    public static float AtNet(float held, int accuracy, Random rng)
    {
        float lift = Lift(held);
        float spread = (0.03f + (0.35f * Stray(accuracy))) * (0.3f + lift);
        return Math.Max(0.02f, Aim(held) + (PuckHandling.Gauss(rng) * spread));
    }

    /// <summary>Gets the upward speed of a shot not aimed at the net.</summary>
    public static float Upward(float held, int accuracy, Random rng)
    {
        float lift = Lift(held);
        float v = FlatLift + ((FullLift - FlatLift) * lift);
        return Math.Max(0f, v + (PuckHandling.Gauss(rng) * (0.2f + (1.5f * Stray(accuracy))) * lift));
    }
}
