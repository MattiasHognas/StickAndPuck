using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Lighting;

/// <summary>The match start time (early, afternoon, late) picks a lighting preset; intros dim and
/// goals flash. Each match draws a random start time; SAP_LIGHTING=early|afternoon|late fixes
/// it. The preset and cues set the toon shader's three tones and light direction, and the
/// ice, scratch, ice band and glass tones in <see cref="ArenaLook"/>, so the arena and every
/// player follow. The ice sheet and its scratches are flat palette greys per preset, so
/// the ice stays cold under the warm evening key light; only the cues dim or brighten them.</summary>
internal sealed class LightingSystem : ISystem
{
    /// <summary>The gold the bands and glass turn in a goal flash (#f2c14e).</summary>
    static readonly Color FlashBand = new(0xf2, 0xc1, 0x4e, 0xff);
    static readonly Color FlashGlass = new(0xc8, 0x96, 0x3c, 150);

    readonly LightCues cues = new();
    int shadowLoc = -1, midLoc, fullLoc, dirLoc;

    LightPreset preset = LightPresets.Afternoon;

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        // A seeded match (and so its replay) always gets the same mood; the unseeded menu backdrop varies
        var dice = world.Seed != 0 ? Seeds.Dice(world.Seed, "lighting", 0) : Random.Shared;
        var time = Env.Get("SAP_LIGHTING") is { Length: > 0 } name
            ? Enum.TryParse<TimeOfDay>(name, ignoreCase: true, out var t) ? t
                : throw new InvalidOperationException($"SAP_LIGHTING={name}: use early, afternoon or late")
            : Enum.TryParse<TimeOfDay>(world.Settings.Lighting.ToString(), out var chosen) ? chosen    // the settings screen's mood
            : (TimeOfDay)dice.Next(3);
        preset = LightPresets.For(time);
        string startTime = preset.StartTimes[dice.Next(preset.StartTimes.Length)];
        Console.WriteLine($"SAP: lighting {time.ToString().ToLowerInvariant()}, face-off {startTime}");
        Apply(world);
    }

    public void Update(World world, float dt)
    {
        if (!world.Match.Paused) cues.Update(world, dt);
        Apply(world);
    }

    /// <summary>Sets the arena tones from the preset and cues.</summary>
    void Apply(World world)
    {
        var cue = cues.Current();
        var look = world.Look;
        look.IceBand = (cue.Flash, cue.Bands) switch
        {
            (true, _) => FlashBand,
            (_, true) => preset.IceBand,
            _ => preset.IceBand with { A = 0 },
        };
        look.Glass = cue.Flash ? FlashGlass : preset.Glass;
        look.Ice = Dimmed(preset.Ice, cue.Tone);
        look.Scratch = Dimmed(preset.Scratch, cue.Tone);
    }

    /// <summary>A flat tone times the cue's light level, so the ice goes down with the intro and up with a flash; alpha is kept.</summary>
    static Color Dimmed(Color c, System.Numerics.Vector3 tone)
    {
        static byte Mul(byte v, float t) => (byte)Math.Clamp(MathF.Round(v * t), 0, 255);
        return new Color(Mul(c.R, tone.X), Mul(c.G, tone.Y), Mul(c.B, tone.Z), c.A);
    }

    /// <summary>Sets the toon shader's tones and dither start; runs before the arena and skaters draw.</summary>
    public void DrawScene(World world, RenderContext render)
    {
        var shader = render.Toon;
        if (shadowLoc < 0)
        {
            shadowLoc = GetShaderLocation(shader, "shadowTone");
            midLoc = GetShaderLocation(shader, "midTone");
            fullLoc = GetShaderLocation(shader, "fullTone");
            dirLoc = GetShaderLocation(shader, "lightDir");
        }
        var tone = cues.Current().Tone;
        SetShaderValue(shader, shadowLoc, preset.Shadow * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, midLoc, preset.Mid * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, fullLoc, preset.Full * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, dirLoc, preset.LightDir, ShaderUniformDataType.Vec3);
        world.App.Dither.Apply(world.Camera, shader, world.App.Scale);     // the dither pattern hangs from the ice, not the screen
    }
}
