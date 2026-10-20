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
    /// <summary>The arena from arena.glb: ice, markings, boards, glass, nets, ad boards, benches, penalty boxes and stands,
    /// with the animated crowd on the stands drawn by <see cref="CrowdRenderer"/> and reacting to the match through <see cref="CrowdReactions"/>.
    /// arena_meshes.json says which part and material each mesh is, since Raylib keeps no names.
    /// Lit parts use the toon shader; ad boards, lanterns and LEDs are unlit; the ice sheet is a
    /// flat cold grey and the glass a flat translucent tint, both from <see cref="ArenaLook"/> so
    /// their tones can follow the lighting without taking its warmth. The league layers (A17, <see cref="ArenaLayers"/>)
    /// pick per match between the 'SHL' centre mark and the home crest (<see cref="CentreIce"/>), the line colours,
    /// the rafter banners and the trapezoid.</summary>
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

        // the line colour a Markings or Trapezoid mesh takes in an NHL arena, if any
        private enum Line
        {
            None,
            Red,
            Blue,
        }

        private readonly List<(
            int Mesh,
            Kind Kind,
            bool Occludes,
            bool Ice,
            bool Sheet,
            string Part,
            Line Line
        )> meshes = [];
        private readonly Dictionary<int, Color> baseColours = []; // line and banner meshes' own colours
        private readonly CentreIce centreIce = new();
        private Maybe<bool> trapezoid = Maybe.None<bool>();
        private readonly IceBands bands = new();
        private readonly IceScratches scratches = new();
        private readonly CrowdRenderer crowd = new();
        private readonly CrowdReactions reactions = new();
        private readonly TifoRenderer tifo = new();
        private readonly TifoMoments tifoMoments = new();
        private readonly SupporterRenderer supporters = new();
        private Maybe<CrowdSection> tifoPreview; // SAP_TIFO: the home tifo held up over this section all match
        private bool posed;
        private bool standsView; // SAP_STANDS_VIEW=1: a close look at the far stands, for previews of the fans' scarves and flags
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
                    "Banners" => Kind.Unlit, // flat in the crowd's light: the toon ramp leaves faces square to the rink in shadow
                    "Bench" or "BenchDoors" => Kind.Lit, // the benches behind the far boards and the door outlines on them
                    "PenaltyBox" or "BoxDoors" => Kind.Lit, // the penalty boxes behind the near boards and their door outlines
                    "Lanterns" when string.Equals(name, "lantern", StringComparison.Ordinal) =>
                        Kind.Unlit, // the glowing shade, not cap or cable
                    _ => unlit ? Kind.Unlit : Kind.Lit,
                };
                Line line =
                    string.Equals(part, "Markings", StringComparison.Ordinal)
                    || string.Equals(part, "Trapezoid", StringComparison.Ordinal)
                        ? name switch
                        {
                            "red_line" => Line.Red,
                            "blue_line" => Line.Blue,
                            _ => Line.None,
                        }
                        : Line.None;
                meshes.Add(
                    (
                        index,
                        kind,
                        Occluders.Contains(part),
                        string.Equals(part, "Ice", StringComparison.Ordinal)
                            || string.Equals(part, "Floor", StringComparison.Ordinal),
                        string.Equals(part, "Ice", StringComparison.Ordinal),
                        part,
                        line
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
            foreach ((int mesh, _, _, _, _, string part, Line line) in meshes)
            {
                if (line != Line.None || string.Equals(part, "Banners", StringComparison.Ordinal))
                {
                    baseColours[mesh] = MaterialAccess.Albedo(MaterialOf(mesh)).Color; // the dusk colour an SHL arena keeps
                }
            }
            maskMaterial = LoadMaterialDefault();
            centreIce.Load();
            trapezoid = ArenaLayers.TrapezoidFromEnvironment();
            bands.Build();
            scratches.Load();
            LoadCrowd(world);
        }

        /// <summary>The crowd, its fans' sides, their scarves, flags and banners and the home tifo (W120), and
        /// the dev previews: SAP_CROWD_POSE, and SAP_TIFO (A19) holding the home club's tifo up over a section
        /// all match.</summary>
        private void LoadCrowd(World world)
        {
            crowd.Load(world);
            CrowdFans.Seat(reactions); // the visitors' corner reacts for the away club
            supporters.Load(world);
            tifoPreview = TifoRenderer.FromEnvironment();
            tifo.Load(world.Setup.Home);

            standsView = Env.Is("SAP_STANDS_VIEW", "1");
            if (Enum.TryParse(Env.Get("SAP_CROWD_POSE"), ignoreCase: true, out CrowdPose pose))
            {
                world.Crowd.SetAll(pose, world.Time); // a fixed pose for previews of the crowd frames
                posed = true;
            }
        }

        /// <summary>Gets the SAP_STANDS_VIEW preview camera: from over the near boards at the far stand's -x third
        /// and the edge of the centre, close enough that a scarf is more than a stripe.</summary>
        private static Camera3D StandsView =>
            new()
            {
                Position = new Vector3(-16f, 10f, 4f),
                Target = new Vector3(-12f, 4f, -24f),
                Up = Vector3.UnitY,
                FovY = 15f,
                Projection = CameraProjection.Perspective,
            };

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

        public void Update(World world, float dt)
        {
            if (!posed)
            {
                reactions.Update(world); // the crowd answers this step's goals, hits, saves, fights and calls
            }

            tifoMoments.Update(world); // the home tifo goes up at the start and after home goals
            if (standsView)
            {
                world.Camera = StandsView; // after the camera system, so it holds through goals and replays
            }
        }

        private Material MaterialOf(int mesh)
        {
            return model.MaterialsAsSpan()[model.MeshMaterialAsSpan()[mesh]];
        }

        public void DrawScene(World world, RenderContext render)
        {
            Shader shaderDefault = OperatingSystem.IsBrowser() ? render.Unlit : maskMaterial.Shader; // Raylib's default shader: texture times colour, no light
            ArenaLayers layers = Layers(world);
            // The goalie, referee and puck are drawn before the arena, so the ice sheet and the bands and
            // skate scratches on it all keep the depth test to stay behind them. The sheet writes no depth
            // while the overlays go down, since millimetres apart they z-fight at rink distance; a depth-only
            // pass of the sheet afterwards puts the ice depth back for the markings and everything else.
            DrawPass(
                render,
                shaderDefault,
                world.Look.Ice,
                world.Look.Crowd,
                layers,
                ice: true,
                sheet: false
            );
            Rlgl.DisableDepthMask();
            DrawPass(
                render,
                shaderDefault,
                world.Look.Ice,
                world.Look.Crowd,
                layers,
                ice: true,
                sheet: true
            );
            bands.Draw(world.Look.IceBand);
            if (layers.HomeCrest)
            {
                centreIce.Draw(world.Setup.Home.Id, CentreIce.Tone(world.Look.Ice)); // under the scratches, like a logo painted under the ice
            }

            scratches.Draw(world.Look.Scratch);
            Rlgl.EnableDepthMask();
            ColorWrite.Set(enabled: false);
            DrawPass(
                render,
                shaderDefault,
                world.Look.Ice,
                world.Look.Crowd,
                layers,
                ice: true,
                sheet: true
            );
            Rlgl.DrawRenderBatchActive();
            ColorWrite.Set(enabled: true);
            DrawPass(
                render,
                shaderDefault,
                world.Look.Ice,
                world.Look.Crowd,
                layers,
                ice: false,
                sheet: false
            );
            crowd.Draw(world, render, shaderDefault, world.Look.Crowd);
            supporters.Draw(world, render, shaderDefault, world.Look.Crowd);
            // glass last and without depth, so players behind it still draw; flat colour over the crowd
            Rlgl.DisableDepthMask();
            foreach ((int mesh, Kind kind, _, _, _, _, _) in meshes)
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
            // the tifo goes over the glass's tint that keeps the crowd back: it is the stands' big moment
            if (tifoPreview.TryGet(out CrowdSection tifoSection))
            {
                tifo.Draw(tifoSection, world.Time, shaderDefault, world.Look.Crowd);
            }
            else if (world.Settings.Supporters)
            {
                TifoState up = world.Crowd.Tifo;
                tifo.Draw(
                    up.Section,
                    world.Time - up.RaisedAt,
                    up.Lift(world.Time),
                    shaderDefault,
                    world.Look.Crowd
                );
            }
        }

        /// <summary>The league layers this match shows, the trapezoid by its rule unless SAP_TRAPEZOID overrides it.</summary>
        private ArenaLayers Layers(World world)
        {
            return ArenaLayers.For(
                world.League,
                ArenaLayers.TrapezoidShown(world.Setup.Rules, trapezoid)
            );
        }

        /// <summary>Draws one group of meshes with the current depth and colour state.</summary>
        /// <param name="render">The scene's shaders.</param>
        /// <param name="unlit">The shader for unlit parts.</param>
        /// <param name="iceTone">The ice sheet's tone.</param>
        /// <param name="crowdLight">The light on the crowd, which the rafter banners take too.</param>
        /// <param name="layers">The league layers this match shows.</param>
        /// <param name="ice">Whether to draw the ice group (the sheet and the floor) or everything else.</param>
        /// <param name="sheet">Within the ice group, whether to draw the sheet itself or the rest of it.</param>
        private void DrawPass(
            RenderContext render,
            Shader unlit,
            Color iceTone,
            Color crowdLight,
            ArenaLayers layers,
            bool ice,
            bool sheet
        )
        {
            foreach (
                (int mesh, Kind kind, _, bool isIce, bool isSheet, string part, Line line) in meshes
            )
            {
                if (
                    kind == Kind.Glass
                    || isIce != ice
                    || (ice && isSheet != sheet)
                    || !layers.Shows(part)
                )
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
                else if (line != Line.None)
                {
                    // set on every draw: the Logo shares blue_line with the markings, and the next match may be the other league
                    MaterialAccess.Albedo(mat).Color = layers.NhlLines
                        ? (line == Line.Red ? ArenaLayers.NhlRed : ArenaLayers.NhlBlue)
                        : baseColours[mesh];
                }
                else if (string.Equals(part, "Banners", StringComparison.Ordinal))
                {
                    Color c = baseColours[mesh];
                    MaterialAccess.Albedo(mat).Color = new Color(
                        (byte)(c.R * crowdLight.R / 255),
                        (byte)(c.G * crowdLight.G / 255),
                        (byte)(c.B * crowdLight.B / 255),
                        c.A
                    );
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

            foreach ((int mesh, _, bool occludes, _, _, _, _) in meshes)
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
            centreIce.Unload();
            crowd.Unload();
            tifo.Unload();
            supporters.Unload();
            UnloadMaterial(maskMaterial);
            UnloadModel(model);
        }
    }
}
