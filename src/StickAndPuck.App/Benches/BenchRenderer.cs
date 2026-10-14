using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Benches;

/// <summary>The clips the bench figures play, all in one place (bench reactions add theirs here).</summary>
internal static class BenchClips
{
    /// <summary>The skater clip a seated figure plays.</summary>
    public const string Sit = "sit";

    /// <summary>The skater clip a figure plays for a while after sitting down from a shift.</summary>
    public const string SitTired = "sit_tired";

    /// <summary>The skater clip a figure waiting at the door plays.</summary>
    public const string Stand = "stand_boards";

    /// <summary>The skater clip a figure walking along the bench plays.</summary>
    public const string Walk = "idle";

    /// <summary>The goalie clip a goalie on the bench plays.</summary>
    public const string Goalie = "ready";

    /// <summary>The seat top over the deck (arena.glb: a 0.2 m riser and the seat), less the clips' 0.45 m from soles to seat (m).</summary>
    public const float SeatLift = 0.62f - 0.45f;

    /// <summary>How far a seated goalie sinks into his seat, his ready clip crouching rather than sitting (m).</summary>
    public const float GoalieLift = 0.1f;
}

/// <summary>Draws both clubs' benches: everyone dressed and off the ice in his club's kit and number,
/// in the seats <see cref="BenchLayout"/> gives, moved by <see cref="BenchCrowd"/>. Render only:
/// the match runs the same with <see cref="Settings.Benches"/> on or off. Every clip frame is skinned
/// once at load into static meshes, and a bench out of the camera's view is not drawn.</summary>
internal sealed unsafe class BenchRenderer : ISystem, IDisposable
{
    const float ClipFps = 24f;

    readonly Dictionary<string, Texture2D> kits = new(StringComparer.Ordinal);
    readonly List<(BenchFigure Figure, int Model, int Clip, int Frame)> draws = [];
    readonly Model[] models = new Model[2];
    readonly ModelAnimation[][] clips = [[], []];
    readonly int[] clip = new int[5];
    readonly Dictionary<(int Model, int Clip, int Frame), nint> baked = [];
    Maybe<BenchCrowd> crowd;
    bool loaded;

    /// <summary>Gets the figures on the benches this step (none with the setting off).</summary>
    internal IReadOnlyList<BenchFigure> Figures => crowd.TryGet(out var c) ? c.Shown : [];

    public void Start(World world)
    {
        models[0] = LoadModel(Assets.Skater);
        clips[0] = LoadModelAnimations(Assets.Skater).ToArray();
        models[1] = LoadModel(Assets.Goalie);
        clips[1] = LoadModelAnimations(Assets.Goalie).ToArray();
        clip[0] = Find(0, "skater", BenchClips.Sit);
        clip[1] = Find(0, "skater", BenchClips.Stand);
        clip[2] = Find(0, "skater", BenchClips.Walk);
        clip[3] = Find(1, "goalie", BenchClips.Goalie);
        clip[4] = Find(0, "skater", BenchClips.SitTired);
        foreach (int c in (ReadOnlySpan<int>)[clip[0], clip[1], clip[2], clip[4]]) Unarm(0, c, "stick");
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
        if (c < 0 || b < 0) return;
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
        c.Update(world, dt);
    }

    /// <summary>Picks each visible figure's model, clip and whole frame, sorted so figures in the same pose draw together.</summary>
    void Plan(World world)
    {
        draws.Clear();
        if (!loaded || !world.Settings.Benches) return;
        var camera = world.Camera;
        bool home = InView(camera, Side.Home), away = InView(camera, Side.Away);
        foreach (var f in Figures)
        {
            if (!(f.Player.Side == Side.Home ? home : away)) continue;
            int model = f.Player.IsGoalie ? 1 : 0;
            int c = model == 1 ? clip[3] : f.Walking ? clip[2] : f.Pose != BenchPose.Watch ? clip[1] : world.Time < f.TiredUntil && clip[4] >= 0 ? clip[4] : clip[0];
            if (c < 0) continue;
            int count = clips[model][c].KeyFrameCount;
            int frame = (int)((world.Time + f.Phase) * ClipFps) % count;
            draws.Add((f, model, c, frame));
        }
        draws.Sort(static (a, b) => a.Model != b.Model ? a.Model - b.Model : a.Clip != b.Clip ? a.Clip - b.Clip : a.Frame - b.Frame);
    }

    /// <summary>Gets whether any of a bench is inside the camera's view across the rink.</summary>
    static bool InView(Camera3D camera, Side side)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        float half = MathF.Atan(MathF.Tan(camera.FovY * MathF.PI / 360f) * Game.Width / Game.Height) + 0.1f;
        var (from, to) = BenchLayout.Span(side);
        for (float x = from; x <= to + 0.01f; x += (to - from) / 4f)
        {
            var d = new Vector3(x, 1f, BenchLayout.SeatZ) - camera.Position;
            float across = Vector3.Dot(d, right), ahead = Vector3.Dot(d, forward);
            if (ahead > 0 && MathF.Atan2(MathF.Abs(across), ahead) < half) return true;
        }
        return false;
    }

    static Vector3 Spot(BenchFigure f)
    {
        float lift = f.Standing ? 0f : f.Player.IsGoalie ? BenchClips.GoalieLift : BenchClips.SeatLift;
        return new Vector3(f.X, lift, f.Standing ? BenchLayout.StandZ : BenchLayout.SeatZ);
    }

    Texture2D Kit(World world, Player p)
    {
        var side = world.Settings.SwapKits ? (p.Side == Side.Home ? Side.Away : Side.Home) : p.Side;
        string path = Assets.KitAtlas(world.Setup.Club(p.Side).Id, side, p.IsGoalie ? "goalie" : "skater", p.Data.Number);
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
        for (int j = 0; j < clip.Length; j++)
        {
            int m = j == 3 ? 1 : 0, c = clip[j];
            if (c < 0 || baked.ContainsKey((m, c, 0))) continue;
            var model = models[m];
            for (int frame = 0; frame < clips[m][c].KeyFrameCount; frame++)
            {
                UpdateModelAnimation(model, clips[m][c], frame);
                var meshes = (Mesh*)NativeMemory.Alloc((nuint)model.MeshCount, (nuint)sizeof(Mesh));
                for (int k = 0; k < model.MeshCount; k++) meshes[k] = Bake(model.Meshes[k]);
                baked[(m, c, frame)] = (nint)meshes;
            }
        }
    }

    /// <summary>Gets the model in a draw's baked pose.</summary>
    Model Posed(int i)
    {
        var (_, m, c, frame) = draws[i];
        var model = models[m];
        model.Meshes = (Mesh*)baked[(m, c, frame)];
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
        Plan(world);
        for (int i = 0; i < draws.Count; i++)
        {
            var f = draws[i].Figure;
            var model = Posed(i);
            var kit = Kit(world, f.Player);
            for (int k = 0; k < model.MaterialCount; k++)
            {
                model.Materials[k].Shader = render.Toon;
                SetMaterialTexture(ref model.Materials[k], MaterialMapIndex.Albedo, kit);
            }
            DrawModelEx(model, Spot(f), Vector3.UnitY, f.Heading * 180f / MathF.PI, new Vector3(Players.SkaterRenderer.Scale), Color.White);
        }
    }

    /// <summary>The same poses in each player's mask colour, so the bench gets outlines like the ice.</summary>
    public void DrawMask(World world, RenderContext render)
    {
        for (int i = 0; i < draws.Count; i++)
        {
            var f = draws[i].Figure;
            var model = Posed(i);
            for (int k = 0; k < model.MaterialCount; k++) model.Materials[k].Shader = render.Mask;
            DrawModelEx(model, Spot(f), Vector3.UnitY, f.Heading * 180f / MathF.PI, new Vector3(Players.SkaterRenderer.Scale), MaskIds.For(f.Player.MaskId));
        }
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
