using Raylib_cs;

namespace StickAndPuck.Render;

/// <summary>
/// The mask pass colour for each player. The id goes straight into the red channel, so every
/// player on the ice (1 to 255) gets its own exact value and the post pass outlines each one
/// separately, even where two players overlap. Draw the model with <c>render.Mask</c> as its
/// shader and this colour as the tint.
/// </summary>
public static class MaskIds
{
    /// <summary>The most ids the mask holds; 0 means no player.</summary>
    public const int Max = 255;

    /// <summary>The mask colour for a player.</summary>
    /// <param name="maskId">The player's mask id, 1 to 255 (<c>Player.MaskId</c>).</param>
    /// <returns>The tint to draw the player with in the mask pass.</returns>
    public static Color For(int maskId)
    {
        if (maskId is < 1 or > Max) throw new ArgumentOutOfRangeException(nameof(maskId), maskId, $"mask ids run from 1 to {Max}");
        return new Color((byte)maskId, (byte)0, (byte)0, (byte)255);
    }
}
