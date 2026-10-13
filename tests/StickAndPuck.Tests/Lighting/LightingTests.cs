using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Lighting;
using StickAndPuck.Physics;
using StickAndPuck.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The match flow with the light cues (W18): the presets differ, the intro is dim, the lights come
/// back at the faceoff and a goal flashes. W21: the ice is a cold grey in every preset and the
/// scratches are faint and a shade off it. Formerly SAP_SCENE=lightingtest.
/// </summary>
public sealed class LightingTests : CheckedTest
{
    [Test]
    public void Presets()
    {
        Check(LightPresets.Early.Full.Y > LightPresets.Late.Full.Y + 0.15f
            && LightPresets.Early.Shadow.Y > LightPresets.Late.Shadow.Y + 0.3f, "early is brighter than late");
        Check(!LightPresets.Early.IceBand.Equals(LightPresets.Late.IceBand), "early and late bands differ");
        CheckIce(LightPresets.Early); CheckIce(LightPresets.Afternoon); CheckIce(LightPresets.Late);
    }

    /// <summary>The cues through one intro, the lights coming up and a goal flash, in order on one world.</summary>
    [Test]
    public void Cues()
    {
        var world = new WorldBuilder().Setup(s => s with { PeriodSeconds = 60 }).Build();
        var cues = new LightCues();
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules() };
        void Step(Action<World>? before = null)
        {
            world.Events.Clear();
            before?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            cues.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }

        Step();
        Check(cues.Intro && cues.Current().Tone == LightCues.IntroDim && !cues.Current().Bands, "intro plays with the lights down");

        while (world.Match.Phase == MatchPhase.Intro) Step();
        var first = cues.Current().Tone;
        Check(first.X > LightCues.IntroDim.X && first.X < 1, "lights come up in steps after the intro");
        for (int i = 0; i < LightCues.LightsUpSeconds / Game.Step + 1; i++) Step();
        Check(cues.Current() == new CueLight(Vector3.One, true, false), "full light once they are up");

        Step(w => w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, Side.Home)));
        Check(cues.Flashing && cues.Current().Flash, "a goal starts a flash");
        bool dark = false;
        for (int i = 0; i < LightCues.FlashSeconds / Game.Step - 2; i++) { Step(); dark |= !cues.Current().Flash; }
        Check(dark && cues.Flashing, "the flash strobes with dark gaps");
        for (int i = 0; i < 4; i++) Step();
        Check(!cues.Flashing && cues.Current().Tone == Vector3.One, "the flash ends and the lights settle");
    }

    /// <summary>W21: the preset's ice is a cold, light grey and its scratches are faint and a shade off it.</summary>
    void CheckIce(LightPreset preset)
    {
        var ice = preset.Ice; var scratch = preset.Scratch;
        Check(ice.B >= ice.R && ice.B >= ice.G && ice.R >= 0xb0, $"{preset.Time} ice is a cold, light grey");
        Check(scratch.A is >= 96 and <= 192 && Math.Abs(scratch.R - ice.R) + Math.Abs(scratch.G - ice.G) + Math.Abs(scratch.B - ice.B) <= 60,
            $"{preset.Time} scratches are faint and a shade off the ice");
    }
}
