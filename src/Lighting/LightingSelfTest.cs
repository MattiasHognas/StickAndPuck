using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Rules;

namespace StickAndPuck.Lighting;

/// <summary>
/// SAP_SCENE=lightingtest: runs the match flow with the light cues, no window, prints one line
/// per check and exits with 0 when all pass, 1 otherwise. Covers the W18 "done when": the
/// presets differ, the intro is dim, the lights come back at the faceoff and a goal flashes.
/// </summary>
public sealed class LightingSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "lightingtest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = 60 }, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world, log: false);
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

        Check(LightPresets.Early.Full.Y > LightPresets.Late.Full.Y + 0.15f
            && LightPresets.Early.Shadow.Y > LightPresets.Late.Shadow.Y + 0.3f, "early is brighter than late");
        Check(!LightPresets.Early.IceBand.Equals(LightPresets.Late.IceBand), "early and late bands differ");

        Step();
        Check(cues.Intro && cues.Current().Tone == LightCues.IntroDim && !cues.Current().Bands, "intro plays with the lights down");

        while (world.Match.Phase == MatchPhase.Intro) Step();
        var first = cues.Current().Tone;
        Check(first.X > LightCues.IntroDim.X && first.X < 1, "lights come up in steps after the intro");
        for (int i = 0; i < LightCues.LightsUpSeconds / Game.Step + 1; i++) Step();
        Check(cues.Current() == new CueLight(System.Numerics.Vector3.One, true, false), "full light once they are up");

        Step(w => w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, Side.Home)));
        Check(cues.Flashing && cues.Current().Flash, "a goal starts a flash");
        bool dark = false;
        for (int i = 0; i < LightCues.FlashSeconds / Game.Step - 2; i++) { Step(); dark |= !cues.Current().Flash; }
        Check(dark && cues.Flashing, "the flash strobes with dark gaps");
        for (int i = 0; i < 4; i++) Step();
        Check(!cues.Flashing && cues.Current().Tone == System.Numerics.Vector3.One, "the flash ends and the lights settle");

        Console.WriteLine(failures == 0 ? "LIGHTINGTEST: all checks passed" : $"LIGHTINGTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return world;
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }
}
