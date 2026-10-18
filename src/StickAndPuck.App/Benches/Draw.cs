using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace StickAndPuck.App.Benches
{
    /// <summary>
    /// Represents a single draw command for rendering a model at a world-space position and heading.
    /// </summary>
    /// <param name="Model">The model identifier to draw.</param>
    /// <param name="Clip">The animation clip index to use.</param>
    /// <param name="Frame">The frame index within the clip.</param>
    /// <param name="At">The position in world space.</param>
    /// <param name="Heading">The heading of the model in radians.</param>
    /// <param name="Atlas">The texture atlas used for rendering.</param>
    /// <param name="Mask">The tint mask applied to the draw call.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Draw(
        int Model,
        int Clip,
        int Frame,
        Vector3 At,
        float Heading,
        Texture2D Atlas,
        Color Mask
    );
}
