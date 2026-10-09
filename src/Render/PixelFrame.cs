using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Render;

/// <summary>
/// The 480x270 frame and the pixel post pass. The scene is drawn into one render texture and
/// the players' flat ids into another; the post shader then snaps every pixel to the palette
/// from the .gpl file and draws a one-pixel outline around each player. The HUD has its own 480x270 target
/// that is laid on top untouched, and the result is scaled up by the largest whole number that
/// fits the window.
/// </summary>
public sealed class PixelFrame : IDisposable
{
    /// <summary>
    /// Shader used to render the scene with per-pixel toon shading.
    /// </summary>
    public readonly Shader Toon;

    /// <summary>
    /// Shader used to generate the player mask texture.
    /// </summary>
    public readonly Shader Mask;

    /// <summary>
    /// Shader used in the final post-processing pass.
    /// </summary>
    public readonly Shader Post;

    readonly RenderTexture2D scene, mask, hud;
    readonly int width, height;

    /// <summary>
    /// Creates a pixel frame with the specified internal resolution.
    /// </summary>
    /// <param name="width">The width of the frame in pixels.</param>
    /// <param name="height">The height of the frame in pixels.</param>
    public PixelFrame(int width, int height)
    {
        this.width = width; this.height = height;
        scene = LoadRenderTexture(width, height);
        mask = LoadRenderTexture(width, height);
        hud = LoadRenderTexture(width, height);
        SetTextureFilter(scene.Texture, TextureFilter.Point);
        SetTextureFilter(mask.Texture, TextureFilter.Point);
        SetTextureFilter(hud.Texture, TextureFilter.Point);

        Toon = LoadShader("shaders/toon.vs", "shaders/toon.fs");
        SetShaderValue(Toon, GetShaderLocation(Toon, "lightDir"), new Vector3(-0.45f, 0.75f, 0.5f), ShaderUniformDataType.Vec3);
        Mask = LoadShader(null, "shaders/mask.fs");
        Post = LoadShader(null, "shaders/post.fs");
        var palette = Palette.Current;
        SetShaderValueV(Post, GetShaderLocation(Post, "palette"), palette.ToArray(), ShaderUniformDataType.Vec3, palette.Count);
        SetShaderValue(Post, GetShaderLocation(Post, "paletteSize"), palette.Count, ShaderUniformDataType.Int);
        SetShaderValue(Post, GetShaderLocation(Post, "kitStart"), Palette.KitStart, ShaderUniformDataType.Int);
        SetShaderValue(Post, GetShaderLocation(Post, "outline"), Palette.Outline, ShaderUniformDataType.Vec3);
        SetShaderValue(Post, GetShaderLocation(Post, "texel"), new Vector2(1f / width, 1f / height), ShaderUniformDataType.Vec2);
    }

    /// <summary>Begins drawing into the scene texture.</summary>
    public void BeginScene() { BeginTextureMode(scene); ClearBackground(new Color(0x2a, 0x22, 0x30, 255)); }

    /// <summary>Ends drawing into the scene texture.</summary>
    public static void EndScene()
    {
        EndTextureMode();
    }

    /// <summary>Begins drawing into the player mask texture.</summary>
    public void BeginMask() { BeginTextureMode(mask); ClearBackground(new Color(0, 0, 0, 0)); }

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
        var src = new Rectangle(0, 0, width, -height);     // render textures are stored upside down
        BeginShaderMode(Post);
        SetShaderValueTexture(Post, GetShaderLocation(Post, "maskTex"), mask.Texture);
        DrawTexturePro(scene.Texture, src, dest, Vector2.Zero, 0, Color.White);
        EndShaderMode();
        DrawTexturePro(hud.Texture, src, dest, Vector2.Zero, 0, Color.White);
    }

    /// <summary>Releases the render textures and shaders used by the frame.</summary>
    public void Dispose()
    {
        UnloadShader(Toon); UnloadShader(Mask); UnloadShader(Post);
        UnloadRenderTexture(scene); UnloadRenderTexture(mask); UnloadRenderTexture(hud);
    }
}
