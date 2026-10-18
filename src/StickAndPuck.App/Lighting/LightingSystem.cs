using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Lighting
{
    /// <summary>The match start time (early, afternoon, late) picks a lighting preset; intros dim and
    /// goals flash. Each match draws a random start time; SAP_LIGHTING=early|afternoon|late fixes
    /// it. The preset and cues set the toon shader's three tones and light direction, and the
    /// ice, scratch, ice band and glass tones in <see cref="ArenaLook"/>, so the arena and every
    /// player follow. The ice sheet and its scratches are flat palette greys per preset, so
    /// the ice stays cold under the warm evening key light; only the cues dim or brighten them.</summary>
    internal sealed class LightingSystem : ISystem
    {
        /// <summary>The gold the bands and glass turn in a goal flash (#f2c14e).</summary>
        private static readonly Color FlashBand = new(0xf2, 0xc1, 0x4e, 0xff);
        private static readonly Color FlashGlass = new(0xc8, 0x96, 0x3c, 150);

        private readonly LightCues cues = new();
        private int shadowLoc = -1,
            midLoc,
            fullLoc,
            dirLoc;

        private LightPreset preset = LightPresets.Afternoon;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            // A seeded match (and so its replay) always gets the same mood; the unseeded menu backdrop varies
            Random dice = world.Seed != 0 ? Seeds.Dice(world.Seed, "lighting", 0) : Random.Shared;

            TimeOfDay time;
            string lightingName = Env.Get("SAP_LIGHTING");
            if (lightingName is { Length: > 0 })
            {
                if (!Enum.TryParse(lightingName, ignoreCase: true, out time))
                {
                    throw new InvalidOperationException(
                        $"SAP_LIGHTING={lightingName}: use early, afternoon or late"
                    );
                }
            }
            else if (Enum.TryParse(world.Settings.Lighting.ToString(), out TimeOfDay chosen))
            {
                time = chosen; // the settings screen's mood
            }
            else
            {
                time = (TimeOfDay)dice.Next(3);
            }

            preset = LightPresets.For(time);
            string startTime = preset.StartTimes[dice.Next(preset.StartTimes.Length)];
            Console.WriteLine(
                $"SAP: lighting {time.ToString().ToLowerInvariant()}, face-off {startTime}"
            );
            Apply(world);
        }

        public void Update(World world, float dt)
        {
            if (!world.Match.Paused)
            {
                cues.Update(world, dt);
            }

            Apply(world);
        }

        /// <summary>Sets the arena tones from the preset and cues.</summary>
        private void Apply(World world)
        {
            CueLight cue = cues.Current();
            ArenaLook look = world.Look;
            look.IceBand = (cue.Flash, cue.Bands) switch
            {
                (true, _) => FlashBand,
                (_, true) => preset.IceBand,
                _ => preset.IceBand with { A = 0 },
            };
            look.Glass = cue.Flash ? FlashGlass : preset.Glass;
            look.Ice = Dimmed(preset.Ice, cue.Tone);
            look.Scratch = Dimmed(preset.Scratch, cue.Tone);
            look.Crowd = Dimmed(Color.White, cue.Tone); // the stands dim with the intro and light with a flash
        }

        /// <summary>A flat tone times the cue's light level, so the ice goes down with the intro and up with a flash; alpha is kept.</summary>
        private static Color Dimmed(Color c, Vector3 tone)
        {
            static byte Mul(byte v, float t)
            {
                return (byte)Math.Clamp(MathF.Round(v * t, MidpointRounding.ToEven), 0, 255);
            }

            return new Color(Mul(c.R, tone.X), Mul(c.G, tone.Y), Mul(c.B, tone.Z), c.A);
        }

        /// <summary>Sets the toon shader's tones and dither start; runs before the arena and skaters draw.</summary>
        public void DrawScene(World world, RenderContext render)
        {
            Shader shader = render.Toon;
            if (shadowLoc < 0)
            {
                shadowLoc = GetShaderLocation(shader, "shadowTone");
                midLoc = GetShaderLocation(shader, "midTone");
                fullLoc = GetShaderLocation(shader, "fullTone");
                dirLoc = GetShaderLocation(shader, "lightDir");
            }
            Vector3 tone = cues.Current().Tone;
            SetShaderValue(shader, shadowLoc, preset.Shadow * tone, ShaderUniformDataType.Vec3);
            SetShaderValue(shader, midLoc, preset.Mid * tone, ShaderUniformDataType.Vec3);
            SetShaderValue(shader, fullLoc, preset.Full * tone, ShaderUniformDataType.Vec3);
            SetShaderValue(shader, dirLoc, preset.LightDir, ShaderUniformDataType.Vec3);
            world.App.Dither.Apply(render.Motion.Camera(world), shader, world.App.Scale); // the dither pattern hangs from the ice, not the screen
        }
    }
}
