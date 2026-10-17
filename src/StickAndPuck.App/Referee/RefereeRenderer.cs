using System.Runtime.InteropServices;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Referee;

/// <summary>Draws the referee with referee.glb and its striped kit: the skate clip while moving, idle while
/// standing, held poses at 24 frames per second like the players. The mask pass uses
/// <see cref="MaskIds.For"/> with <see cref="Referee.MaskId"/>, so the referee gets its own outline.</summary>
internal sealed unsafe class RefereeRenderer : IDisposable
{
    /// <summary>The Blender scene draws players at 1.15.</summary>
    public const float Scale = 1.15f;
    const float ClipFps = 24f;

    Model model;
    Texture2D kit;
    ModelAnimation[] clips = [];
    bool loaded;
    /// <summary>How long a called penalty's signal shows.</summary>
    const float SignalSeconds = 2.5f;

    /// <summary>The drop clip's first throw frame: its last two poses, the puck thrown down, play once at the drop.</summary>
    const float ThrowFrame = 8f;

    int idle = -1, skate = -1, armUp = -1, set = -1, drop = -1, clip = -1;
    float frame;

    public void Load()
    {
        model = ResourceLoader.Model(Assets.Referee);
        clips = ResourceLoader.Animations(Assets.Referee);
        idle = Find("idle");
        skate = Find("skate");
        armUp = Find("armup");
        set = Find("set");
        drop = Find("drop");
        kit = Camera.FarTexture.Load(Assets.RefereeKit);   // mipmapped so stripes hold still at broadcast range
        loaded = true;
    }

    int Find(string name)
    {
        return Array.FindIndex(clips, a => string.Equals(Marshal.PtrToStringUTF8((IntPtr)a.Name), $"referee_{name}", StringComparison.OrdinalIgnoreCase));
    }

    public void Animate(Referee r, float dt, float time)
    {
        if (!loaded) return;
        int want = Want(r, time);
        float sinceDrop = (time - r.DroppedAt) * ClipFps;
        bool signalling = want != set && want != skate && want != idle;
        if (drop >= 0 && sinceDrop >= 0 && sinceDrop < clips[drop].KeyFrameCount - 1 - ThrowFrame && !signalling)
        {
            // The throw, once, right after the puck drops
            clip = drop;
            frame = ThrowFrame + sinceDrop;
            return;
        }
        if (want != clip) { clip = want; frame = 0; }
        frame += dt * ClipFps;
    }

    /// <summary>Gets the clip to play: a penalty signal, the delayed-call arm, the set pose at the dot, then skate or idle.</summary>
    int Want(Referee r, float time)
    {
        // The call's signal for a moment after the whistle, the raised arm while a call is delayed
        if (r.Announced.TryGet(out var called) && time - called.Time < SignalSeconds && Find(called.Infraction) is >= 0 and var signal)
        {
            return signal;
        }
        if (r.Call.HasValue && armUp >= 0)
        {
            return armUp;
        }
        if (r.SetTime > 0 && set >= 0)
        {
            return set;   // set at the dot, leaning in over it with the puck held out
        }
        return r.Speed > 0.6f && skate >= 0 ? skate : idle;
    }

    void Pose()
    {
        if (clip < 0) return;
        // The loader's final sample is the rest pose; the set stance must stay crouched across its loop.
        int count = clip == set ? Math.Max(1, clips[clip].KeyFrameCount - 1) : clips[clip].KeyFrameCount;
        ModelPose.Apply(model, clips[clip], (int)frame % count);   // whole frames only
    }

    public void DrawScene(Referee r, RenderContext render)
    {
        if (!loaded) return;
        Pose();
        for (int i = 0; i < model.MaterialCount; i++)
        {
            model.Materials[i].Shader = render.Toon;
            SetMaterialTexture(ref model.Materials[i], MaterialMapIndex.Albedo, kit);
        }
        DrawModelEx(model, r.Position, System.Numerics.Vector3.UnitY, r.Heading * 180f / MathF.PI, new System.Numerics.Vector3(Scale), Color.White);
    }

    public void DrawMask(Referee r, RenderContext render)
    {
        if (!loaded) return;
        Pose();
        for (int i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = render.Mask;
        DrawModelEx(model, r.Position, System.Numerics.Vector3.UnitY, r.Heading * 180f / MathF.PI, new System.Numerics.Vector3(Scale), MaskIds.For(Referee.MaskId));
    }

    public void Dispose()
    {
        if (!loaded) return;
        UnloadTexture(kit);
        UnloadModel(model);
        loaded = false;
    }
}
