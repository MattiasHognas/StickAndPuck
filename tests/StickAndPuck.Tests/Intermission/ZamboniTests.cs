using StickAndPuck.Core;
using StickAndPuck.Intermission;
using StickAndPuck.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The intermission (W31), run with the match rules through two period breaks, and a third match
/// where the drive is skipped: the Zamboni laps the ice between periods and the next period starts
/// after it. Formerly SAP_SCENE=zambonitest.
/// </summary>
public sealed class ZamboniTests : CheckedTest
{
    /// <summary>A match of 3-second periods with no overtime, the starters on and the match flow begun.</summary>
    static World Make() => new WorldBuilder().Setup(s => s with { PeriodSeconds = 3, Overtime = OvertimeRule.None }).Build();

    /// <summary>The route stays on the ice clear of the boards, the creases and the bench doors.</summary>
    [Test]
    public void Route()
    {
        var route = IntermissionSystem.Route;
        bool inside = true, clearOfCreases = true, clearOfBenches = true;
        float nearestBoards = float.MaxValue;
        for (float d = 0; d <= route.Length; d += 0.1f)
        {
            if (!route.OnIce(d)) continue;
            var p = route.At(d);
            // half the Zamboni's 2.16 m width and its 2.62 m tail, whichever way it points
            const float reach = 2.62f;
            bool laps = p.X > IntermissionSystem.Route.At(0).X + 0.5f;   // past the gate lane
            if (laps)
            {
                float toSide = RinkSpec.HalfWidth - MathF.Abs(p.Y);
                float toEnd = RinkSpec.HalfLength - MathF.Abs(p.X);
                nearestBoards = Math.Min(nearestBoards, Math.Min(toSide, toEnd));
                inside &= toSide > 1.08f && toEnd > reach;
            }
            float crease = RinkSpec.GoalLineX - RinkSpec.CreaseRadius;
            clearOfCreases &= MathF.Abs(p.X) + 1.08f < crease + 0.3f;
            bool atDoor = MathF.Abs(MathF.Abs(p.X) - 6f) < 4f && p.Y < -RinkSpec.HalfWidth + 2.5f;
            clearOfBenches &= !atDoor;
        }
        Check(inside, $"the laps stay inside the boards (closest {nearestBoards:F1} m from the centre to the boards)");
        Check(clearOfCreases, "the Zamboni never reaches a crease, so the goalies stay put");
        Check(clearOfBenches, "the Zamboni keeps clear of the skaters waiting at the bench doors");
        Check(route.Duration is > 20f and < 45f, $"one drive takes {route.Duration:F0} s");
        float far = 0;
        for (float d = 0; d <= route.Length; d += 0.5f) far = Math.Max(far, route.At(d).X);
        Check(far > 15f, $"it laps the whole rink, out to x {far:F1} at the away end");
    }

    /// <summary>A whole match: two breaks, the Zamboni out for each, the next period only after it has gone.</summary>
    [Test]
    public void Laps()
    {
        var world = Make();
        var system = new IntermissionSystem(() => false);
        float firstOut = -1, firstGone = -1, secondPeriod = -1;
        int drives = 0;
        IntermissionSystem.Drive? last = null;
        Run(world, system, w => w.Match.Phase == MatchPhase.Final, 200f, w =>
        {
            if (system.Current is { } d && d != last) { drives++; last = d; }
            if (system.Current is { Finished: false } && firstOut < 0) firstOut = w.Time;
            if (system.Current is { Finished: true } && firstGone < 0) firstGone = w.Time;
            if (w.Match.Period == 2 && w.Match.Phase == MatchPhase.Faceoff && secondPeriod < 0) secondPeriod = w.Time;
        });
        Check(world.Match.Phase == MatchPhase.Final, "the match plays through to the final");
        Check(drives == 2, $"the Zamboni drives in both intermissions ({drives})");
        Check(firstGone - firstOut >= IntermissionSystem.Route.Duration - 0.1f, $"the first intermission lasts the whole drive ({firstGone - firstOut:F1} s)");
        Check(secondPeriod > firstGone && secondPeriod - firstGone < 3f, $"period 2 starts {secondPeriod - firstGone:F1} s after the Zamboni leaves");
    }

    /// <summary>A skip ends the drive at once and brings the next period on quickly.</summary>
    [Test]
    public void Skip()
    {
        var world = Make();
        bool press = false;
        var system = new IntermissionSystem(() => press);
        Run(world, system, w => system.Current != null, 20f);
        float start = world.Time;
        Run(world, system, w => w.Time - start > 3f, 5f);
        press = true;
        Run(world, system, w => system.Current?.Finished == true, 1f);
        press = false;
        Check(system.Current is { Skipped: true }, "enter skips the Zamboni");
        float skipped = world.Time;
        Run(world, system, w => w.Match.Period == 2 && w.Match.Phase == MatchPhase.Faceoff, 10f);
        Check(world.Match.Period == 2 && world.Time - skipped < 3f, $"after a skip period 2 starts {world.Time - skipped:F1} s later");
    }

    static void Run(World world, IntermissionSystem system, Func<World, bool> until, float limit, Action<World>? each = null)
    {
        var systems = new ISystem[] { new MatchRules(), system };
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            each?.Invoke(world);
            world.Time += Game.Step; world.Tick++;
        }
    }
}
