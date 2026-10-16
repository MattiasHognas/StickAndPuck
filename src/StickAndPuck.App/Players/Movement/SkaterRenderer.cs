using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Players;

/// <summary>Draws every skater on the ice with skater.glb, its named clips and each club's kit atlas.
/// Clips are baked with held poses, so playing them at 24 frames per second gives the stepped
/// motion. One model is posed and drawn per player in turn. Tab swaps home and away kits (a dev key).</summary>
internal sealed unsafe class SkaterRenderer : ISystem, IDisposable
{
    /// <summary>The Blender scene draws players at 1.15.</summary>
    public const float Scale = 1.15f;
    const float ClipFps = 24f;

    /// <summary>The shoot clip's back-swing, the last frame before the sweep: the held wind-up when skater_windup is missing.</summary>
    const float ShootBackSwing = 5f;

    readonly Dictionary<string, Texture2D> kits = new(StringComparer.Ordinal);
    readonly Dictionary<int, (int Clip, float Frame)> poses = [];
    Model model;
    ModelAnimation[] clips = [];
    bool swapKits;

    public void Start(World world)
    {
        model = ResourceLoader.Model(Assets.Skater);
        clips = ResourceLoader.Animations(Assets.Skater);
    }

    static string ClipName(ModelAnimation a)
    {
        return Marshal.PtrToStringUTF8((IntPtr)a.Name) ?? "";
    }

    int Find(string name)
    {
        return Array.FindIndex(clips, a => string.Equals(ClipName(a), $"skater_{name}", StringComparison.OrdinalIgnoreCase));
    }

    public void Update(World world, float dt)
    {
        if (KeyInput.Pressed(world.Settings, GameAction.Kit)) world.Settings = world.Settings with { SwapKits = !world.Settings.SwapKits };
        swapKits = world.Settings.SwapKits;    // the setting, which Tab flips in play
        if (Dev.OneTimerDemo.Of(world).TryGet(out var demo)) demo.Run(world);
        foreach (var p in Skaters(world))
        {
            var (clip, frame) = poses.GetValueOrDefault(p.Id, (-1, 0f));
            if (p.Action == PlayerAction.Fight && Fights.Fighting.Pose(world, p).TryGet(out var fight) && Find(fight.Clip) is >= 0 and var fc)
            {
                // Fight clips come from the fight; loops wrap here, one-shots hold their last frame in Pose
                frame = fight.Seconds * ClipFps;
                if (fight.Loop) frame %= clips[fc].KeyFrameCount;
                poses[p.Id] = (fc, frame);
                continue;
            }
            if (Rules.Celebration.Pose(world, p).TryGet(out var pose) && Find(pose.Clip) is >= 0 and var pc)
            {
                // The end-of-game celebration poses the winners and the losers the same way
                frame = pose.Seconds * ClipFps;
                if (pose.Loop) frame %= clips[pc].KeyFrameCount;
                poses[p.Id] = (pc, frame);
                continue;
            }
            if (p.Action == PlayerAction.Fallen && Find("fall") is >= 0 and var down)
            {
                // A knocked-down skater plays the fall clip, which lies flat on the ice, and holds its last frame
                poses[p.Id] = (down, p.ActionTime * ClipFps);
                continue;
            }
            if (p.Action != PlayerAction.Shoot && OneTimerArming.ArmedAt(world, p.Id).TryGet(out float armedAt) && WindUp() is >= 0 and var wind)
            {
                // An armed one-timer draws the stick back and up and holds it until the pass arrives; the strike goes on from the shoot clip's sweep
                float last = wind == Find("windup") ? clips[wind].KeyFrameCount - 2 : ShootBackSwing;   // the loader's very last sample is not the held pose
                poses[p.Id] = (wind, Math.Min((world.Time - armedAt) * ClipFps, last));
                continue;
            }
            if (FaceoffPose.Clip(world, p).TryGet(out string stance) && Find(stance) is >= 0 and var set)
            {
                // Set for the draw until the puck drops: a crouch, the centres lowest over the dot; the held clip loops
                float held = set == clip ? frame + (dt * ClipFps) : 0f;
                poses[p.Id] = (set, held % (clips[set].KeyFrameCount - 1));
                continue;
            }
            int want = p.Action == PlayerAction.Shoot ? Find("shoot") : p.Speed > 0.4f ? Find("skate") : Find("idle");
            if (want != clip) { clip = want; frame = 0; }
            if (p.Action == PlayerAction.Shoot) frame = p.ActionTime * ClipFps;
            else frame += dt * ClipFps * (clip == Find("skate") ? Math.Clamp(p.Speed / SkatingSystem.MaxSpeed, 0.6f, 1.4f) : 1f);
            poses[p.Id] = (clip, frame);
        }
    }

    /// <summary>The wind-up clip, or the shoot clip (held at its back-swing) when the model has none.</summary>
    int WindUp()
    {
        return Find("windup") is >= 0 and var w ? w : Find("shoot");
    }

    static IEnumerable<Player> Skaters(World world)
    {
        return world.Players.Where(p => p.OnIce && !p.IsGoalie);
    }

    void Pose(Player p)
    {
        var (clip, frame) = poses.GetValueOrDefault(p.Id, (-1, 0f));
        if (clip < 0) return;
        int count = clips[clip].KeyFrameCount;
        UpdateModelAnimation(model, clips[clip], Math.Min((int)frame, count - 1) % count);   // whole frames only: the poses are held, never blended
    }

    Texture2D Kit(World world, Player p)
    {
        var side = swapKits ? (p.Side == Side.Home ? Side.Away : Side.Home) : p.Side;
        string path = Assets.KitAtlas(world.Setup.Club(p.Side).Id, side, "skater", p.Data.Number);
        if (!kits.TryGetValue(path, out var tex))
        {
            tex = Camera.FarTexture.Load(path);   // mipmapped so stripes hold still at broadcast range
            kits[path] = tex;
        }
        return tex;
    }

    public void DrawScene(World world, RenderContext render)
    {
        foreach (var p in Skaters(world))
        {
            Pose(p);
            var kit = Kit(world, p);
            for (int i = 0; i < model.MaterialCount; i++)
            {
                model.Materials[i].Shader = render.Toon;
                SetMaterialTexture(ref model.Materials[i], MaterialMapIndex.Albedo, kit);
            }
            DrawModelEx(model, p.Position, Vector3.UnitY, p.Heading * 180f / MathF.PI, new Vector3(Scale), Color.White);
        }
    }

    /// <summary>The same pose in one flat colour per player, for the outline pass.</summary>
    public void DrawMask(World world, RenderContext render)
    {
        foreach (var p in Skaters(world))
        {
            Pose(p);
            for (int i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = render.Mask;
            int id = p.MaskId;
            var c = new Color((byte)(40 + id * 37 % 200), (byte)(90 + id * 59 % 160), (byte)(200 - id * 23 % 150), (byte)255);
            DrawModelEx(model, p.Position, Vector3.UnitY, p.Heading * 180f / MathF.PI, new Vector3(Scale), c);
        }
    }

    public void Dispose()
    {
        foreach (var t in kits.Values) UnloadTexture(t);
        UnloadModel(model);
    }
}
