using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Render;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Checks with skating, handling and puck physics: a held shot goes high and a tap stays low,
/// the height grows with the hold, shot accuracy sets how far the height strays, a held shot away
/// from the net is lofted, and an airborne puck reads as airborne (it grows and casts a shadow).
/// Formerly SAP_SCENE=shottest.
/// </summary>
public sealed class ShotTests : CheckedTest
{
    int seed = 1;

    /// <summary>An empty world: the shots add their own shooter.</summary>
    static World Make() => new WorldBuilder().NoStarters().NotBegun().Build();

    /// <summary>
    /// The hold and the lofted clear share one run of handling seeds (1 upwards, one per shot), so
    /// they stay in one test and in this order, as the scene ran them.
    /// </summary>
    [Test]
    public void HeightFollowsHoldThenLoftedClear()
    {
        HeightFollowsHold();
        LoftedClear();
    }

    /// <summary>Shoots from the slot with shoot held for some steps; returns the puck's height as it crosses the goal line (or its highest point if it never does) and whether it scored.</summary>
    (float AtLine, float Peak, bool Goal) Shoot(int holdSteps, int accuracy = 70, bool atNet = true)
    {
        var w = Make();
        var p = w.AddPlayer(Side.Home, new PlayerData { Position = PlayingPosition.C, Traits = new Traits { ShotAccuracy = accuracy } }, new Vector3(RinkSpec.GoalLineX - 9f, 0, 0));
        p.Heading = atNet ? MathF.PI / 2 : -MathF.PI / 2;
        w.Puck.Carrier = p.Id;
        w.Puck.Position = PuckHandling.Blade(p);
#pragma warning disable S2245 // a fixed seed per run so the checks repeat
        ISystem[] systems = [new SkatingSystem(), new PuckHandlingSystem(new Random(seed++)), new PuckPhysics()];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not PuckPhysics) s.Start(w);
        float atLine = float.NaN, peak = 0;
        bool goal = false, released = false;
        for (int i = 0; i < 240; i++)
        {
            w.Events.Clear();
            p.Command = new PlayerCommand { Shoot = i < holdSteps };   // standing still, shooting the way they face
            var before = w.Puck.Position;
            foreach (var s in systems) s.Update(w, Game.Step);
            var after = w.Puck.Position;
            if (w.Puck.Carrier < 0) released = true;
            if (released) peak = Math.Max(peak, after.Y);
            if (float.IsNaN(atLine) && released && before.X < RinkSpec.GoalLineX && after.X >= RinkSpec.GoalLineX) atLine = after.Y;
            goal |= w.Events.Current.Any(e => e is GoalScored);
            w.Time += Game.Step; w.Tick++;
        }
        return (float.IsNaN(atLine) ? peak : atLine, peak, goal);
    }

    void HeightFollowsHold()
    {
        // the median height at the goal line: a few wild ones that ring off the bar and fly up do not skew it
        float Median(int steps) => Enumerable.Range(0, 13).Select(_ => Shoot(steps).AtLine).Order().ElementAt(6);
        float tap = Median(2), half = Median(24), full = Median(66);
        Check(tap < 0.25f, $"a tap stays low ({tap:0.00} m at the goal line)");
        Check(full > 0.8f, $"a full wind-up goes high ({full:0.00} m at the goal line)");
        Check(tap < half && half < full, $"height grows with the hold ({tap:0.00} < {half:0.00} < {full:0.00} m)");
        int goals = Enumerable.Range(0, 12).Count(_ => Shoot(66, accuracy: 95).Goal);
        Check(goals >= 9, $"a sharp shooter's full wind-up still goes in under the bar ({goals}/12 into an empty net)");
    }

    [Test]
    public void AccuracySetsSpread()
    {
        static float Spread(int accuracy)
        {
#pragma warning disable S2245 // a fixed seed so the check repeats
            var rng = new Random(7);
#pragma warning restore S2245
            var h = Enumerable.Range(0, 400).Select(_ => ShotHeight.AtNet(0.8f, accuracy, rng)).ToList();
            float mean = h.Average();
            return MathF.Sqrt(h.Select(x => (x - mean) * (x - mean)).Average());
        }
        float sharp = Spread(95), wild = Spread(5);
        Check(wild > sharp * 2.5f, $"shot accuracy sets how far the height strays ({sharp:0.00} m vs {wild:0.00} m)");
        Check(ShotHeight.Lift(0.02f) < 1e-6f && ShotHeight.Lift(PuckHandling.FullCharge) > 0.9999f, "a tap has no lift and a full wind-up has all of it");
    }

    void LoftedClear()
    {
        float tap = Shoot(2, atNet: false).Peak, full = Enumerable.Range(0, 6).Average(_ => Shoot(66, atNet: false).Peak);
        Check(tap < 0.1f, $"a tapped shot away from the net slides on the ice (peak {tap:0.00} m)");
        Check(full > 0.8f, $"a held shot away from the net is lofted (mean peak {full:0.00} m)");
    }

    [Test]
    public void AirborneLook()
    {
        Check(!PuckLook.Airborne(0f) && MathF.Abs(PuckLook.Scale(0f) - 1f) < 1e-6f, "a puck on the ice is drawn plain, with no shadow");
        Check(PuckLook.Airborne(0.3f) && PuckLook.Scale(0.3f) > 1.1f, "a lifted puck is drawn larger and casts a shadow");
        Check(PuckLook.Scale(1.2f) > PuckLook.Scale(0.3f) && PuckLook.Shadow(1.2f) < PuckLook.Shadow(0.3f), "higher means a larger puck over a smaller shadow");
    }
}
