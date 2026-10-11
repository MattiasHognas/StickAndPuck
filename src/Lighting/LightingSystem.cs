using Raylib_cs;
using StickAndPuck.Arena;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Lighting;

/// <summary>
/// W18: the match start time (early, afternoon, late) picks a lighting preset; intros dim and
/// goals flash. Each match draws a random start time; SAP_LIGHTING=early|afternoon|late fixes
/// it. The preset and cues set the toon shader's three tones and light direction, and the
/// ice band and glass tones in <see cref="ArenaLook"/>, so the arena and every player follow.
/// </summary>
internal sealed class LightingSystem : ISystem
{
    /// <summary>The gold the bands and glass turn in a goal flash (#f2c14e).</summary>
    static readonly Color FlashBand = new(0xf2, 0xc1, 0x4e, 0xff);
    static readonly Color FlashGlass = new(0xc8, 0x96, 0x3c, 150);

    readonly LightCues cues = new();
    int shadowLoc = -1, midLoc, fullLoc, dirLoc;

    /// <summary>Gets the preset of the current match, for the HUD and screens.</summary>
    public static LightPreset Preset { get; private set; } = LightPresets.Afternoon;

    /// <summary>Gets the face-off time of the current match, like "19:00".</summary>
    public static string StartTime { get; private set; } = LightPresets.Afternoon.StartTimes[0];

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
#pragma warning disable S2245 // a dice roll for the start time, not security
        var dice = new Random();
#pragma warning restore S2245
        var time = Environment.GetEnvironmentVariable("SAP_LIGHTING") is { Length: > 0 } name
            ? Enum.TryParse<TimeOfDay>(name, ignoreCase: true, out var t) ? t
                : throw new InvalidOperationException($"SAP_LIGHTING={name}: use early, afternoon or late")
            : Enum.TryParse<TimeOfDay>(world.Settings.Lighting.ToString(), out var chosen) ? chosen    // W20: the settings screen's mood
            : (TimeOfDay)dice.Next(3);
        Preset = LightPresets.For(time);
        StartTime = Preset.StartTimes[dice.Next(Preset.StartTimes.Length)];
        Console.WriteLine($"SAP: lighting {time.ToString().ToLowerInvariant()}, face-off {StartTime}");
        Apply();
    }

    public void Update(World world, float dt)
    {
        if (!world.Match.Paused) cues.Update(world, dt);
        Apply();
    }

    /// <summary>Sets the arena tones from the preset and cues.</summary>
    void Apply()
    {
        var cue = cues.Current();
        ArenaLook.IceBand = cue.Flash ? FlashBand : cue.Bands ? Preset.IceBand : Preset.IceBand with { A = 0 };
        ArenaLook.Glass = cue.Flash ? FlashGlass : Preset.Glass;
    }

    /// <summary>Sets the toon shader's tones; runs before the arena and skaters draw.</summary>
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
        SetShaderValue(shader, shadowLoc, Preset.Shadow * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, midLoc, Preset.Mid * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, fullLoc, Preset.Full * tone, ShaderUniformDataType.Vec3);
        SetShaderValue(shader, dirLoc, Preset.LightDir, ShaderUniformDataType.Vec3);
    }
}
