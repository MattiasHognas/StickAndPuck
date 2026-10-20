using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>The arena from outside at night (art/scene/exterior.glb, art/scripts/build_exterior.py): lit parts
    /// toon shaded, the lamp's glass, the lobby, the exit light and two windows unlit. The club's name and crest
    /// go on the dark name board in the game's font, the bulbs dead but for a couple of letters and one that
    /// flickers; the street lamp throws a cone and a pool of warm light.</summary>
    internal sealed class ExteriorSet : IDisposable
    {
        /// <summary>The asset names.</summary>
        public const string Asset = "art.scene.exterior.glb",
            Meshes = "art.scene.exterior_meshes.json";

        private static readonly Color DeadBulb = new(0x33, 0x29, 0x3a, 0xff);
        private static readonly Color LitBulb = new(0xf2, 0xc1, 0x4e, 0xff);
        private static readonly Color LampGlow = new(0xf9, 0xda, 0x93, 0xff);

        /// <summary>The pool of lamp light on the ground: two rings, the inner one stronger.</summary>
        private static readonly (float Radius, byte Alpha)[] Pools = [(3.6f, 30), (2.2f, 48)];

        private readonly List<(int Mesh, bool Unlit)> meshes = [];
        private Model model;
        private Material plain;
        private RenderTexture2D sign;
        private bool loaded;
        private string name = "";
        private int crestWidth;

        /// <summary>Gets the name board's front: left, right, bottom and top edges and its z.</summary>
        public (float X0, float X1, float Y0, float Y1, float Z) Board { get; private set; }

        /// <summary>Gets the street lamp's glass.</summary>
        public Vector3 Lamp { get; private set; }

        public void Load(ClubData club, HudArt art)
        {
            model = ResourceLoader.Model(Asset);
            using JsonDocument doc = JsonDocument.Parse(ResourceLoader.ReadText(Meshes));
            JsonElement root = doc.RootElement;
            foreach (JsonElement m in root.GetProperty("meshes").EnumerateArray())
            {
                int index = m.GetProperty("index").GetInt32();
                if (index >= model.MeshCount)
                {
                    throw new InvalidDataException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{Meshes} lists mesh {index}, the model has {model.MeshCount}"
                        )
                    );
                }

                meshes.Add((index, m.GetProperty("unlit").GetBoolean()));
            }

            JsonElement s = root.GetProperty("sign");
            Board = (
                s.GetProperty("x0").GetSingle(),
                s.GetProperty("x1").GetSingle(),
                s.GetProperty("y0").GetSingle(),
                s.GetProperty("y1").GetSingle(),
                s.GetProperty("z").GetSingle()
            );
            JsonElement l = root.GetProperty("lamp");
            Lamp = new Vector3(
                l.GetProperty("x").GetSingle(),
                l.GetProperty("y").GetSingle(),
                l.GetProperty("z").GetSingle()
            );
            for (int i = 0; i < model.MaterialCount; i++)
            {
                ref MaterialMap albedo = ref Render.MaterialAccess.Albedo(
                    model.MaterialsAsSpan()[i]
                );
                albedo.Color = ToSrgb(albedo.Color);
            }

            plain = LoadMaterialDefault();
            name = art.Printable(club.Name);
            crestWidth = 16;
            sign = LoadRenderTexture(crestWidth + 6 + art.Measure(name) + 2, 16);
            SetTextureFilter(sign.Texture, TextureFilter.Point);
            loaded = true;
        }

        private static Color ToSrgb(Color c)
        {
            static byte F(byte v)
            {
                float lin = v / 255f;
                float s =
                    lin <= 0.0031308f
                        ? lin * 12.92f
                        : (1.055f * MathF.Pow(lin, 1f / 2.4f)) - 0.055f;
                return (byte)Math.Clamp(MathF.Round(s * 255f, MidpointRounding.ToEven), 0, 255);
            }

            return new Color(F(c.R), F(c.G), F(c.B), c.A);
        }

        /// <summary>Paints the name board's letters for this moment: dead bulbs, a few lit, one flickering.
        /// Runs outside the 3D camera, before the scene pass.</summary>
        public void PaintSign(ClubData club, HudArt art, float time)
        {
            if (!loaded)
            {
                return;
            }

            BeginTextureMode(sign);
            ClearBackground(new Color(0x21, 0x1b, 0x28, 0xff));
            art.DrawCrest(
                club.Id,
                new Rectangle(0, 0, 16, 16),
                new Rectangle(0, 0, 16, 16),
                new Color(0x55, 0x4c, 0x5c, 0xff)
            );
            int x = crestWidth + 6;
            int letter = 0;
            bool flicker = (int)(time * 7.3f) % 5 != 0 && (int)(time * 2.1f) % 3 != 1;
            foreach (char c in name)
            {
                string glyph = c.ToString();
                bool lit = letter is 1 or 4 && c != ' ';
                bool flickering = letter == name.Length - 2 && flicker;
                art.DrawText(glyph, x, 4, lit || flickering ? LitBulb : DeadBulb);
                x += art.Measure(glyph) + 1;
                letter++;
            }

            EndTextureMode();
        }

        /// <summary>Draws the set; lit parts use the toon shader with whatever tones it has now.</summary>
        public void DrawScene(RenderContext render)
        {
            if (!loaded)
            {
                return;
            }

            Shader unlit = OperatingSystem.IsBrowser() ? render.Unlit : plain.Shader;
            Span<Material> materials = model.MaterialsAsSpan();
            Span<int> meshMaterial = model.MeshMaterialAsSpan();
            foreach ((int mesh, bool isUnlit) in meshes)
            {
                Material mat = materials[meshMaterial[mesh]];
                mat.Shader = isUnlit ? unlit : render.Toon;
                DrawMesh(model.MeshesAsSpan()[mesh], mat, Matrix4x4.Identity);
            }

            DrawSign(unlit);
        }

        private void DrawSign(Shader unlit)
        {
            (float x0, float x1, float y0, float y1, float z) = Board;
            float w = sign.Texture.Width,
                h = sign.Texture.Height;
            float height = (y1 - y0) * 0.62f;
            float width = height * w / h;
            float cx = (x0 + x1) / 2f,
                cy = (y0 + y1) / 2f;
            float l = cx - (width / 2f),
                r = cx + (width / 2f),
                b = cy - (height / 2f),
                t = cy + (height / 2f);
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            Rlgl.SetTexture(sign.Texture.Id);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(255, 255, 255, 255);
            Rlgl.TexCoord2f(0, 0); // render textures are stored upside down
            Rlgl.Vertex3f(l, b, z);
            Rlgl.TexCoord2f(1, 0);
            Rlgl.Vertex3f(r, b, z);
            Rlgl.TexCoord2f(1, 1);
            Rlgl.Vertex3f(r, t, z);
            Rlgl.TexCoord2f(0, 1);
            Rlgl.Vertex3f(l, t, z);
            Rlgl.End();
            Rlgl.SetTexture(0);
            Rlgl.DrawRenderBatchActive();
            EndShaderMode();
        }

        /// <summary>The lamp's cone and its pool of light on the wet pavement, see-through over the set.</summary>
        public void DrawLampLight(Shader unlit)
        {
            if (!loaded)
            {
                return;
            }

            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            Rlgl.DisableDepthMask();
            Rlgl.DisableBackfaceCulling();
            Vector3 ground = Lamp with { Y = 0.02f };
            DrawCylinderEx(ground, Lamp, 2.6f, 0.22f, 16, LampGlow with { A = 12 });
            Rlgl.Begin(DrawMode.Triangles);
            const int n = 20;
            for (int i = 0; i < n; i++)
            {
                float a0 = 2f * MathF.PI * i / n,
                    a1 = 2f * MathF.PI * (i + 1) / n;
                foreach ((float radius, byte alpha) in Pools)
                {
                    Rlgl.Color4ub(LampGlow.R, LampGlow.G, LampGlow.B, alpha);
                    Rlgl.Vertex3f(ground.X, ground.Y, ground.Z);
                    Rlgl.Vertex3f(
                        ground.X + (radius * MathF.Cos(a1)),
                        ground.Y,
                        ground.Z + (radius * 0.8f * MathF.Sin(a1))
                    );
                    Rlgl.Vertex3f(
                        ground.X + (radius * MathF.Cos(a0)),
                        ground.Y,
                        ground.Z + (radius * 0.8f * MathF.Sin(a0))
                    );
                }
            }

            Rlgl.End();
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableBackfaceCulling();
            Rlgl.EnableDepthMask();
            EndShaderMode();
        }

        public void Dispose()
        {
            if (!loaded)
            {
                return;
            }

            UnloadRenderTexture(sign);
            UnloadMaterial(plain);
            UnloadModel(model);
            loaded = false;
        }
    }
}
