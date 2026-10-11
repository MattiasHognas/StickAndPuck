using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Arena;

/// <summary>
/// W2: the arena from arena.glb: ice, markings, boards, glass, nets, ad boards, stands and crowd.
/// arena_meshes.json says which part and material each mesh is, since Raylib keeps no names.
/// Lit parts use the toon shader; ad boards, lanterns and LEDs are unlit; the glass is a flat
/// translucent tint from <see cref="ArenaLook"/> so its tone can follow the lighting.
/// </summary>
internal sealed unsafe class ArenaSystem : ISystem, IDisposable
{
    enum Kind { Lit, Unlit, Glass }

    // parts in front of the ice that hide a player behind them, so the outline pass hides him too
    static readonly HashSet<string> Occluders = new(StringComparer.Ordinal) { "Boards", "AdBoards", "Nets", "Posts" };

    readonly List<(int Mesh, Kind Kind, bool Occludes, bool Ice, bool Sheet)> meshes = [];
    Model model;
    Material maskMaterial;

    public void Start(World world)
    {
        model = LoadModel(Assets.Arena);
        using var doc = JsonDocument.Parse(File.ReadAllText(Assets.ArenaMeshes));
        foreach (var m in doc.RootElement.GetProperty("meshes").EnumerateArray())
        {
            int index = m.GetProperty("index").GetInt32();
            string part = m.GetProperty("part").GetString() ?? "";
            string name = m.GetProperty("material").GetString() ?? "";
            if (index >= model.MeshCount) throw new InvalidDataException($"{Assets.ArenaMeshes} lists mesh {index}, arena.glb has {model.MeshCount}");
            bool unlit = m.GetProperty("unlit").GetBoolean();
            var kind = part switch
            {
                "Glass" => Kind.Glass,
                "LED" => Kind.Unlit,
                "Lanterns" when string.Equals(name, "lantern", StringComparison.Ordinal) => Kind.Unlit,   // the glowing shade, not cap or cable
                _ => unlit ? Kind.Unlit : Kind.Lit,
            };
            meshes.Add((index, kind, Occluders.Contains(part), part is "Ice" or "Floor", string.Equals(part, "Ice", StringComparison.Ordinal)));
        }

        // glTF colours are linear and Raylib uses them raw, which reads darker than the Blender
        // render; convert each material once. Textures (the ad strip) are already sRGB.
        for (int i = 0; i < model.MaterialCount; i++)
        {
            ref var albedo = ref model.Materials[i].Maps[(int)MaterialMapIndex.Albedo];
            albedo.Color = ToSrgb(albedo.Color);
            if (albedo.Texture.Id != 0) SetTextureFilter(albedo.Texture, TextureFilter.Point);
        }
        maskMaterial = LoadMaterialDefault();
        IceBands.Build();
        IceScratches.Load();
    }

    static Color ToSrgb(Color c)
    {
        static byte F(byte v)
        {
            float l = v / 255f;
            float s = l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
            return (byte)Math.Clamp(MathF.Round(s * 255f), 0, 255);
        }
        return new Color(F(c.R), F(c.G), F(c.B), c.A);
    }

    public void Update(World world, float dt) { }

    Material MaterialOf(int mesh)
    {
        return model.Materials[model.MeshMaterial[mesh]];
    }

    public void DrawScene(World world, RenderContext render)
    {
        var shaderDefault = maskMaterial.Shader;   // Raylib's default shader: texture times colour, no light
        // The goalie, referee and puck are drawn before the arena, so the ice sheet and the bands and
        // skate scratches on it all keep the depth test to stay behind them. The sheet writes no depth
        // while the overlays go down, since millimetres apart they z-fight at rink distance; a depth-only
        // pass of the sheet afterwards puts the ice depth back for the markings and everything else.
        DrawPass(render, shaderDefault, ice: true, sheet: false);
        Rlgl.DisableDepthMask();
        DrawPass(render, shaderDefault, ice: true, sheet: true);
        IceBands.Draw(ArenaLook.IceBand);
        IceScratches.Draw(ArenaLook.IceBand);
        Rlgl.EnableDepthMask();
        Rlgl.ColorMask(false, false, false, false);
        DrawPass(render, shaderDefault, ice: true, sheet: true);
        Rlgl.DrawRenderBatchActive();
        Rlgl.ColorMask(true, true, true, true);
        DrawPass(render, shaderDefault, ice: false, sheet: false);

        // glass last and without depth, so players behind it still draw; flat colour over the crowd
        Rlgl.DisableDepthMask();
        foreach (var (mesh, kind, _, _, _) in meshes)
        {
            if (kind != Kind.Glass) continue;
            var mat = MaterialOf(mesh);
            mat.Shader = shaderDefault;
            mat.Maps[(int)MaterialMapIndex.Albedo].Color = ArenaLook.Glass;
            DrawMesh(model.Meshes[mesh], mat, Matrix4x4.Identity);
        }
        Rlgl.EnableDepthMask();
    }

    /// <summary>Draws one group of meshes with the current depth and colour state.</summary>
    /// <param name="render">The scene's shaders.</param>
    /// <param name="unlit">The shader for unlit parts.</param>
    /// <param name="ice">Whether to draw the ice group (the sheet and the floor) or everything else.</param>
    /// <param name="sheet">Within the ice group, whether to draw the sheet itself or the rest of it.</param>
    void DrawPass(RenderContext render, Shader unlit, bool ice, bool sheet)
    {
        foreach (var (mesh, kind, _, isIce, isSheet) in meshes)
        {
            if (kind == Kind.Glass || isIce != ice || (ice && isSheet != sheet)) continue;
            var mat = MaterialOf(mesh);
            mat.Shader = kind == Kind.Lit ? render.Toon : unlit;
            DrawMesh(model.Meshes[mesh], mat, Matrix4x4.Identity);
        }
    }

    /// <summary>Boards, nets and posts into the mask pass's depth only, so a player behind them loses his outline there too.</summary>
    public void DrawMask(World world, RenderContext render)
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.ColorMask(false, false, false, false);
        foreach (var (mesh, _, occludes, _, _) in meshes)
            if (occludes) DrawMesh(model.Meshes[mesh], maskMaterial, Matrix4x4.Identity);
        Rlgl.ColorMask(true, true, true, true);
    }

    public void Dispose()
    {
        IceScratches.Unload();
        UnloadMaterial(maskMaterial);
        UnloadModel(model);
    }
}
