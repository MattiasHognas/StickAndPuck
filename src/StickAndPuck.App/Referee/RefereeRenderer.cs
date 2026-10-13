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

    int idle = -1, skate = -1, armUp = -1, clip = -1;
    float frame;

    public void Load()
    {
        model = LoadModel(Assets.Referee);
        clips = LoadModelAnimations(Assets.Referee).ToArray();
        idle = Find("idle");
        skate = Find("skate");
        armUp = Find("armup");
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
        int want = r.Speed > 0.6f && skate >= 0 ? skate : idle;
        // The call's signal for a moment after the whistle, the raised arm while a call is delayed
        if (r.Announced.TryGet(out var called) && time - called.Time < SignalSeconds && Find(called.Infraction) is >= 0 and var signal) want = signal;
        else if (r.Call.HasValue && armUp >= 0) want = armUp;
        if (want != clip) { clip = want; frame = 0; }
        frame += dt * ClipFps;
    }

    void Pose()
    {
        if (clip < 0) return;
        UpdateModelAnimation(model, clips[clip], (int)frame % clips[clip].KeyFrameCount);   // whole frames only
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
