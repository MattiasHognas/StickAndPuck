using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Arena;

/// <summary>
/// A4: skate scratches on the ice from art/scene/ice_scratches.png (made by
/// art/scripts/ice_scratches.py), a white mask over the whole rink tinted with the scratch tone,
/// so they stay a palette colour and follow the lighting; the tone's alpha fades them towards
/// the ice (W21). Drawn flat on the ice with the bands, with the depth test but no depth write,
/// so players drawn earlier stay in front.
/// </summary>
internal static class IceScratches
{
    const float Y = 0f;          // on the ice plane, after the bands; the sheet writes no depth while they go down
    static Texture2D texture;

    /// <summary>Loads the scratch mask once; a missing file just leaves the ice clean.</summary>
    public static void Load()
    {
        if (!File.Exists(Assets.IceScratches)) return;
        texture = LoadTexture(Assets.IceScratches);
        SetTextureFilter(texture, TextureFilter.Point);
    }

    /// <summary>Draws the scratches in the given tone whether or not the bands show. The caller has turned depth writes off.</summary>
    /// <param name="tone">The scratch colour; its alpha is how strong they are.</param>
    public static void Draw(Color tone)
    {
        if (texture.Id == 0) return;
        float x = RinkSpec.HalfLength, z = RinkSpec.HalfWidth;
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableBackfaceCulling();
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(tone.R, tone.G, tone.B, tone.A);
        Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(-x, Y, -z);
        Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(-x, Y, z);
        Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(x, Y, z);
        Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(x, Y, -z);
        Rlgl.End();
        Rlgl.SetTexture(0);
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
    }

    /// <summary>Frees the texture.</summary>
    public static void Unload()
    {
        if (texture.Id != 0) UnloadTexture(texture);
        texture = default;
    }
}
