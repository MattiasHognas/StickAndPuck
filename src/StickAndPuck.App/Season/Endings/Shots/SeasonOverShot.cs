using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Benches;
using StickAndPuck.App.Core;
using StickAndPuck.App.Intermission;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Season over: the arena after everyone has gone. The seats stand empty in the club's colours
    /// (one per spectator's place in art/scene/crowd_seats.json), the stands go dark a section at a time with the
    /// house lights clicking down, then the rink lights step down too; the Zamboni lays a last wet sheet across
    /// the ice in front of the home bench, where one player sits alone, head down. The camera looks down at him
    /// across the ice and creeps in.</summary>
    internal sealed class SeasonOverShot(EndingKit kit) : EndingShot(kit)
    {
        private static readonly Color Shade = new(0x18, 0x13, 0x1f, 0xff);
        private static readonly Color Wet = new(0xe3, 0xd8, 0xc6, 0xff);

        /// <summary>The order the stand sections go dark and when, in seconds.</summary>
        private static readonly (CrowdSection Section, float At)[] Dark =
        [
            (CrowdSection.RightEnd, 0.8f),
            (CrowdSection.NearRight, 0.8f),
            (CrowdSection.FarRight, 1.8f),
            (CrowdSection.NearCentre, 2.4f),
            (CrowdSection.FarCentre, 3.0f),
            (CrowdSection.FarLeft, 4.2f),
            (CrowdSection.NearLeft, 4.2f),
            (CrowdSection.LeftEnd, 5.2f),
        ];

        /// <summary>The rink lights' steps after the stands: when, and the level after.</summary>
        private static readonly (float At, float Level)[] Steps =
        [
            (6.0f, 0.84f),
            (7.0f, 0.7f),
            (8.0f, 0.58f),
        ];

        private const float ZamboniZ = -10.6f,
            ZamboniStart = 8f,
            ZamboniSpeed = 3.0f;

        private readonly ZamboniRenderer zamboni = new();
        private readonly Dictionary<CrowdSection, Vector3[]> covers = [];
        private readonly List<(
            Vector3 Foot,
            Vector3 Away,
            CrowdSection Section,
            bool Stripe
        )> seats = [];
        private Color seatColour,
            stripeColour;

        /// <summary>How far the slumped figure is lifted on his seat, so his head and shoulders clear the boards from
        /// the camera's height (the seat is hidden behind them).</summary>
        private const float SitUp = 0.6f;

        private static float SeatX => BenchLayout.DoorX(Side.Home) - 1.6f;

        public override void Load(ArenaSystem arena)
        {
            arena.CrowdShare = 0f;
            zamboni.Load();
            seatColour = EndingHud.Snap(Kit.Club.Home.Primary);
            stripeColour = EndingHud.Snap(Kit.Club.Home.Secondary);
            using JsonDocument doc = JsonDocument.Parse(ResourceLoader.ReadText(Assets.CrowdSeats));
            List<(Vector3 Foot, int Row, CrowdSection Section)> all =
            [
                .. doc
                    .RootElement.GetProperty("seats")
                    .EnumerateArray()
                    .Select(s =>
                        (
                            Foot: new Vector3(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle()),
                            Row: s[3].GetInt32(),
                            Section: (CrowdSection)s[4].GetInt32()
                        )
                    ),
            ];
            foreach (
                IGrouping<
                    CrowdSection,
                    (Vector3 Foot, int Row, CrowdSection Section)
                > g in all.GroupBy(s => s.Section)
            )
            {
                (Vector3[] cover, Vector3 away) = Cover([.. g.Select(s => (s.Foot, s.Row))]);
                covers[g.Key] = cover;
                foreach ((Vector3 foot, int row, CrowdSection section) in g)
                {
                    seats.Add((foot, away, section, row % 5 == 2));
                }
            }
        }

        /// <summary>A quad lying over a section's seats from the front row to over the back one, a little wider than
        /// them, that darkens the stand under it when it is drawn see-through, and the way the rows climb.</summary>
        private static (Vector3[] Quad, Vector3 Away) Cover(
            IReadOnlyList<(Vector3 Foot, int Row)> seats
        )
        {
            int front = seats.Min(s => s.Row),
                back = seats.Max(s => s.Row);
            Vector3 f = Mean(seats.Where(s => s.Row == front).Select(s => s.Foot));
            Vector3 b = Mean(seats.Where(s => s.Row == back).Select(s => s.Foot));
            Vector3 away = Vector3.Normalize(new Vector3(b.X - f.X, 0f, b.Z - f.Z));
            Vector3 right = new(-away.Z, 0f, away.X);
            float min = seats.Min(s => Vector3.Dot(s.Foot - f, right)) - 1.2f;
            float max = seats.Max(s => Vector3.Dot(s.Foot - f, right)) + 1.2f;
            Vector3 f0 = f - (away * 0.6f) + new Vector3(0f, 0.05f, 0f),
                b0 = b + (away * 0.8f) + new Vector3(0f, 3.0f, 0f);
            return (
                [f0 + (right * min), f0 + (right * max), b0 + (right * max), b0 + (right * min)],
                away
            );
        }

        private static Vector3 Mean(IEnumerable<Vector3> points)
        {
            Vector3 sum = Vector3.Zero;
            int n = 0;
            foreach (Vector3 p in points)
            {
                sum += p;
                n++;
            }

            return sum / Math.Max(1, n);
        }

        /// <summary>The rink's light level: full, then down in steps.</summary>
        private static float Level(float time)
        {
            float level = 1f;
            foreach ((float at, float l) in Steps)
            {
                if (time >= at)
                {
                    level = l;
                }
            }

            return level;
        }

        /// <summary>How far a section's house lights have gone down: 0 to 3 clicks.</summary>
        private static int Clicks(CrowdSection section, float time)
        {
            foreach ((CrowdSection s, float at) in Dark)
            {
                if (s == section)
                {
                    return (int)(Math.Clamp((time - at) / 0.5f, 0f, 1f) * 3f);
                }
            }

            return 0;
        }

        public override void Update(World world, float time)
        {
            float level = Level(time);
            world.Look.IceBand = world.Look.IceBand with { A = 0 };
            Color ice = Lighting.LightPresets.DimIce;
            world.Look.Ice = new Color(
                (byte)(ice.R * level),
                (byte)(ice.G * level),
                (byte)(ice.B * level),
                (byte)255
            );
            world.Look.Scratch = new Color(0xcc, 0xc9, 0xd2, 90);
            world.Look.Crowd = Color.White;
        }

        public override Tones Light(float time)
        {
            return Tones.Late.Times(Level(time));
        }

        public override Camera3D Camera(float time)
        {
            float u = Ease(time / 9.5f);
            Vector3 target = new(SeatX + 0.2f, 1.15f, BenchLayout.SeatZ + 0.2f);
            Vector3 from = new(SeatX + 2.4f - (0.6f * u), 3.3f - (0.3f * u), -5.4f - (1.2f * u));
            return Look(from, target, 34f);
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            PlayerData alone = EndingKit.Skaters(Kit.Club)[0];
            return
            [
                new Figure(
                    Body.Skater,
                    EndingKit.Kit(Kit.Club, Side.Home, alone),
                    "slump",
                    time,
                    new Vector3(SeatX, SitUp, BenchLayout.SeatZ),
                    0.25f,
                    1
                ),
            ];
        }

        private static float ZamboniX(float time)
        {
            return SeatX + ZamboniStart - (ZamboniSpeed * time);
        }

        public override void DrawSet(RenderContext render, Shader unlit, float time)
        {
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            Rlgl.DisableDepthMask();
            Rlgl.DisableBackfaceCulling();
            foreach ((CrowdSection section, Vector3[] q) in covers)
            {
                int clicks = Clicks(section, time);
                if (clicks > 0)
                {
                    Quad(q, Shade with { A = (byte)(52 * clicks) }); // the stand itself goes dark under it
                }
            }

            // the wet sheet behind the Zamboni, glossier than the dry ice
            float x = ZamboniX(time) + 2.3f;
            float from = SeatX + ZamboniStart + 4f;
            if (x < from)
            {
                Quad(
                    [
                        new(x, 0.012f, ZamboniZ + 1.05f),
                        new(from, 0.012f, ZamboniZ + 1.05f),
                        new(from, 0.012f, ZamboniZ - 1.05f),
                        new(x, 0.012f, ZamboniZ - 1.05f),
                    ],
                    Wet with
                    {
                        A = 80,
                    }
                );
            }

            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableDepthMask();
            DrawSeats(time);
            Rlgl.DrawRenderBatchActive();
            Rlgl.EnableBackfaceCulling();
            EndShaderMode();
        }

        /// <summary>Every seat: a back and a pan in the club's colours, one row in five in its second colour,
        /// dimmed with its section's house lights and the rink's.</summary>
        private void DrawSeats(float time)
        {
            float rink = Level(time);
            foreach ((Vector3 foot, Vector3 away, CrowdSection section, bool stripe) in seats)
            {
                float light = rink * (1f - (0.17f * Clicks(section, time)));
                Color c = stripe ? stripeColour : seatColour;
                Color back = Times(c, 0.85f * light),
                    pan = Times(c, 0.55f * light);
                Vector3 right = new Vector3(-away.Z, 0f, away.X) * 0.21f;
                Vector3 up = new(0f, 0.4f, 0f);
                Vector3 b0 = foot + (away * 0.16f) + new Vector3(0f, 0.42f, 0f);
                Vector3 p0 = foot - (away * 0.18f) + new Vector3(0f, 0.42f, 0f);
                _ = Rlgl.CheckRenderBatchLimit(8);
                Quad([b0 - right, b0 + right, b0 + right + up, b0 - right + up], back);
                Quad([p0 - right, p0 + right, b0 + right, b0 - right], pan);
            }
        }

        private static Color Times(Color c, float k)
        {
            return new Color((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k), (byte)255);
        }

        private static void Quad(Vector3[] q, Color c)
        {
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(c.R, c.G, c.B, c.A);
            foreach (Vector3 v in q)
            {
                Rlgl.Vertex3f(v.X, v.Y, v.Z);
            }

            Rlgl.End();
        }

        public override void DrawProps(RenderContext render, float time, bool mask)
        {
            Vector2 at = new(ZamboniX(time), ZamboniZ);
            if (mask)
            {
                zamboni.DrawMask(at, -MathF.PI / 2f, render);
            }
            else
            {
                zamboni.DrawScene(at, -MathF.PI / 2f, render);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                zamboni.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
