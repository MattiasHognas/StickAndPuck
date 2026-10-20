using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena
{
    /// <summary>W120: the scarves, hand flags and banners the fans hold up (<see cref="SupporterProps"/>), the
    /// home sections from the home club's supporter sheet and the visitors' corner (<see cref="CrowdFans"/>)
    /// from the away club's, upright and facing the camera like the crowd. The sheets load with point sampling
    /// and no mipmaps (their small levels would turn the slogans to their outlines). Off with
    /// <see cref="Settings.Supporters"/>. Render only.</summary>
    internal sealed class SupporterRenderer
    {
        private readonly Texture2D[] sheets = new Texture2D[2];
        private readonly Dictionary<PropKind, SupporterArt.Piece> pieces = [];
        private SupporterProps.Prop[] props = [];

        /// <summary>Loads both clubs' sheets and picks the fans holding something up. Needs an open window; a
        /// club without a supporter sheet holds nothing up.</summary>
        /// <param name="world">The match: its clubs.</param>
        public void Load(World world)
        {
            sheets[(int)Side.Home] = Sheet(world.Setup.Home);
            sheets[(int)Side.Away] = Sheet(world.Setup.Away);
            SupporterArt art = SupporterArt.Bundled;
            foreach (PropKind kind in Enum.GetValues<PropKind>())
            {
                pieces[kind] = art[SupporterProps.Piece(kind)];
            }

            props = SupporterProps.Place(ResourceLoader.ReadText(Assets.CrowdSeats), CrowdFans.Of);
        }

        private static Texture2D Sheet(ClubData club)
        {
            string path = SupporterArt.Sheet(club);
            if (!ResourceLoader.Exists(path))
            {
                return default;
            }

            Texture2D t = ResourceLoader.Texture(path);
            SetTextureFilter(t, TextureFilter.Point);
            return t;
        }

        /// <summary>Draws every prop held up now. Runs inside the scene's 3D camera right after the crowd, so
        /// the glass's tint covers it like the fans.</summary>
        /// <param name="world">The world: the camera, the time, the settings and the sections' poses.</param>
        /// <param name="render">The scene's display poses.</param>
        /// <param name="unlit">The shader for flat textured draws.</param>
        /// <param name="tone">The crowd's light (<see cref="ArenaLook.Crowd"/>).</param>
        public void Draw(World world, RenderContext render, Shader unlit, Color tone)
        {
            if (!world.Settings.Supporters || props.Length == 0)
            {
                return;
            }

            Camera3D cam = render.Motion.Camera(world);
            Vector3 look = cam.Target - cam.Position;
            Vector3 right = Vector3.Normalize(new Vector3(-look.Z, 0f, look.X));
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            int diffuse = GetShaderLocation(unlit, "colDiffuse");
            if (diffuse >= 0)
            {
                SetShaderValue(unlit, diffuse, Vector4.One, ShaderUniformDataType.Vec4);
            }

            Rlgl.DisableBackfaceCulling();
            Rlgl.DisableDepthMask();
            foreach (Side side in (ReadOnlySpan<Side>)[Side.Home, Side.Away])
            {
                Texture2D sheet = sheets[(int)side];
                if (sheet.Id == 0)
                {
                    continue;
                }

                Rlgl.SetTexture(sheet.Id);
                foreach (SupporterProps.Prop p in props.Where(p => CrowdFans.Of(p.Section) == side))
                {
                    Prop(world, p, sheet, right, tone);
                }

                Rlgl.SetTexture(0);
                Rlgl.DrawRenderBatchActive();
            }

            Rlgl.EnableDepthMask();
            Rlgl.EnableBackfaceCulling();
            EndShaderMode();
        }

        private void Prop(
            World world,
            SupporterProps.Prop p,
            Texture2D sheet,
            Vector3 right,
            Color tone
        )
        {
            CrowdState state = world.Crowd;
            if (
                !SupporterProps
                    .For(
                        p.Kind,
                        state.Pose(p.Section),
                        world.Time - state.Since(p.Section),
                        p.Phase
                    )
                    .TryGet(out SupporterProps.Look l)
            )
            {
                return;
            }

            SupporterArt.Piece piece = pieces[p.Kind];
            Rectangle f = piece.Frames[l.Frame % piece.Frames.Length];
            float u0 = f.X / sheet.Width,
                u1 = (f.X + f.Width) / sheet.Width,
                v0 = f.Y / sheet.Height,
                v1 = (f.Y + f.Height) / sheet.Height;
            Vector3 middle =
                p.Foot
                + (right * l.Sway)
                + new Vector3(0f, SupporterProps.Height(p.Kind) + l.Rise, 0f);
            Vector3 half = right * (piece.Width / 2f);
            Vector3 up = new(0f, piece.Height / 2f, 0f);
            Vector3 a = middle - half - up,
                b = middle + half - up,
                c = middle + half + up,
                d = middle - half + up;
            _ = Rlgl.CheckRenderBatchLimit(4);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tone.R, tone.G, tone.B, 255);
            Rlgl.TexCoord2f(u0, v1);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.TexCoord2f(u1, v1);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.TexCoord2f(u1, v0);
            Rlgl.Vertex3f(c.X, c.Y, c.Z);
            Rlgl.TexCoord2f(u0, v0);
            Rlgl.Vertex3f(d.X, d.Y, d.Z);
            Rlgl.End();
        }

        /// <summary>Frees the sheets.</summary>
        public void Unload()
        {
            for (int i = 0; i < sheets.Length; i++)
            {
                if (sheets[i].Id != 0)
                {
                    UnloadTexture(sheets[i]);
                }

                sheets[i] = default;
            }
        }
    }
}
