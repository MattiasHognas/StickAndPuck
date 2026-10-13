using Raylib_cs;

namespace StickAndPuck.App.Render;

/// <summary>How the puck reads from the broadcast camera. On the ice it is the plain small puck. In the
/// air it grows a little (up to <see cref="MaxGrow"/> at <see cref="FullHeight"/>) and drops a crisp
/// shadow onto the ice in the darkest palette colour, a solid disc about the size of the puck that
/// shrinks a touch as the puck climbs, so the gap between puck and shadow shows the height.</summary>
public static class PuckLook
{
    /// <summary>The drawn radius of the puck on the ice, in metres (bigger than a real puck so it reads at 480x270).</summary>
    public const float Radius = 0.12f;

    /// <summary>The drawn thickness of the puck, in metres.</summary>
    public const float Thickness = 0.05f;

    /// <summary>A puck higher than this is airborne and casts a shadow.</summary>
    public const float AirborneAt = 0.04f;

    /// <summary>The height at which the puck has grown fully.</summary>
    public const float FullHeight = 1.2f;

    /// <summary>The most the puck grows in the air, as a scale on its size.</summary>
    public const float MaxGrow = 1.5f;

    /// <summary>The shadow's radius for a puck just off the ice.</summary>
    public const float ShadowRadius = 0.13f;

    /// <summary>The shadow's radius for a puck at <see cref="FullHeight"/> and above.</summary>
    public const float ShadowRadiusHigh = 0.1f;

    /// <summary>The shadow's height above the ice: above the painted lines and scratches, below anything standing on it.</summary>
    public const float ShadowLift = 0.006f;

    /// <summary>Gets a value indicating whether a puck at this height reads as airborne.</summary>
    public static bool Airborne(float height)
    {
        return height > AirborneAt;
    }

    /// <summary>Gets how far toward full height the puck is, 0 on the ice to 1 at <see cref="FullHeight"/>.</summary>
    public static float Rise(float height)
    {
        return Math.Clamp(height / FullHeight, 0f, 1f);
    }

    /// <summary>Gets the scale the puck is drawn at.</summary>
    public static float Scale(float height)
    {
        return Airborne(height) ? 1f + ((MaxGrow - 1f) * MathF.Sqrt(Rise(height))) : 1f;
    }

    /// <summary>Gets the radius of the shadow under an airborne puck.</summary>
    public static float Shadow(float height)
    {
        return ShadowRadius + ((ShadowRadiusHigh - ShadowRadius) * Rise(height));
    }

    /// <summary>Gets the shadow's colour: the darkest palette colour, so the post pass keeps it crisp and dark on any ice.</summary>
    public static Color ShadowColour
    {
        get
        {
            var c = Palette.Outline * 255f;
            return new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)255);
        }
    }
}
