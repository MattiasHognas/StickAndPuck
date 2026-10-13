using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Intermission;

/// <summary>
/// Draws A7's Zamboni (art/zamboni/zamboni.glb: one mesh, its 64x64 texture embedded, no clips)
/// with the toon shader like the players, and in the mask pass with its own id so it gets its own
/// outline. Forward is +z, so heading 0 faces the camera; the origin is the wheelbase centre on the ice.
/// </summary>
internal sealed class ZamboniRenderer : IDisposable
{
    /// <summary>The model, next to the other art.</summary>
    public const string Asset = "art/zamboni/zamboni.glb";

    /// <summary>The Zamboni's mask id: the one below the referee's, far above any player's.</summary>
    public const int MaskId = MaskIds.Max - 1;

    Model model;
    bool loaded;

    /// <summary>Loads the model; a missing file leaves the intermission running without it.</summary>
    public void Load()
    {
        if (loaded || !File.Exists(Asset)) return;
        model = LoadModel(Asset);
        loaded = true;
    }

    public void DrawScene(Vector2 at, float heading, RenderContext render)
    {
        if (!loaded) return;
        for (int i = 0; i < model.MaterialCount; i++) SetShader(i, render.Toon);
        DrawModelEx(model, new Vector3(at.X, 0, at.Y), Vector3.UnitY, heading * 180f / MathF.PI, Vector3.One, Color.White);
    }

    public void DrawMask(Vector2 at, float heading, RenderContext render)
    {
        if (!loaded) return;
        for (int i = 0; i < model.MaterialCount; i++) SetShader(i, render.Mask);
        DrawModelEx(model, new Vector3(at.X, 0, at.Y), Vector3.UnitY, heading * 180f / MathF.PI, Vector3.One, MaskIds.For(MaskId));
    }

    public void Dispose()
    {
        if (!loaded) return;
        // UnloadModel keeps textures, which models may share; this one's embedded texture is its own
        for (int i = 0; i < model.MaterialCount; i++) UnloadOwnTexture(i);
        UnloadModel(model);
        loaded = false;
    }

    unsafe void UnloadOwnTexture(int material)
    {
        var texture = model.Materials[material].Maps[(int)MaterialMapIndex.Albedo].Texture;
        if (texture.Id != 0 && texture.Id != Rlgl.GetTextureIdDefault()) UnloadTexture(texture);
    }

    unsafe void SetShader(int material, Shader shader)
    {
        model.Materials[material].Shader = shader;
    }
}
