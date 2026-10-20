using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>The dressing room (A20's backdrop with the club's jerseys and floor crest, as the dressing room
    /// screen draws it) on a flat wall behind the fired coach, so the 3D coach walks in front of the same room the
    /// season was spent in. The backdrop is drawn once into a texture with a mip chain, so the coarse close-up
    /// scale samples it averaged rather than skipping rows; a lit doorway is cut into its right-hand stalls.</summary>
    internal sealed class RoomWall : IDisposable
    {
        private static readonly Color DoorLight = new(0xf9, 0xda, 0x93, 0xff);
        private static readonly Color DoorFrame = new(0x2a, 0x22, 0x30, 0xff);
        private static readonly Color DoorLeaf = new(0x42, 0x32, 0x36, 0xff);

        private RenderTexture2D room;
        private Texture2D texture;
        private bool loaded;

        /// <summary>Paints the room in a club's colours. Needs an open window, outside any drawing.</summary>
        public void Load(ScreenArt art, ClubData club)
        {
            room = LoadRenderTexture(Game.Width, Game.Height);
            BeginTextureMode(room);
            ClearBackground(new Color(0x21, 0x1b, 0x28, 0xff));
            _ = art.DrawDressingRoom(club.Home, club.Id);
            EndTextureMode();
            texture = room.Texture;
            GenTextureMipmaps(ref texture);
            SetTextureFilter(texture, TextureFilter.Trilinear);
            loaded = true;
        }

        /// <summary>Draws the room filling a camera's view at a distance, dimmed by a light level, with a
        /// doorway at a point on it opened to a share (0 shut, 1 open).</summary>
        public void Draw(
            Camera3D camera,
            float distance,
            float light,
            Vector3 door,
            float open,
            Shader unlit
        )
        {
            if (!loaded)
            {
                return;
            }

            Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            Vector3 up = Vector3.Cross(right, forward);
            float halfH = distance * MathF.Tan(camera.FovY * MathF.PI / 360f) * 1.04f; // a touch over the frame, so no edge shows after the pixel snap
            float halfW = halfH * Game.Width / Game.Height;
            Vector3 c = camera.Position + (forward * distance);
            Vector3 bl = c - (right * halfW) - (up * halfH),
                br = c + (right * halfW) - (up * halfH),
                tr = c + (right * halfW) + (up * halfH),
                tl = c - (right * halfW) + (up * halfH);
            byte v = (byte)Math.Clamp(255f * light, 0f, 255f);
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            Rlgl.SetTexture(texture.Id);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(v, v, v, 255);
            Rlgl.TexCoord2f(0, 0);
            Rlgl.Vertex3f(bl.X, bl.Y, bl.Z);
            Rlgl.TexCoord2f(1, 0);
            Rlgl.Vertex3f(br.X, br.Y, br.Z);
            Rlgl.TexCoord2f(1, 1);
            Rlgl.Vertex3f(tr.X, tr.Y, tr.Z);
            Rlgl.TexCoord2f(0, 1);
            Rlgl.Vertex3f(tl.X, tl.Y, tl.Z);
            Rlgl.End();
            Rlgl.SetTexture(0);
            Rlgl.DrawRenderBatchActive();

            // the doorway: a dark frame, the light beyond, the leaf swinging across it as it shuts
            const float w = 1.25f,
                h = 2.35f,
                f = 0.12f;
            Vector3 n = -forward * 0.02f;
            Quad(
                door + n - (right * ((w / 2) + f)),
                right * (w + (2 * f)),
                up * (h + f),
                Dim(DoorFrame, light)
            );
            Quad(door + (n * 2) - (right * (w / 2)), right * w, up * h, DoorLight);
            float shut = 1f - Math.Clamp(open, 0f, 1f);
            if (shut > 0)
            {
                Quad(
                    door + (n * 3) + (right * (w / 2)) - (right * (w * shut)),
                    right * (w * shut),
                    up * h,
                    Dim(DoorLeaf, light)
                );
            }

            Rlgl.DrawRenderBatchActive();
            EndShaderMode();
        }

        private static Color Dim(Color c, float light)
        {
            float k = Math.Clamp(light, 0.25f, 1f);
            return new Color((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k), c.A);
        }

        private static void Quad(Vector3 corner, Vector3 across, Vector3 upward, Color colour)
        {
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(colour.R, colour.G, colour.B, colour.A);
            Vector3 a = corner,
                b = corner + across,
                c = corner + across + upward,
                d = corner + upward;
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.Vertex3f(c.X, c.Y, c.Z);
            Rlgl.Vertex3f(d.X, d.Y, d.Z);
            Rlgl.End();
        }

        public void Dispose()
        {
            if (loaded)
            {
                UnloadRenderTexture(room);
                loaded = false;
            }
        }
    }
}
