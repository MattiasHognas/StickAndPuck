using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Goalie;

/// <summary>
/// Draws every goalie on the ice with goalie.glb and each club's goalie kit: the ready clip while
/// standing and the butterfly clip while down, held poses at 24 frames per second like the skaters.
/// The mask pass uses <see cref="MaskIds.For"/>, so a goalie gets its own outline in front of the net.
/// </summary>
internal sealed unsafe class GoalieRenderer : IDisposable
{
    /// <summary>The Blender scene draws players at 1.15.</summary>
    public const float Scale = 1.15f;
    const float ClipFps = 24f;

    readonly Dictionary<string, Texture2D> kits = new(StringComparer.Ordinal);
    readonly Dictionary<int, (int Clip, float Frame)> poses = [];
    Model model;
    ModelAnimation[] clips = [];
    bool loaded;
    int ready = -1, butterfly = -1;

    public void Load()
    {
        model = LoadModel(Assets.Goalie);
        clips = LoadModelAnimations(Assets.Goalie).ToArray();
        ready = Find("ready");
        butterfly = Find("butterfly");
        loaded = true;
    }

    int Find(string name)
    {
        return Array.FindIndex(clips, a => string.Equals(Marshal.PtrToStringUTF8((IntPtr)a.Name), $"goalie_{name}", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Picks and advances the clip: butterfly from its first frame when the goalie drops, then held on its last pose.</summary>
    public void Animate(Player g, GoalieState state, float dt)
    {
        if (!loaded) return;
        var (clip, frame) = poses.GetValueOrDefault(g.Id, (-1, 0f));
        int want = state.Down && butterfly >= 0 ? butterfly : ready;
        if (want != clip) { clip = want; frame = 0; }
        frame += dt * ClipFps;
        if (clip >= 0 && clip == butterfly) frame = MathF.Min(frame, clips[clip].KeyFrameCount - 1);
        poses[g.Id] = (clip, frame);
    }

    static IEnumerable<Player> Goalies(World world)
    {
        return world.Players.Where(p => p.OnIce && p.IsGoalie);
    }

    void Pose(Player g)
    {
        var (clip, frame) = poses.GetValueOrDefault(g.Id, (-1, 0f));
        if (clip < 0) return;
        int count = clips[clip].KeyFrameCount;
        UpdateModelAnimation(model, clips[clip], (int)frame % count);   // whole frames only: the poses are held, never blended
    }

    Texture2D Kit(World world, Player g)
    {
        string path = Assets.KitAtlas(world.Setup.Club(g.Side).Id, g.Side, "goalie");
        if (!kits.TryGetValue(path, out var tex))
        {
            tex = LoadTexture(path);
            SetTextureFilter(tex, TextureFilter.Point);
            kits[path] = tex;
        }
        return tex;
    }

    static float Degrees(Player g)
    {
        return g.Heading * 180f / MathF.PI;
    }

    public void DrawScene(World world, RenderContext render)
    {
        if (!loaded) return;
        foreach (var g in Goalies(world))
        {
            Pose(g);
            var kit = Kit(world, g);
            for (int i = 0; i < model.MaterialCount; i++)
            {
                model.Materials[i].Shader = render.Toon;
                SetMaterialTexture(ref model.Materials[i], MaterialMapIndex.Albedo, kit);
            }
            DrawModelEx(model, g.Position, Vector3.UnitY, Degrees(g), new Vector3(Scale), Color.White);
        }
    }

    public void DrawMask(World world, RenderContext render)
    {
        if (!loaded) return;
        foreach (var g in Goalies(world))
        {
            Pose(g);
            for (int i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = render.Mask;
            DrawModelEx(model, g.Position, Vector3.UnitY, Degrees(g), new Vector3(Scale), MaskIds.For(g.MaskId));
        }
    }

    public void Dispose()
    {
        if (!loaded) return;
        foreach (var t in kits.Values) UnloadTexture(t);
        UnloadModel(model);
    }
}
