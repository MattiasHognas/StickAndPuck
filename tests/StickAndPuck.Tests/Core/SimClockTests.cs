using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>Host callbacks schedule fixed simulation steps independently of display refresh.</summary>
public sealed class SimClockTests : CheckedTest
{
    [Test]
    public void DisplayRefreshDoesNotChangeSimulationRate()
    {
        foreach (int refresh in new[] { 30, 60, 120, 144 })
        {
            var clock = new SimClock();
            int steps = 0;
            for (int frame = 0; frame < refresh * 10; frame++)
                steps += clock.Steps(1f / refresh, lockstep: false, fast: false);
            Check(Math.Abs(steps - 600) <= 1, string.Create(CultureInfo.InvariantCulture, $"{refresh} Hz callbacks simulate ten seconds at 60 Hz ({steps} steps)"));
        }
    }

    [Test]
    public void BackgroundTabStallHasBoundedCatchUp()
    {
        var clock = new SimClock();
        Check(clock.Steps(0, lockstep: false, fast: false) == 0, "first callback draws without advancing time");
        int caughtUp = clock.Steps(60, lockstep: false, fast: false);
        Check(caughtUp is >= 14 and <= 15, "a suspended tab catches up at most a quarter second");
        Check(clock.Steps(0, lockstep: false, fast: false) == 0, "the suspended minute is discarded");
        Check(clock.Steps(0, lockstep: true, fast: false) == 1, "desktop captures retain one step per frame");
        Check(clock.Steps(0, lockstep: false, fast: true) == SimClock.FastSteps, "replay fast-forward retains its step batch");
    }
}
