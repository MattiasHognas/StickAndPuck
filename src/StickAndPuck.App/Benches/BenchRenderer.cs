using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Benches;

/// <summary>Draws both clubs' benches: everyone dressed and off the ice in his club's kit and number,
/// in the seats <see cref="BenchLayout"/> gives, and the skaters sitting in the penalty boxes, moved by <see cref="BenchCrowd"/>. Render only:
/// the match runs the same with <see cref="Settings.Benches"/> on or off. Every clip frame is skinned
/// once at load into static meshes, and a bench out of the camera's view is not drawn.</summary>
#pragma warning disable S6640 // Raylib mesh baking requires pointer-based manipulation of native model data.
internal sealed unsafe partial class BenchRenderer : ISystem, IDisposable
{
    /// <summary>The mask ids of the staff (home coach, home trainer, away coach, away trainer), clear of the players', the Zamboni's and the referee's.</summary>
    const int StaffMask = MaskIds.Max - 5;

    readonly Dictionary<string, Texture2D> kits = new(StringComparer.Ordinal);
    readonly List<Draw> draws = [];
    readonly Model[] models = new Model[3];
    readonly ModelAnimation[][] clips = [[], [], []];
    readonly Dictionary<(int Model, string Clip), int> named = [];
    readonly Dictionary<(int Model, int Clip, int Frame), nint> baked = [];
    Maybe<BenchCrowd> crowd;
    Maybe<BenchReactions> reactions;
    bool loaded;
    long drawTicks;

    /// <summary>Gets the figures on the benches this step (none with the setting off).</summary>
    internal IReadOnlyList<BenchFigure> Figures => crowd.TryGet(out var c) ? c.Shown : [];

    /// <summary>Gets the figures in the penalty boxes this step (none with the setting off).</summary>
    internal IReadOnlyList<BenchFigure> Boxed => crowd.TryGet(out var c) ? c.InBoxes : [];

    /// <summary>Gets the benches' reactions and staff, once the setting has been on.</summary>
    internal Maybe<BenchReactions> Reactions => reactions;

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
        foreach (var (m, name, stick) in BenchClips.All)
        {
            int c = Find(m, BenchClips.Prefix(m), name);
            if (c < 0) continue;
            named[(m, name)] = c;
            if (!stick) Unarm(m, c, "stick");
        }
        BakeAll();
        loaded = true;
    }

    /// <summary>Folds a bone away in every frame of one of this renderer's own clips: on the bench the stick
    /// would poke through the boards over the ad strip, so it shrinks into the hand.</summary>
    void Unarm(int model, int c, string bone)
    {
        var skeleton = models[model].Skeleton;
        int b = -1;
        for (int i = 0; i < skeleton.BoneCount; i++)
            if (string.Equals(Marshal.PtrToStringUTF8((IntPtr)skeleton.Bones[i].Name), bone, StringComparison.Ordinal)) b = i;
        if (b < 0) return;
        var anim = clips[model][c];
        for (int k = 0; k < anim.KeyFrameCount; k++) anim.KeyframePoses[k][b].Scale = new Vector3(0.001f);
    }

    int Find(int model, string prefix, string name)
    {
        return Array.FindIndex(clips[model], a => string.Equals(Marshal.PtrToStringUTF8((IntPtr)a.Name), $"{prefix}_{name}", StringComparison.OrdinalIgnoreCase));
    }

    public void Update(World world, float dt)
    {
        if (!world.Settings.Benches)
        {
            if (crowd.TryGet(out var off)) off.Clear();
            return;
        }
        if (!crowd.TryGet(out var c))
        {
            c = new BenchCrowd(Seeds.For(world.Seed, "bench", 104));
            crowd = c;
        }
        if (!reactions.TryGet(out var r))
        {
            r = new BenchReactions(Seeds.Dice(world.Seed, "bench", 105));
            reactions = r;
        }
        var events = world.Events.Current;
        bool fight = world.Fight.HasValue;
        if (Dev.BenchDemo.Of(world).TryGet(out var demo))
        {
            // the bench scene's own script: its events in place of the match's
            demo.Run(world, DrawMs);
            events = demo.Events;
            fight = demo.FightOn;
        }
        if (Dev.BoxDemo.Of(world).TryGet(out var box)) box.Run(world, DrawMs);
        c.Update(world, dt);
        r.Update(world, c, events, fight, dt);
    }

    /// <summary>Picks each visible figure's and staff member's model, clip and whole frame, sorted so the same poses draw together.</summary>
    void Plan(World world)
    {
        draws.Clear();
        if (!loaded || !world.Settings.Benches) return;
        var camera = world.Camera;
        bool home = InView(camera, BenchLayout.Span(Side.Home), BenchLayout.SeatZ), away = InView(camera, BenchLayout.Span(Side.Away), BenchLayout.SeatZ);
        bool homeBox = InView(camera, BenchLayout.BoxSpan(Side.Home), BenchLayout.BoxSeatZ), awayBox = InView(camera, BenchLayout.BoxSpan(Side.Away), BenchLayout.BoxSeatZ);
        float time = world.Time;
        foreach (var f in Figures) Add(world, f, f.Player.Side == Side.Home ? home : away, time);
        foreach (var f in Boxed) Add(world, f, f.Player.Side == Side.Home ? homeBox : awayBox, time);
        if (reactions.TryGet(out var r))
        {
            for (int i = 0; i < r.Staff.Count; i++)
            {
                var s = r.Staff[i];
                if (!(s.Side == Side.Home ? home : away) || !Frame(BenchClips.Of(s, time), out int c, out int frame)) continue;
                var spot = new Vector3(s.X, BenchStaff.StepY, BenchStaff.StepZ);
                draws.Add(new Draw(BenchClips.Staff, c, frame, spot, s.Heading, StaffKit(s.Coach), MaskIds.For(StaffMask + i)));
            }
        }
        draws.Sort(static (a, b) =>
        {
            if (a.Model != b.Model) return a.Model - b.Model;
            if (a.Clip != b.Clip) return a.Clip - b.Clip;
            return a.Frame - b.Frame;
        });
    }

    void Add(World world, BenchFigure f, bool seen, float time)
    {
        var look = BenchClips.Of(f, time);
        if (!seen || !Frame(look, out int c, out int frame)) return;
        draws.Add(new Draw(look.Model, c, frame, Spot(f), f.Heading, Kit(world, f.Player, BenchClips.BareAt(f, time)), MaskIds.For(f.Player.MaskId)));
    }

    /// <summary>Gets a look's clip and whole frame.</summary>
    bool Frame(BenchLook look, out int c, out int frame)
    {
        frame = 0;
        if (!named.TryGetValue((look.Model, look.Clip), out c)) return false;
        int count = clips[look.Model][c].KeyFrameCount;
        int at = look.Hold >= 0 ? look.Hold : (int)(look.Seconds * BenchClips.Fps);
        frame = look.Loop && look.Hold < 0 ? ((at % count) + count) % count : Math.Clamp(at, 0, count - 1);
        return true;
    }

    /// <summary>Gets whether any of a bench or penalty box, its x range at a depth, is inside the camera's view across the rink.</summary>
    static bool InView(Camera3D camera, (float From, float To) span, float z)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        float half = MathF.Atan(MathF.Tan(camera.FovY * MathF.PI / 360f) * Game.Width / Game.Height) + 0.1f;
        var (from, to) = span;
        for (float x = from; x <= to + 0.01f; x += (to - from) / 4f)
        {
            var d = new Vector3(x, 1f, z) - camera.Position;
            float across = Vector3.Dot(d, right), ahead = Vector3.Dot(d, forward);
            if (ahead > 0 && MathF.Atan2(MathF.Abs(across), ahead) < half) return true;
        }
        return false;
    }

    static Vector3 Spot(BenchFigure f)
    {
        if (f.InBox) return new Vector3(f.X, f.Standing ? 0f : BenchClips.BoxSeatLift, f.Standing ? BenchLayout.BoxStandZ : BenchLayout.BoxSeatZ);

        float lift;
        if (f.Standing)
            lift = 0f;
        else if (f.Player.IsGoalie)
            lift = BenchClips.GoalieLift;
        else
            lift = BenchClips.SeatLift;

        return new Vector3(f.X, lift, f.Standing ? BenchLayout.StandZ : BenchLayout.SeatZ);
    }

    Texture2D Kit(World world, Player p, bool bare)
    {
        var side = p.Side;
        if (world.Settings.SwapKits)
            side = p.Side == Side.Home ? Side.Away : Side.Home;

        string role;
        if (p.IsGoalie)
            role = "goalie";
        else if (bare)
            role = "skater_bare";
        else
            role = "skater";
        return Texture(Assets.KitAtlas(world.Setup.Club(p.Side).Id, side, role, bare ? 0 : p.Data.Number));
    }

    Texture2D StaffKit(bool coach) => Texture(coach ? BenchStaffArt.Coach : BenchStaffArt.Trainer);

    Texture2D Texture(string path)
    {
        if (!kits.TryGetValue(path, out var tex))
        {
            tex = Camera.FarTexture.Load(path);   // mipmapped so stripes hold still at broadcast range
            kits[path] = tex;
        }
        return tex;
    }

    /// <summary>Skins every frame of the bench clips once, at the start, into static meshes that both passes
    /// then draw as they are: nothing is skinned while the match is on.</summary>
    void BakeAll()
    {
        foreach (var ((m, _), c) in named)
        {
            if (baked.ContainsKey((m, c, 0))) continue;
            var model = models[m];
            for (int frame = 0; frame < clips[m][c].KeyFrameCount; frame++)
            {
                ModelPose.Apply(model, clips[m][c], frame);
                var meshes = (Mesh*)NativeMemory.Alloc((nuint)model.MeshCount, (nuint)sizeof(Mesh));
                for (int k = 0; k < model.MeshCount; k++) meshes[k] = Bake(model.Meshes[k]);
                baked[(m, c, frame)] = (nint)meshes;
            }
        }
    }

    /// <summary>Gets the model in a draw's baked pose.</summary>
    Model Posed(int i)
    {
        var d = draws[i];
        var model = models[d.Model];
        model.Meshes = (Mesh*)baked[(d.Model, d.Clip, d.Frame)];
        return model;
    }

    /// <summary>Copies a skinned mesh's current pose into a new static mesh on the GPU.</summary>
    static Mesh Bake(Mesh from)
    {
        var to = new Mesh(from.VertexCount, from.TriangleCount);
        to.AllocVertices();
        Copy(Has(from.AnimVertices) ? from.AnimVertices : from.Vertices, to.Vertices, from.VertexCount * 3 * sizeof(float));
        if (Has(from.Normals))
        {
            to.AllocNormals();
            Copy(Has(from.AnimNormals) ? from.AnimNormals : from.Normals, to.Normals, from.VertexCount * 3 * sizeof(float));
        }
        if (Has(from.TexCoords))
        {
            to.AllocTexCoords();
            Copy(from.TexCoords, to.TexCoords, from.VertexCount * 2 * sizeof(float));
        }
        if (Has(from.Colors))
        {
            to.AllocColors();
            Copy(from.Colors, to.Colors, from.VertexCount * 4);
        }
        if (Has(from.Indices))
        {
            to.AllocIndices();
            Copy(from.Indices, to.Indices, from.TriangleCount * 3 * sizeof(ushort));
        }
        UploadMesh(ref to, false);
        return to;
    }

    static bool Has(void* data) => (nint)data != 0;

    static void Copy(void* from, void* to, int bytes) => Buffer.MemoryCopy(from, to, bytes, bytes);

    public void DrawScene(World world, RenderContext render)
    {
        long from = Stopwatch.GetTimestamp();
        Plan(world);
        for (int i = 0; i < draws.Count; i++)
        {
            var d = draws[i];
            var model = Posed(i);
            for (int k = 0; k < model.MaterialCount; k++)
            {
                model.Materials[k].Shader = render.Toon;
                SetMaterialTexture(ref model.Materials[k], MaterialMapIndex.Albedo, d.Atlas);
            }
            DrawModelEx(model, d.At, Vector3.UnitY, d.Heading * 180f / MathF.PI, new Vector3(Players.SkaterRenderer.Scale), Color.White);
        }
        drawTicks = Stopwatch.GetTimestamp() - from;
    }

    /// <summary>The same poses in each figure's mask colour, so the bench gets outlines like the ice.</summary>
    public void DrawMask(World world, RenderContext render)
    {
        long from = Stopwatch.GetTimestamp();
        for (int i = 0; i < draws.Count; i++)
        {
            var d = draws[i];
            var model = Posed(i);
            for (int k = 0; k < model.MaterialCount; k++) model.Materials[k].Shader = render.Mask;
            DrawModelEx(model, d.At, Vector3.UnitY, d.Heading * 180f / MathF.PI, new Vector3(Players.SkaterRenderer.Scale), d.Mask);
        }
        DrawMs = Stopwatch.GetElapsedTime(0, drawTicks + Stopwatch.GetTimestamp() - from).TotalMilliseconds;
    }

    public void Dispose()
    {
        if (!loaded) return;
        foreach (var (key, meshes) in baked)
        {
            for (int k = 0; k < models[key.Model].MeshCount; k++) UnloadMesh(((Mesh*)meshes)[k]);
            NativeMemory.Free((void*)meshes);
        }
        foreach (var t in kits.Values) UnloadTexture(t);
        foreach (var m in models) UnloadModel(m);
    }
}
#pragma warning restore S6640 // Raylib mesh baking requires pointer-based manipulation of native model data.
