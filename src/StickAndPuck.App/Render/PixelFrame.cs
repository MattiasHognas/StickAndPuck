using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render;

/// <summary>The 480x270 frame and the pixel post pass. The scene is drawn into one render texture and
/// the players' flat ids into another; the post shader then snaps every pixel to the palette
/// from the .gpl file and draws a one-pixel outline around each player. The HUD has its own 480x270 target
/// that is laid on top untouched, and the result is scaled up by the largest whole number that
/// fits the window. Close shots draw the scene and mask at 240x135 or 160x90 instead
/// (<see cref="RenderScale"/>), so the post pass and its outline run on the coarser grid, and the
/// mask carries each pixel's depth so the post pass can also draw lines inside a player where an
/// arm or stick passes in front of the body. Toon band edges and palette snaps are dithered
/// with a 4x4 Bayer pattern on the scene's pixel grid, on players and on what stands inside the
/// boards only (see <see cref="Dither"/>).</summary>
public sealed class PixelFrame : IDisposable
{
    /// <summary>Shader used to render the scene with per-pixel toon shading.</summary>
    public readonly Shader Toon;

    /// <summary>High-precision unlit shader for WebGL; desktop keeps raylib's default.</summary>
    public readonly Shader Unlit;

    /// <summary>Shader used to generate the player mask texture.</summary>
    public readonly Shader Mask;

    /// <summary>Shader used in the final post-processing pass.</summary>
    public readonly Shader Post;

    /// <summary>Lines inside a player are drawn where the depth jumps by more than this many metres; only on the close scales.</summary>
    const float InnerDepth = 0.25f;

    /// <summary>Half the width, in light (0-1 of the toon ramp), of the dithered run at each toon band edge.</summary>
    const float BandWidth = 0.05f;

    /// <summary>How far either side of the midpoint between two palette colours the post pass mixes them.</summary>
    const float DitherSpread = 0.15f;

    /// <summary>Only palette colours closer than this (rgb distance, 0-1 channels) are mixed; farther pairs keep a hard edge.</summary>
    const float DitherGap = 0.18f;

    /// <summary>Gets whether dithering is on: SAP_DITHER=0 turns it off, for before and after captures.</summary>
    public static bool Dither { get; } = !Env.Is("SAP_DITHER", "0");

    readonly RenderTexture2D[] scenes, masks;
    readonly RenderTexture2D hud;
    readonly int width, height;
    readonly int texelLoc, innerLoc, originLoc;
    readonly RenderScale renderScale;
    readonly DitherGrid grid;
    int divisor = 1;

    /// <summary>Creates a pixel frame with the specified internal resolution, drawn at the scale the camera asks for, dithered from the grid's start.</summary>
    public PixelFrame(int width, int height, RenderScale scale, DitherGrid grid)
    {
        this.width = width; this.height = height;
        renderScale = scale;
        this.grid = grid;
        scenes = new RenderTexture2D[RenderScale.MaxDivisor];
        masks = new RenderTexture2D[RenderScale.MaxDivisor];
        for (int i = 0; i < RenderScale.MaxDivisor; i++)
        {
            scenes[i] = Target(width / (i + 1), height / (i + 1));
            masks[i] = Target(width / (i + 1), height / (i + 1));
        }
        hud = Target(width, height);

        if (OperatingSystem.IsBrowser()) Unlit = ResourceLoader.Shader("shaders/unlit.vs", "shaders/unlit.fs");
        Toon = ResourceLoader.Shader("shaders/toon.vs", "shaders/toon.fs");
        SetShaderValue(Toon, GetShaderLocation(Toon, "lightDir"), new Vector3(-0.45f, 0.75f, 0.5f), ShaderUniformDataType.Vec3);
        SetShaderValue(Toon, GetShaderLocation(Toon, "bandWidth"), Dither ? BandWidth : 0f, ShaderUniformDataType.Float);
        SetShaderValue(Toon, GetShaderLocation(Toon, "rink"), new Vector3(RinkSpec.HalfLength, RinkSpec.HalfWidth, RinkSpec.CornerRadius), ShaderUniformDataType.Vec3);
        Mask = ResourceLoader.FragmentShader("shaders/mask.fs");
        SetShaderValue(Mask, GetShaderLocation(Mask, "nearPlane"), (float)Rlgl.GetCullDistanceNear(), ShaderUniformDataType.Float);
        SetShaderValue(Mask, GetShaderLocation(Mask, "farPlane"), (float)Rlgl.GetCullDistanceFar(), ShaderUniformDataType.Float);
        Post = ResourceLoader.FragmentShader("shaders/post.fs");
        var palette = Palette.Current;
        SetShaderValueV(Post, GetShaderLocation(Post, "palette"), palette.ToArray(), ShaderUniformDataType.Vec3, palette.Count);
        SetShaderValue(Post, GetShaderLocation(Post, "paletteSize"), palette.Count, ShaderUniformDataType.Int);
        SetShaderValue(Post, GetShaderLocation(Post, "kitStart"), Palette.KitStart, ShaderUniformDataType.Int);
        SetShaderValue(Post, GetShaderLocation(Post, "outline"), Palette.Outline, ShaderUniformDataType.Vec3);
        SetShaderValue(Post, GetShaderLocation(Post, "ditherSpread"), Dither ? DitherSpread : 0f, ShaderUniformDataType.Float);
        SetShaderValue(Post, GetShaderLocation(Post, "ditherGap"), DitherGap, ShaderUniformDataType.Float);
        texelLoc = GetShaderLocation(Post, "texel");
        innerLoc = GetShaderLocation(Post, "innerDepth");
        originLoc = GetShaderLocation(Post, "ditherOrigin");
    }

    static RenderTexture2D Target(int w, int h)
    {
        var t = LoadRenderTexture(w, h);
        SetTextureFilter(t.Texture, TextureFilter.Point);
        return t;
    }

    /// <summary>Begins drawing into the scene texture, at the scale the camera asked for; the mask and post pass of this frame use the same one.</summary>
    public void BeginScene()
    {
        divisor = renderScale.Divisor;
        BeginTextureMode(scenes[divisor - 1]);
        ClearBackground(new Color(0x2a, 0x22, 0x30, 255));
    }

    /// <summary>Ends drawing into the scene texture.</summary>
    public static void EndScene()
    {
        EndTextureMode();
    }

    /// <summary>Begins drawing into the player mask texture.</summary>
    public void BeginMask() { BeginTextureMode(masks[divisor - 1]); ClearBackground(new Color(0, 0, 0, 0)); }

    /// <summary>Ends drawing into the player mask texture.</summary>
    public static void EndMask()
    {
        EndTextureMode();
    }

    /// <summary>Begins drawing into the HUD texture, in native 480x270 pixels.</summary>
    public void BeginHud() { BeginTextureMode(hud); ClearBackground(new Color(0, 0, 0, 0)); }

    /// <summary>Ends drawing into the HUD texture.</summary>
    public static void EndHud()
    {
        EndTextureMode();
    }

    /// <summary>Post pass and upscale into the current drawing target.</summary>
    public void Present()
    {
        int scale = Math.Max(1, Math.Min(GetScreenWidth() / width, GetScreenHeight() / height));
        int w = width * scale, h = height * scale;
        var dest = new Rectangle((GetScreenWidth() - w) / 2, (GetScreenHeight() - h) / 2, w, h);
        int sw = width / divisor, sh = height / divisor;
        var src = new Rectangle(0, 0, sw, -sh);     // render textures are stored upside down
        BeginShaderMode(Post);
        SetShaderValue(Post, texelLoc, new Vector2(1f / sw, 1f / sh), ShaderUniformDataType.Vec2);
        SetShaderValue(Post, innerLoc, divisor > 1 ? InnerDepth : 0f, ShaderUniformDataType.Float);
        SetShaderValue(Post, originLoc, grid.Origin, ShaderUniformDataType.Vec2);
        SetShaderValueTexture(Post, GetShaderLocation(Post, "maskTex"), masks[divisor - 1].Texture);
        DrawTexturePro(scenes[divisor - 1].Texture, src, dest, Vector2.Zero, 0, Color.White);
        EndShaderMode();
        DrawTexturePro(hud.Texture, new Rectangle(0, 0, width, -height), dest, Vector2.Zero, 0, Color.White);
    }

    /// <summary>Releases the render textures and shaders used by the frame.</summary>
    public void Dispose()
    {
        UnloadShader(Toon); UnloadShader(Mask); UnloadShader(Post);
        if (OperatingSystem.IsBrowser()) UnloadShader(Unlit);
        foreach (var t in scenes) UnloadRenderTexture(t);
        foreach (var t in masks) UnloadRenderTexture(t);
        UnloadRenderTexture(hud);
    }
}
