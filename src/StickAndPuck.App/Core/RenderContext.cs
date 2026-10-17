using Raylib_cs;

namespace StickAndPuck.App.Core;

/// <summary>The shaders a system needs to draw into the pixel frame. The renderer fills it once at start.</summary>
public sealed class RenderContext
{
    /// <summary>Gets the display poses, kept separate from simulation state.</summary>
    public Render.RenderInterpolation Motion { get; } = new();

    /// <summary>Gets the three-step toon shader for lit models.</summary>
    public Shader Toon { get; init; }

    /// <summary>Gets the high-precision WebGL shader for unlit 3D draws.</summary>
    public Shader Unlit { get; init; }

    /// <summary>Gets the flat-colour shader for the player mask pass; the draw colour is the player's mask id.</summary>
    public Shader Mask { get; init; }
}
