using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Benches.Dev;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Benches
{
    /// <summary>Draws both clubs' benches: everyone dressed and off the ice in his club's kit and number,
    /// in the seats <see cref="BenchLayout"/> gives, and the skaters sitting in the penalty boxes, moved by <see cref="BenchCrowd"/>. Render only:
    /// the match runs the same with <see cref="Settings.Benches"/> on or off. Every clip frame is skinned
    /// once at load into static meshes, and a bench out of the camera's view is not drawn.</summary>
    internal sealed class BenchRenderer : ISystem, IDisposable
    {
        /// <summary>The mask ids of the staff (home coach, home trainer, away coach, away trainer), clear of the players', the Zamboni's and the referee's.</summary>
        private const int StaffMask = MaskIds.Max - 5;

        private readonly Dictionary<string, Texture2D> kits = [with(StringComparer.Ordinal)];
        private readonly List<Draw> draws = [];
        private readonly Model[] models = new Model[3];
        private readonly ModelAnimation[][] clips =
        [
            [],
            [],
            [],
        ];
        private readonly Dictionary<(int Model, string Clip), int> named = [];
        private readonly Dictionary<(int Model, int Clip, int Frame), Model[]> baked = [];
        private Maybe<BenchCrowd> crowd;
        private bool loaded;
        private long drawTicks;

        /// <summary>Gets the figures on the benches this step (none with the setting off).</summary>
        internal IReadOnlyList<BenchFigure> Figures =>
            crowd.TryGet(out BenchCrowd c) ? c.Shown : [];

        /// <summary>Gets the figures in the penalty boxes this step (none with the setting off).</summary>
        internal IReadOnlyList<BenchFigure> Boxed =>
            crowd.TryGet(out BenchCrowd c) ? c.InBoxes : [];

        /// <summary>Gets the benches' reactions and staff, once the setting has been on.</summary>
        internal Maybe<BenchReactions> Reactions { get; private set; }

        /// <summary>Gets the time the last frame's two bench passes took (ms).</summary>
        internal double DrawMs { get; private set; }

        public void Start(World world)
        {
            string[] paths = [Assets.Skater, Assets.Goalie, BenchStaffArt.Model];
            for (int m = 0; m < models.Length; m++)
            {
                models[m] = ResourceLoader.Model(paths[m]);
                clips[m] = ResourceLoader.Animations(paths[m]);
            }
            foreach ((int m, string name, bool stick) in BenchClips.All)
            {
                int c = Find(m, BenchClips.Prefix(m), name);
                if (c < 0)
                {
                    continue;
                }

                named[(m, name)] = c;
                if (!stick)
                {
                    Unarm(m, c, "stick");
                }
            }
            BakeAll();
            loaded = true;
        }

        /// <summary>Folds a bone away in every frame of one of this renderer's own clips: on the bench the stick
        /// would poke through the boards over the ad strip, so it shrinks into the hand.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        private unsafe void Unarm(int model, int c, string bone)
#pragma warning restore S6640
        {
            ModelSkeleton skeleton = models[model].Skeleton;
            int b = -1;
            for (int i = 0; i < skeleton.BoneCount; i++)
            {
                if (
                    string.Equals(
                        skeleton.BonesAsSpan()[i].NameToString(),
                        bone,
                        StringComparison.Ordinal
                    )
                )
                {
                    b = i;
                }
            }

            if (b < 0)
            {
                return;
            }

            ModelAnimation anim = clips[model][c];
            // raylib-cs 8.1.0 sizes GetKeyFramePoseAsSpan by KeyFrameCount, not BoneCount.
            // Direct access is still needed when this bone lies beyond that span's reported length.
            for (int k = 0; k < anim.KeyFrameCount; k++)
            {
                anim.KeyframePoses[k][b].Scale = new Vector3(0.001f);
            }
        }

        private int Find(int model, string prefix, string name)
        {
            return Array.FindIndex(
                clips[model],
                a =>
                    string.Equals(
                        a.NameToString(),
                        $"{prefix}_{name}",
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        public void Update(World world, float dt)
        {
            if (!world.Settings.Benches)
            {
                if (crowd.TryGet(out BenchCrowd off))
                {
                    off.Clear();
                }

                return;
            }
            if (!crowd.TryGet(out BenchCrowd c))
            {
                c = new BenchCrowd(Seeds.For(world.Seed, "bench", 104));
                crowd = c;
            }
            if (!Reactions.TryGet(out BenchReactions r))
            {
                r = new BenchReactions(Seeds.Dice(world.Seed, "bench", 105));
                Reactions = r;
            }
            IReadOnlyList<MatchEvent> events = world.Events.Current;
            bool fight = world.Fight.HasValue;
            if (BenchDemo.Of(world).TryGet(out BenchDemo demo))
            {
                // the bench scene's own script: its events in place of the match's
                demo.Run(world, DrawMs);
                events = demo.Events;
                fight = demo.FightOn;
            }
            if (BoxDemo.Of(world).TryGet(out BoxDemo box))
            {
                box.Run(world, DrawMs);
            }

            c.Update(world, dt);
            r.Update(world, c, events, fight, dt);
        }

        /// <summary>Picks each visible figure's and staff member's model, clip and whole frame, sorted so the same poses draw together.</summary>
        private void Plan(World world)
        {
            draws.Clear();
            if (!loaded || !world.Settings.Benches)
            {
                return;
            }

            (bool home, bool away, bool homeBox, bool awayBox) = Visible(world.Camera);
            float time = world.Time;
            PlanFigures(world, time, home, away, homeBox, awayBox);
            PlanStaff(time, home, away);
            draws.Sort(
                static (a, b) =>
                    (a.Model != b.Model)
                        ? (a.Model - b.Model)
                        : ((a.Clip != b.Clip) ? (a.Clip - b.Clip) : (a.Frame - b.Frame))
            );
        }

        private static (bool Home, bool Away, bool HomeBox, bool AwayBox) Visible(Camera3D camera)
        {
            return (
                InView(camera, BenchLayout.Span(Side.Home), BenchLayout.SeatZ),
                InView(camera, BenchLayout.Span(Side.Away), BenchLayout.SeatZ),
                InView(camera, BenchLayout.BoxSpan(Side.Home), BenchLayout.BoxSeatZ),
                InView(camera, BenchLayout.BoxSpan(Side.Away), BenchLayout.BoxSeatZ)
            );
        }

        private void PlanFigures(
            World world,
            float time,
            bool home,
            bool away,
            bool homeBox,
            bool awayBox
        )
        {
            foreach (BenchFigure f in Figures)
            {
                Add(world, f, f.Player.Side == Side.Home ? home : away, time);
            }

            foreach (BenchFigure f in Boxed)
            {
                Add(world, f, f.Player.Side == Side.Home ? homeBox : awayBox, time);
            }
        }

        private void PlanStaff(float time, bool home, bool away)
        {
            if (!Reactions.TryGet(out BenchReactions r))
            {
                return;
            }

            for (int i = 0; i < r.Staff.Count; i++)
            {
                BenchStaff s = r.Staff[i];
                if (
                    !(s.Side == Side.Home ? home : away)
                    || !Frame(BenchClips.Of(s, time), out int c, out int frame)
                )
                {
                    continue;
                }

                Vector3 spot = new(s.X, BenchStaff.StepY, BenchStaff.StepZ);
                draws.Add(
                    new Draw(
                        BenchClips.Staff,
                        c,
                        frame,
                        spot,
                        s.Heading,
                        StaffKit(s.Coach),
                        MaskIds.For(StaffMask + i)
                    )
                );
            }
        }

        private void Add(World world, BenchFigure f, bool seen, float time)
        {
            BenchLook look = BenchClips.Of(f, time);
            if (!seen || !Frame(look, out int c, out int frame))
            {
                return;
            }

            draws.Add(
                new Draw(
                    look.Model,
                    c,
                    frame,
                    Spot(f),
                    f.Heading,
                    Kit(world, f.Player, BenchClips.BareAt(f, time)),
                    MaskIds.For(f.Player.MaskId)
                )
            );
        }

        /// <summary>Gets a look's clip and whole frame.</summary>
        private bool Frame(BenchLook look, out int c, out int frame)
        {
            frame = 0;
            if (!named.TryGetValue((look.Model, look.Clip), out c))
            {
                return false;
            }

            int count = clips[look.Model][c].KeyFrameCount;
            int at = look.Hold >= 0 ? look.Hold : (int)(look.Seconds * BenchClips.Fps);
            frame =
                look.Loop && look.Hold < 0
                    ? ((at % count) + count) % count
                    : Math.Clamp(at, 0, count - 1);
            return true;
        }

        /// <summary>Gets whether any of a bench or penalty box, its x range at a depth, is inside the camera's view across the rink.</summary>
        private static bool InView(Camera3D camera, (float From, float To) span, float z)
        {
            Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            float half =
                MathF.Atan(MathF.Tan(camera.FovY * MathF.PI / 360f) * Game.Width / Game.Height)
                + 0.1f;
            (float from, float to) = span;
            for (float x = from; x <= to + 0.01f; x += (to - from) / 4f)
            {
                Vector3 d = new Vector3(x, 1f, z) - camera.Position;
                float across = Vector3.Dot(d, right),
                    ahead = Vector3.Dot(d, forward);
                if (ahead > 0 && MathF.Atan2(MathF.Abs(across), ahead) < half)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector3 Spot(BenchFigure f)
        {
            if (f.InBox)
            {
                return new Vector3(
                    f.X,
                    f.Standing ? 0f : BenchClips.BoxSeatLift,
                    f.Standing ? BenchLayout.BoxStandZ : BenchLayout.BoxSeatZ
                );
            }

            float lift = f.Standing
                ? 0f
                : (f.Player.IsGoalie ? BenchClips.GoalieLift : BenchClips.SeatLift);
            return new Vector3(f.X, lift, f.Standing ? BenchLayout.StandZ : BenchLayout.SeatZ);
        }

        private Texture2D Kit(World world, Player p, bool bare)
        {
            Side side = p.Side;
            if (world.Settings.SwapKits)
            {
                side = p.Side == Side.Home ? Side.Away : Side.Home;
            }

            string role = p.IsGoalie ? "goalie" : (bare ? "skater_bare" : "skater");
            return Texture(
                Assets.KitAtlas(world.Setup.Club(p.Side).Id, side, role, bare ? 0 : p.Data.Number)
            );
        }

        private Texture2D StaffKit(bool coach)
        {
            return Texture(coach ? BenchStaffArt.Coach : BenchStaffArt.Trainer);
        }

        private Texture2D Texture(string path)
        {
            if (!kits.TryGetValue(path, out Texture2D tex))
            {
                tex = Camera.FarTexture.Load(path); // mipmapped so stripes hold still at broadcast range
                kits[path] = tex;
            }
            return tex;
        }

        /// <summary>Skins every frame of the bench clips once, at the start, into static meshes that both passes
        /// then draw as they are: nothing is skinned while the match is on.</summary>
        private void BakeAll()
        {
            foreach (((int m, string _), int c) in named)
            {
                if (baked.ContainsKey((m, c, 0)))
                {
                    continue;
                }

                Model model = models[m];
                for (int frame = 0; frame < clips[m][c].KeyFrameCount; frame++)
                {
                    ModelPose.Apply(model, clips[m][c], frame);
                    Model[] posed = new Model[model.MeshCount];
                    for (int k = 0; k < model.MeshCount; k++)
                    {
                        posed[k] = LoadModelFromMesh(Bake(model.MeshesAsSpan()[k]));
                        posed[k].Transform = model.Transform;
                    }
                    baked[(m, c, frame)] = posed;
                }
            }
        }

        /// <summary>Draws the owned models of a baked pose with the source model's materials.</summary>
        private void DrawPosed(Draw draw, Color tint)
        {
            Model source = models[draw.Model];
            Model[] posed = baked[(draw.Model, draw.Clip, draw.Frame)];
            for (int i = 0; i < posed.Length; i++)
            {
                Model model = posed[i];
                ref Material material = ref model.MaterialsAsSpan()[0];
                Material owned = material;
                material = source.MaterialsAsSpan()[source.MeshMaterialAsSpan()[i]];
                try
                {
                    DrawModelEx(
                        model,
                        draw.At,
                        Vector3.UnitY,
                        draw.Heading * 180f / MathF.PI,
                        new Vector3(Players.Movement.SkaterRenderer.Scale),
                        tint
                    );
                }
                finally
                {
                    // Restore its own maps so UnloadModel never frees a borrowed source material.
                    material = owned;
                }
            }
        }

        /// <summary>Copies a skinned mesh's current pose into a new static mesh on the GPU.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        private static unsafe Mesh Bake(Mesh from)
#pragma warning restore S6640
        {
            Mesh to = new(from.VertexCount, from.TriangleCount);
            to.AllocVertices();
            ReadOnlySpan<float> vertices =
                (nint)from.AnimVertices != 0
                    ? new ReadOnlySpan<float>(from.AnimVertices, from.VertexCount * 3)
                    : from.VerticesAs<float>();
            vertices.CopyTo(to.VerticesAs<float>());
            if ((nint)from.Normals != 0)
            {
                to.AllocNormals();
                ReadOnlySpan<float> normals =
                    (nint)from.AnimNormals != 0
                        ? new ReadOnlySpan<float>(from.AnimNormals, from.VertexCount * 3)
                        : from.NormalsAs<float>();
                normals.CopyTo(to.NormalsAs<float>());
            }
            if ((nint)from.TexCoords != 0)
            {
                to.AllocTexCoords();
                from.TexCoordsAs<float>().CopyTo(to.TexCoordsAs<float>());
            }
            if ((nint)from.Colors != 0)
            {
                to.AllocColors();
                from.ColorsAs<byte>().CopyTo(to.ColorsAs<byte>());
            }
            if ((nint)from.Indices != 0)
            {
                to.AllocIndices();
                from.IndicesAs<ushort>().CopyTo(to.IndicesAs<ushort>());
            }
            UploadMesh(ref to, dynamic: false);
            return to;
        }

        public void DrawScene(World world, RenderContext render)
        {
            long from = Stopwatch.GetTimestamp();
            Plan(world);
            for (int i = 0; i < draws.Count; i++)
            {
                Draw d = draws[i];
                Model model = models[d.Model];
                for (int k = 0; k < model.MaterialCount; k++)
                {
                    model.MaterialsAsSpan()[k].Shader = render.Toon;
                    SetMaterialTexture(
                        ref model.MaterialsAsSpan()[k],
                        MaterialMapIndex.Albedo,
                        d.Atlas
                    );
                }
                DrawPosed(d, Color.White);
            }
            drawTicks = Stopwatch.GetTimestamp() - from;
        }

        /// <summary>The same poses in each figure's mask colour, so the bench gets outlines like the ice.</summary>
        public void DrawMask(World world, RenderContext render)
        {
            long from = Stopwatch.GetTimestamp();
            for (int i = 0; i < draws.Count; i++)
            {
                Draw d = draws[i];
                Model model = models[d.Model];
                for (int k = 0; k < model.MaterialCount; k++)
                {
                    model.MaterialsAsSpan()[k].Shader = render.Mask;
                }

                DrawPosed(d, d.Mask);
            }
            DrawMs = Stopwatch
                .GetElapsedTime(0, drawTicks + Stopwatch.GetTimestamp() - from)
                .TotalMilliseconds;
        }

        public void Dispose()
        {
            if (!loaded)
            {
                return;
            }

            foreach (Model[] posed in baked.Values)
            {
                foreach (Model model in posed)
                {
                    UnloadModel(model);
                }
            }
            baked.Clear();
            foreach (Texture2D t in kits.Values)
            {
                UnloadTexture(t);
            }

            foreach (Model m in models)
            {
                UnloadModel(m);
            }

            loaded = false;
        }
    }
}
