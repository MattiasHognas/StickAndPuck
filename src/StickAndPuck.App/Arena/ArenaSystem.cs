using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena
{
    /// <summary>The arena from arena.glb: ice, markings, boards, glass, nets, ad boards, benches, penalty boxes, stands and crowd.
    /// arena_meshes.json says which part and material each mesh is, since Raylib keeps no names.
    /// Lit parts use the toon shader; ad boards, lanterns and LEDs are unlit; the ice sheet is a
    /// flat cold grey and the glass a flat translucent tint, both from <see cref="ArenaLook"/> so
    /// their tones can follow the lighting without taking its warmth.</summary>
    internal sealed class ArenaSystem : ISystem, IDisposable
    {
        private enum Kind
        {
            Lit,
            Unlit,
            Glass,
        }

        // parts in front of the ice that hide a player behind them, so the outline pass hides him too
        private static readonly FrozenSet<string> Occluders = new[]
        {
            "Boards",
            "BenchDoors",
            "BoxDoors",
            "AdBoards",
            "Nets",
            "Posts",
        }.ToFrozenSet(StringComparer.Ordinal);

        private readonly List<(int Mesh, Kind Kind, bool Occludes, bool Ice, bool Sheet)> meshes =
        [];
        private readonly IceBands bands = new();
        private readonly IceScratches scratches = new();
        private Model model;
        private Material maskMaterial;

        public void Start(World world)
        {
            model = ResourceLoader.Model(Assets.Arena);
            string jsonText = ResourceLoader.ReadText(Assets.ArenaMeshes);
            using JsonDocument doc = JsonDocument.Parse(jsonText);
            foreach (JsonElement m in doc.RootElement.GetProperty("meshes").EnumerateArray())
            {
                int index = m.GetProperty("index").GetInt32();
                string part = m.GetProperty("part").GetString() ?? "";
                string name = m.GetProperty("material").GetString() ?? "";
                if (index >= model.MeshCount)
                {
                    throw new InvalidDataException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{Assets.ArenaMeshes} lists mesh {index}, arena.glb has {model.MeshCount}"
                        )
                    );
                }

                bool unlit = m.GetProperty("unlit").GetBoolean();
                Kind kind = part switch
                {
                    "Glass" => Kind.Glass,
                    "LED" => Kind.Unlit,
                    "Bench" or "BenchDoors" => Kind.Lit, // the benches behind the far boards and the door outlines on them
                    "PenaltyBox" or "BoxDoors" => Kind.Lit, // the penalty boxes behind the near boards and their door outlines
                    "Lanterns" when string.Equals(name, "lantern", StringComparison.Ordinal) =>
                        Kind.Unlit, // the glowing shade, not cap or cable
                    _ => unlit ? Kind.Unlit : Kind.Lit,
                };
                meshes.Add(
                    (
                        index,
                        kind,
                        Occluders.Contains(part),
                        string.Equals(part, "Ice", StringComparison.Ordinal)
                            || string.Equals(part, "Floor", StringComparison.Ordinal),
                        string.Equals(part, "Ice", StringComparison.Ordinal)
                    )
                );
            }
            for (int i = 0; i < model.MaterialCount; i++)
            {
                ref MaterialMap albedo = ref MaterialAccess.Albedo(model.MaterialsAsSpan()[i]);
                albedo.Color = ToSrgb(albedo.Color);
                if (albedo.Texture.Id != 0)
                {
                    SetTextureFilter(albedo.Texture, TextureFilter.Point);
                }
            }
            maskMaterial = LoadMaterialDefault();
            bands.Build();
            scratches.Load();
        }

        private static Color ToSrgb(Color c)
        {
            static byte F(byte v)
            {
                float l = v / 255f;
                float s =
                    l <= 0.0031308f ? l * 12.92f : (1.055f * MathF.Pow(l, 1f / 2.4f)) - 0.055f;
                return (byte)Math.Clamp(MathF.Round(s * 255f, MidpointRounding.ToEven), 0, 255);
            }
            return new Color(F(c.R), F(c.G), F(c.B), c.A);
        }

        public void Update(World world, float dt) { }

        private Material MaterialOf(int mesh)
        {
            return model.MaterialsAsSpan()[model.MeshMaterialAsSpan()[mesh]];
        }

        public void DrawScene(World world, RenderContext render)
        {
            Shader shaderDefault = OperatingSystem.IsBrowser() ? render.Unlit : maskMaterial.Shader; // Raylib's default shader: texture times colour, no light
            // The goalie, referee and puck are drawn before the arena, so the ice sheet and the bands and
            // skate scratches on it all keep the depth test to stay behind them. The sheet writes no depth
            // while the overlays go down, since millimetres apart they z-fight at rink distance; a depth-only
            // pass of the sheet afterwards puts the ice depth back for the markings and everything else.
            DrawPass(render, shaderDefault, world.Look.Ice, ice: true, sheet: false);
            Rlgl.DisableDepthMask();
            DrawPass(render, shaderDefault, world.Look.Ice, ice: true, sheet: true);
            bands.Draw(world.Look.IceBand);
            scratches.Draw(world.Look.Scratch);
            Rlgl.EnableDepthMask();
            ColorWrite.Set(enabled: false);
            DrawPass(render, shaderDefault, world.Look.Ice, ice: true, sheet: true);
            Rlgl.DrawRenderBatchActive();
            ColorWrite.Set(enabled: true);
            DrawPass(render, shaderDefault, world.Look.Ice, ice: false, sheet: false);

            // glass last and without depth, so players behind it still draw; flat colour over the crowd
            Rlgl.DisableDepthMask();
            foreach ((int mesh, Kind kind, bool _, bool _, bool _) in meshes)
            {
                if (kind != Kind.Glass)
                {
                    continue;
                }

                Material mat = MaterialOf(mesh);
                mat.Shader = shaderDefault;
                MaterialAccess.Albedo(mat).Color = world.Look.Glass;
                DrawArenaMesh(mesh, mat);
            }
            Rlgl.EnableDepthMask();
        }

        /// <summary>Draws one group of meshes with the current depth and colour state.</summary>
        /// <param name="render">The scene's shaders.</param>
        /// <param name="unlit">The shader for unlit parts.</param>
        /// <param name="iceTone">The ice sheet's tone.</param>
        /// <param name="ice">Whether to draw the ice group (the sheet and the floor) or everything else.</param>
        /// <param name="sheet">Within the ice group, whether to draw the sheet itself or the rest of it.</param>
        private void DrawPass(
            RenderContext render,
            Shader unlit,
            Color iceTone,
            bool ice,
            bool sheet
        )
        {
            foreach ((int mesh, Kind kind, bool _, bool isIce, bool isSheet) in meshes)
            {
                if (kind == Kind.Glass || isIce != ice || (ice && isSheet != sheet))
                {
                    continue;
                }

                Material mat = MaterialOf(mesh);
                // the ice sheet is flat in the lighting's cold grey; the toon ramp would warm it under the evening light
                mat.Shader = kind == Kind.Lit && !isSheet ? render.Toon : unlit;
                if (isSheet)
                {
                    MaterialAccess.Albedo(mat).Color = iceTone;
                }

                DrawArenaMesh(mesh, mat);
            }
        }

        /// <summary>Boards, nets and posts into the mask pass's depth only, so a player behind them loses his outline there too.</summary>
        public void DrawMask(World world, RenderContext render)
        {
            Rlgl.DrawRenderBatchActive();
            ColorWrite.Set(enabled: false);
            Material material = maskMaterial;
            if (OperatingSystem.IsBrowser())
            {
                material.Shader = render.Unlit;
            }

            foreach ((int mesh, Kind _, bool occludes, bool _, bool _) in meshes)
            {
                if (occludes)
                {
                    DrawArenaMesh(mesh, material);
                }
            }

            ColorWrite.Set(enabled: true);
        }

        // Generic vertex attribute values belong to the context, not the VAO. raylib sets
        // white once in UploadMesh, but a later draw can change it. Reapply it for uncoloured
        // arena meshes before the default shader multiplies it into the ice/glass colour.
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        private unsafe void DrawArenaMesh(int index, Material material)
#pragma warning restore S6640
        {
            Mesh mesh = model.MeshesAsSpan()[index];
            int color = material.Shader.Locs[(int)ShaderLocationIndex.VertexColor];
            if (OperatingSystem.IsBrowser() && mesh.VboId[3] == 0 && color >= 0)
            {
                Vector4 white = Vector4.One;
                Rlgl.SetVertexAttributeDefault(color, &white, (int)ShaderAttributeDataType.Vec4, 4);
            }
            DrawMesh(mesh, material, Matrix4x4.Identity);
        }

        public void Dispose()
        {
            scratches.Unload();
            UnloadMaterial(maskMaterial);
            UnloadModel(model);
        }
    }
}
