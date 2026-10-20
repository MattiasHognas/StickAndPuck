using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena
{
    /// <summary>A19: the home club's tifo (its frames in the club's supporter sheet, <see cref="SupporterArt"/>)
    /// as a cloth raised over a stand section: lying on the hands of the fans, a little above their heads, from
    /// the front row up the slope, as wide as the section with the art's 4:3 shape. W120 raises it at the start of a match and after
    /// home goals (<see cref="TifoMoments"/>), passing it up the stand from the front row; SAP_TIFO holds it up
    /// all match to preview the art in the stands. Render only.</summary>
    internal sealed class TifoRenderer
    {
        /// <summary>How far above the seat the cloth lies, in metres: over the heads of the standing crowd.</summary>
        private const float Hold = 2.05f;

        /// <summary>How often the cloth ripples between its two frames, per second.</summary>
        private const float RippleHz = 1.5f;

        private readonly Dictionary<CrowdSection, Cloth> cloths = [];
        private Texture2D sheet;
        private Rectangle[] frames = [];

        /// <summary>A cloth's corners in the stands, as seen from the rink.</summary>
        /// <param name="TopLeft">The back row's end on the viewer's left.</param>
        /// <param name="TopRight">The back row's end on the viewer's right.</param>
        /// <param name="BottomRight">The front row's end on the viewer's right.</param>
        /// <param name="BottomLeft">The front row's end on the viewer's left.</param>
        [StructLayout(LayoutKind.Auto)]
        internal readonly record struct Cloth(
            Vector3 TopLeft,
            Vector3 TopRight,
            Vector3 BottomRight,
            Vector3 BottomLeft
        );

        /// <summary>Gets the section SAP_TIFO raises the tifo over: 1 for the far centre stand,
        /// or a section's name (far_left, left_end, ...); none when it is unset.</summary>
        public static Maybe<CrowdSection> FromEnvironment()
        {
            return Section(Env.Get("SAP_TIFO"));
        }

        /// <summary>Reads a section the way SAP_TIFO names it.</summary>
        /// <param name="value">1, or a section's name with or without underscores.</param>
        /// <returns>The section, none for an empty or unknown value.</returns>
        internal static Maybe<CrowdSection> Section(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal) ? CrowdSection.FarCentre
                : value.Length > 0
                && !value.All(char.IsDigit)
                && Enum.TryParse(
                    value.Replace("_", "", StringComparison.Ordinal),
                    ignoreCase: true,
                    out CrowdSection section
                )
                    ? section
                : Maybe.None<CrowdSection>();
        }

        /// <summary>Loads a club's tifo frames and the cloth over every section. Needs an open window; a club
        /// without a supporter sheet raises nothing.</summary>
        /// <param name="club">The home club.</param>
        public void Load(ClubData club)
        {
            string path = SupporterArt.Sheet(club);
            if (!ResourceLoader.Exists(path))
            {
                return;
            }

            SupporterArt.Piece tifo = SupporterArt.Bundled[SupporterArt.Tifo];
            frames = tifo.Frames;
            sheet = ResourceLoader.Texture(path); // no mip chain: its rarest-colour levels would turn the cloth to its ink outlines
            SetTextureFilter(sheet, TextureFilter.Point);
            Rectangle first = frames[0];
            foreach (
                (CrowdSection section, Cloth cloth) in Cloths(
                    ResourceLoader.ReadText(Assets.CrowdSeats),
                    first.Width / first.Height
                )
            )
            {
                cloths[section] = cloth;
            }
        }

        /// <summary>Lays a cloth over every section of the seats file.</summary>
        /// <param name="json">art/scene/crowd_seats.json.</param>
        /// <param name="aspect">The cloth's width over its height.</param>
        /// <returns>Each section's cloth.</returns>
        internal static IEnumerable<(CrowdSection Section, Cloth Cloth)> Cloths(
            string json,
            float aspect
        )
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            List<(Vector3 Foot, int Row, CrowdSection Section)> seats =
            [
                .. doc
                    .RootElement.GetProperty("seats")
                    .EnumerateArray()
                    .Select(s =>
                        (
                            new Vector3(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle()),
                            s[3].GetInt32(),
                            (CrowdSection)s[4].GetInt32()
                        )
                    ),
            ];
            foreach (
                IGrouping<
                    CrowdSection,
                    (Vector3 Foot, int Row, CrowdSection Section)
                > g in seats.GroupBy(s => s.Section)
            )
            {
                yield return (g.Key, Over([.. g.Select(s => (s.Foot, s.Row))], aspect));
            }
        }

        /// <summary>The cloth over one section: across the section as wide as it is (a seat's width more), up
        /// the slope from the front row to the back, centred on both, keeping the art's shape (narrower when the
        /// section is wider than its slope allows), held <see cref="Hold"/> over the seats.</summary>
        /// <param name="seats">The section's seats: foot position and row.</param>
        /// <param name="aspect">The cloth's width over its height.</param>
        /// <returns>The cloth.</returns>
        internal static Cloth Over(IReadOnlyList<(Vector3 Foot, int Row)> seats, float aspect)
        {
            int frontRow = seats.Min(s => s.Row);
            int backRow = seats.Max(s => s.Row);
            Vector3 front = Mean(seats.Where(s => s.Row == frontRow));
            Vector3 back = Mean(seats.Where(s => s.Row == backRow));
            Vector3 away = Vector3.Normalize(new Vector3(back.X - front.X, 0f, back.Z - front.Z));
            Vector3 right = new(-away.Z, 0f, away.X); // the viewer's right, facing the stand from the rink
            float min = seats.Min(s => Vector3.Dot(s.Foot - front, right)) - 0.5f;
            float max = seats.Max(s => Vector3.Dot(s.Foot - front, right)) + 0.5f;
            Vector3 slope = back - front;
            float length = slope.Length();
            Vector3 up = slope / length;
            float height = MathF.Min((max - min) / aspect, length + 2f);
            float width = height * aspect;
            Vector3 centre =
                ((front + back) / 2f) + (right * ((min + max) / 2f)) + new Vector3(0f, Hold, 0f);
            Vector3 half = right * (width / 2f);
            Vector3 rise = up * (height / 2f);
            return new Cloth(
                centre - half + rise,
                centre + half + rise,
                centre + half - rise,
                centre - half - rise
            );
        }

        private static Vector3 Mean(IEnumerable<(Vector3 Foot, int Row)> seats)
        {
            Vector3 sum = Vector3.Zero;
            int n = 0;
            foreach ((Vector3 foot, _) in seats)
            {
                sum += foot;
                n++;
            }

            return sum / n;
        }

        /// <summary>The part of a cloth that is up: from the front row to <paramref name="lift"/> of the way to
        /// the back, showing the top of the art first.</summary>
        /// <param name="full">The whole cloth.</param>
        /// <param name="lift">How far up it reaches, 0 to 1.</param>
        /// <returns>The cloth that is up.</returns>
        internal static Cloth Unrolled(Cloth full, float lift)
        {
            float k = Math.Clamp(lift, 0f, 1f);
            return full with
            {
                TopLeft = Vector3.Lerp(full.BottomLeft, full.TopLeft, k),
                TopRight = Vector3.Lerp(full.BottomRight, full.TopRight, k),
            };
        }

        /// <summary>Gets the middle of the cloth over a section, where the camera looks when it goes up.</summary>
        /// <param name="cloth">The cloth.</param>
        /// <returns>The point in the middle of its four corners.</returns>
        internal static Vector3 Middle(Cloth cloth)
        {
            return (cloth.TopLeft + cloth.TopRight + cloth.BottomRight + cloth.BottomLeft) / 4f;
        }

        /// <summary>Gets the frame the cloth shows: flat and rippled in turn.</summary>
        /// <param name="time">Seconds since it went up.</param>
        /// <returns>0 or 1.</returns>
        internal static int Frame(float time)
        {
            return (int)MathF.Floor(time * RippleHz * 2f) & 1;
        }

        /// <summary>Draws the tifo raised over a section, in the crowd's light. Runs inside the scene's 3D
        /// camera after the crowd, so it covers the fans holding it.</summary>
        /// <param name="section">The section under it.</param>
        /// <param name="time">Seconds since it went up.</param>
        /// <param name="unlit">The shader for flat textured draws.</param>
        /// <param name="tone">The crowd's light (<see cref="ArenaLook.Crowd"/>).</param>
        public void Draw(CrowdSection section, float time, Shader unlit, Color tone)
        {
            Draw(section, time, 1f, unlit, tone);
        }

        /// <summary>Draws the tifo part of the way up the stand: its top edge passed up from the front row,
        /// the cloth unrolling behind it.</summary>
        /// <param name="section">The section under it.</param>
        /// <param name="time">Seconds since it went up.</param>
        /// <param name="lift">How far up it reaches, 0 to 1 (<see cref="TifoState.Lift"/>).</param>
        /// <param name="unlit">The shader for flat textured draws.</param>
        /// <param name="tone">The crowd's light.</param>
        public void Draw(CrowdSection section, float time, float lift, Shader unlit, Color tone)
        {
            if (sheet.Id == 0 || lift <= 0f || !cloths.TryGetValue(section, out Cloth full))
            {
                return;
            }

            Cloth c = Unrolled(full, lift);
            Rectangle f = frames[Frame(time) % frames.Length];
            float u0 = f.X / sheet.Width,
                u1 = (f.X + f.Width) / sheet.Width,
                v0 = f.Y / sheet.Height,
                v1 = (f.Y + (f.Height * Math.Min(1f, lift))) / sheet.Height;
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            int diffuse = GetShaderLocation(unlit, "colDiffuse");
            if (diffuse >= 0)
            {
                SetShaderValue(unlit, diffuse, Vector4.One, ShaderUniformDataType.Vec4);
            }

            Rlgl.DisableBackfaceCulling();
            Rlgl.SetTexture(sheet.Id);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(tone.R, tone.G, tone.B, 255);
            Rlgl.TexCoord2f(u0, v1);
            Rlgl.Vertex3f(c.BottomLeft.X, c.BottomLeft.Y, c.BottomLeft.Z);
            Rlgl.TexCoord2f(u1, v1);
            Rlgl.Vertex3f(c.BottomRight.X, c.BottomRight.Y, c.BottomRight.Z);
            Rlgl.TexCoord2f(u1, v0);
            Rlgl.Vertex3f(c.TopRight.X, c.TopRight.Y, c.TopRight.Z);
            Rlgl.TexCoord2f(u0, v0);
            Rlgl.Vertex3f(c.TopLeft.X, c.TopLeft.Y, c.TopLeft.Z);
            Rlgl.End();
            Rlgl.SetTexture(0);
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableBackfaceCulling();
            EndShaderMode();
        }

        /// <summary>Frees the sheet.</summary>
        public void Unload()
        {
            if (sheet.Id != 0)
            {
                UnloadTexture(sheet);
            }

            sheet = default;
        }
    }
}
