using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Render;

/// <summary>
/// W60: where the dither pattern starts on the scene's pixel grid. A pattern fixed to the screen
/// would slide over the ice each time the camera steps a pixel (the dots "crawl"); instead the
/// pattern starts at the pixel a fixed spot on the ice lands on, so when the camera steps by whole
/// pixels (W58) the pattern steps with the rink and the dithered bands stay put on it.
/// </summary>
public static class DitherGrid
{
    /// <summary>Spots on the ice the pattern hangs from: centre ice first, then the two ends for a camera looking away from it.</summary>
    static readonly Vector3[] Anchors = [Vector3.Zero, new(-26f, 0, 0), new(26f, 0, 0)];

    static int originLoc = -1;

    /// <summary>Gets the pattern's start, in whole scene pixels from the bottom left (the shaders' gl_FragCoord convention).</summary>
    public static Vector2 Origin { get; private set; }

    /// <summary>Finds the start for this frame's camera and hands it to the toon shader; call inside the scene pass.</summary>
    /// <param name="camera">The camera the scene is drawn with.</param>
    /// <param name="toon">The toon shader.</param>
    public static void Apply(Camera3D camera, Shader toon)
    {
        int w = Game.Width / RenderScale.Divisor, h = Game.Height / RenderScale.Divisor;
        var anchor = Anchor(camera);
        var p = GetWorldToScreenEx(anchor, camera, w, h);
        Origin = new Vector2(MathF.Round(p.X), MathF.Round(h - p.Y));
        if (originLoc < 0) originLoc = GetShaderLocation(toon, "ditherOrigin");
        SetShaderValue(toon, originLoc, Origin, ShaderUniformDataType.Vec2);
    }

    /// <summary>Centre ice when it is in front of the camera, otherwise the end in front of it.</summary>
    static Vector3 Anchor(Camera3D camera)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        foreach (var a in Anchors)
            if (Vector3.Dot(a - camera.Position, forward) > 1f) return a;
        return camera.Target;
    }
}
