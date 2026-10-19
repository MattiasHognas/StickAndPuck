using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena
{
    /// <summary>A18: the crowd in the stands, one flat spectator on every seat of art/scene/crowd_seats.json,
    /// drawn from the frames in art/crowd/crowd_256.png in his section's <see cref="CrowdPose"/>.
    /// Each seat keeps a look, a shirt and an animation phase of its own; about two in five wear the home
    /// club's main colour and one in five its second colour, drawn as the look's shirt mask over his own
    /// clothes in that colour snapped to the arena palette, so the stands take the club's colours
    /// without leaving the palette; the visitors' corner (<see cref="CrowdFans"/>) wears the away club's. The spectators stand upright facing the camera (turned about
    /// the vertical only), back rows first with no depth writes, so each row covers the one behind
    /// and their empty texels hide nothing. Render only: it reads the world and changes nothing.</summary>
    internal sealed class CrowdRenderer
    {
        private const float PrimaryShare = 0.42f;
        private const float SecondaryShare = 0.18f;
        private const float AwayPrimaryShare = 0.62f; // the travelling fans nearly all wear the shirt
        private const float AwaySecondaryShare = 0.24f;
        private const float ClothesLight = 0.72f; // own clothes, skin and hair sit darker than the club colours, under the roof

        private Texture2D atlas;
        private Seat[] seats = [];

        internal readonly record struct Seat(
            Vector3 Foot,
            CrowdSection Section,
            int Look,
            float Phase,
            Color Shirt,
            bool Tinted
        );

        /// <summary>Loads the atlas and the seats, and dresses the seats in the home club's colours, the
        /// visitors' corner (<see cref="CrowdFans"/>) in the away club's.</summary>
        public void Load(World world)
        {
            atlas = Camera.FarTexture.Load(Assets.CrowdAtlas);
            KitColours home = world.Setup.Home.Home;
            KitColours away = world.Setup.Away.Home; // supporters wear their club's home shirt
            seats = Seats(
                ResourceLoader.ReadText(Assets.CrowdSeats),
                (Snap(home.Primary), Snap(home.Secondary)),
                (Snap(away.Primary), Snap(away.Secondary)),
                CrowdFans.Of
            );
        }

        /// <summary>Reads the seats file and gives every seat its look, shirt and phase.</summary>
        /// <param name="json">art/scene/crowd_seats.json.</param>
        /// <param name="primary">The home club's main colour, on the palette.</param>
        /// <param name="secondary">The home club's second colour, on the palette.</param>
        /// <returns>The seats, back rows first.</returns>
        internal static Seat[] Seats(string json, Color primary, Color secondary)
        {
            return Seats(json, (primary, secondary), (primary, secondary), _ => Side.Home);
        }

        /// <summary>Reads the seats file and gives every seat its look, shirt and phase, in the colours of
        /// the club its section supports; the away fans wear their shirts more often.</summary>
        /// <param name="json">art/scene/crowd_seats.json.</param>
        /// <param name="home">The home club's main and second colour, on the palette.</param>
        /// <param name="away">The away club's main and second colour, on the palette.</param>
        /// <param name="fans">The club each section supports.</param>
        /// <returns>The seats, back rows first.</returns>
        internal static Seat[] Seats(
            string json,
            (Color Primary, Color Secondary) home,
            (Color Primary, Color Secondary) away,
            Func<CrowdSection, Side> fans
        )
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            int sections = root.GetProperty("sections").GetArrayLength();
            if (sections != Enum.GetValues<CrowdSection>().Length)
            {
                throw new InvalidDataException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Assets.CrowdSeats} has {sections} sections, CrowdSection has {Enum.GetValues<CrowdSection>().Length}"
                    )
                );
            }

            List<Seat> list = [];
            foreach (JsonElement s in root.GetProperty("seats").EnumerateArray())
            {
                int i = list.Count;
                CrowdSection section = (CrowdSection)s[4].GetInt32();
                bool visitors = fans(section) == Side.Away;
                (Color primary, Color secondary) = visitors ? away : home;
                float primaryShare = visitors ? AwayPrimaryShare : PrimaryShare;
                float secondaryShare = visitors ? AwaySecondaryShare : SecondaryShare;
                float shirt = CrowdFrames.Unit(i, 1);
                bool tinted = shirt < primaryShare + secondaryShare;
                list.Add(
                    new Seat(
                        new Vector3(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle()),
                        section,
                        (int)(CrowdFrames.Hash(i, 2) % CrowdFrames.Looks),
                        CrowdFrames.Unit(i, 3),
                        shirt < primaryShare ? primary : secondary,
                        tinted
                    )
                );
            }

            return [.. list];
        }

        /// <summary>The nearest arena palette colour (never a kit colour) to a club colour.</summary>
        private static Color Snap(string hex)
        {
            Color c = HudArt.Hex(hex);
            Vector3 v = new(c.R / 255f, c.G / 255f, c.B / 255f);
            Vector3 best = Palette
                .Current.Take(Palette.KitStart)
                .MinBy(p => Vector3.DistanceSquared(p, v));
            return new Color(
                (byte)MathF.Round(best.X * 255f, MidpointRounding.ToEven),
                (byte)MathF.Round(best.Y * 255f, MidpointRounding.ToEven),
                (byte)MathF.Round(best.Z * 255f, MidpointRounding.ToEven),
                (byte)255
            );
        }

        /// <summary>Draws every spectator. Runs inside the scene's 3D camera after the stands.</summary>
        /// <param name="world">The world: the camera, the time and the sections' poses.</param>
        /// <param name="render">The scene's shaders and display poses.</param>
        /// <param name="unlit">The shader for flat textured draws.</param>
        /// <param name="tone">The crowd's light (<see cref="ArenaLook.Crowd"/>).</param>
        public void Draw(World world, RenderContext render, Shader unlit, Color tone)
        {
            if (atlas.Id == 0 || seats.Length == 0)
            {
                return;
            }

            Camera3D cam = render.Motion.Camera(world);
            Vector3 look = cam.Target - cam.Position;
            Vector3 right =
                Vector3.Normalize(new Vector3(-look.Z, 0f, look.X)) * (CrowdFrames.Width / 2f);
            Vector3 up = new(0f, CrowdFrames.Height, 0f);
            CrowdState state = world.Crowd;
            Color clothes = new(
                (byte)(tone.R * ClothesLight),
                (byte)(tone.G * ClothesLight),
                (byte)(tone.B * ClothesLight),
                (byte)255
            );
            const float du = (float)CrowdFrames.CellWidth / CrowdFrames.AtlasSize;
            const float dv = (float)CrowdFrames.CellHeight / CrowdFrames.AtlasSize;

            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            // the last arena mesh drawn with this shader left its material colour in colDiffuse
            int diffuse = GetShaderLocation(unlit, "colDiffuse");
            if (diffuse >= 0)
            {
                SetShaderValue(unlit, diffuse, Vector4.One, ShaderUniformDataType.Vec4);
            }

            Rlgl.DisableBackfaceCulling();
            Rlgl.DisableDepthMask();
            Rlgl.SetTexture(atlas.Id);
            foreach (Seat s in seats)
            {
                CrowdPose pose = state.Pose(s.Section);
                int col = CrowdFrames.Column(pose, world.Time - state.Since(s.Section), s.Phase);
                float u = col * du;
                Quad(s.Foot, right, up, u, s.Look * dv, du, dv, clothes);
                if (s.Tinted)
                {
                    Color shirt = new(
                        (byte)(s.Shirt.R * tone.R / 255),
                        (byte)(s.Shirt.G * tone.G / 255),
                        (byte)(s.Shirt.B * tone.B / 255),
                        (byte)255
                    );
                    Quad(s.Foot, right, up, u, (CrowdFrames.ShirtRow + s.Look) * dv, du, dv, shirt);
                }
            }

            Rlgl.SetTexture(0);
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
            Rlgl.EnableBackfaceCulling();
            EndShaderMode();
        }

        private static void Quad(
            Vector3 foot,
            Vector3 right,
            Vector3 up,
            float u,
            float v,
            float du,
            float dv,
            Color c
        )
        {
            _ = Rlgl.CheckRenderBatchLimit(4);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(c.R, c.G, c.B, c.A);
            Vector3 a = foot - right;
            Vector3 b = foot + right;
            Rlgl.TexCoord2f(u, v + dv);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.TexCoord2f(u + du, v + dv);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.TexCoord2f(u + du, v);
            Rlgl.Vertex3f(b.X + up.X, b.Y + up.Y, b.Z + up.Z);
            Rlgl.TexCoord2f(u, v);
            Rlgl.Vertex3f(a.X + up.X, a.Y + up.Y, a.Z + up.Z);
            Rlgl.End();
        }

        /// <summary>Frees the atlas.</summary>
        public void Unload()
        {
            if (atlas.Id != 0)
            {
                UnloadTexture(atlas);
            }

            atlas = default;
        }
    }
}
