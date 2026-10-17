using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render;

/// <summary>Where the dither pattern starts on the scene's pixel grid. A pattern fixed to the screen
/// would slide over the ice each time the camera steps a pixel (the dots "crawl"); instead the
/// pattern starts at the pixel a fixed spot on the ice lands on, so when the camera steps by whole
/// pixels the pattern steps with the rink and the dithered bands stay put on it.</summary>
public sealed class DitherGrid
{
    /// <summary>Spots on the ice the pattern hangs from: centre ice first, then the two ends for a camera looking away from it.</summary>
    static readonly ImmutableArray<Vector3> Anchors = [Vector3.Zero, new(-26f, 0, 0), new(26f, 0, 0)];

    int originLoc = -1;

    /// <summary>Gets the pattern's start, in whole scene pixels from the bottom left (the shaders' gl_FragCoord convention).</summary>
    public Vector2 Origin { get; private set; }

    /// <summary>Finds the start for this frame's camera and hands it to the toon shader; call inside the scene pass.</summary>
    public void Apply(Camera3D camera, Shader toon, RenderScale scale)
    {
        int w = Game.Width / scale.Divisor, h = Game.Height / scale.Divisor;
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
        return Anchors
            .Where(a => Vector3.Dot(a - camera.Position, forward) > 1f)
            .DefaultIfEmpty(camera.Target)
            .First();
    }
}
