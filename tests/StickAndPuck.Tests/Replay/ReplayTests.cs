using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Replays;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Deterministic sim and replay (W67): the maths gives the same bits it gave when the test was
/// written, so this machine computes like the others; the seed streams; the replay file's round
/// trip; and that two AI matches with the same seed play step for step the same, to a checksum
/// written down from the cloud, while another seed plays a different match. SAP_REPLAYTEST_PRINT=1
/// prints the values to write down after a deliberate change to the game. Formerly
/// SAP_SCENE=replaytest.
/// </summary>
public sealed class ReplayTests : CheckedTest
{
    // written down from the cloud run; his machine and the Windows build must give the same
    const string SinCosBits = "3f576aa4 3f0a5140 3f7e9262 4081c433 3f9e830c 3ed227eb";
    const string SeedValues = "610514733 1889226249 14";
    const string MatchSum = "83d7773a46ad20ec";

    readonly bool print = string.Equals(Environment.GetEnvironmentVariable("SAP_REPLAYTEST_PRINT"), "1", StringComparison.Ordinal);

    [Test]
    public void Maths()
    {
        double worst = 0;
        for (int i = -4000; i <= 4000; i++)
        {
            double x = i * 0.01;
            worst = Math.Max(worst, Math.Abs(SimMath.Sin(x) - Math.Sin(x)));
            worst = Math.Max(worst, Math.Abs(SimMath.Cos(x) - Math.Cos(x)));
            worst = Math.Max(worst, Math.Abs(SimMath.Atan2(x, 3.7 - (i * 0.002)) - Math.Atan2(x, 3.7 - (i * 0.002))));
            if (x is > -30 and < 30) worst = Math.Max(worst, Math.Abs(SimMath.Exp(x) - Math.Exp(x)) / Math.Exp(x));
            if (x > 0) worst = Math.Max(worst, Math.Abs(SimMath.Log(x) - Math.Log(x)));
            if (x > 0) worst = Math.Max(worst, Math.Abs(SimMath.Pow(x, 1.5) - Math.Pow(x, 1.5)) / Math.Pow(x, 1.5));
        }
        Check(worst < 1e-12, $"the game's sine, cosine, arctangent, exp, log and pow agree with the C library to 1e-12 ({worst:E1})");
        bool corners = BitConverter.SingleToInt32Bits(SimMath.Atan2(0f, -1f)) == BitConverter.SingleToInt32Bits(MathF.PI)
            && BitConverter.SingleToInt32Bits(SimMath.Atan2(1f, 0f)) == BitConverter.SingleToInt32Bits(MathF.PI / 2)
            && BitConverter.SingleToInt32Bits(SimMath.Sin(0f)) == 0 && BitConverter.SingleToInt32Bits(SimMath.Cos(0f)) == BitConverter.SingleToInt32Bits(1f);
        Check(corners, "and give the exact values at the corners");

        float[] values = [SimMath.Sin(1f), SimMath.Cos(1f), SimMath.Atan2(2f, 1.3f), SimMath.Exp(1.4f), SimMath.Log(3.45f), SimMath.Pow(0.71f, 2.6f)];
        string bits = string.Join(" ", values.Select(v => BitConverter.SingleToInt32Bits(v).ToString("x8", CultureInfo.InvariantCulture)));
        if (print) Console.WriteLine($"      maths bits: {bits}");
        Check(string.Equals(bits, SinCosBits, StringComparison.Ordinal), $"they give the same bits as on the machine the test was written on ({bits})");
    }

    [Test]
    public void SeedStreams()
    {
        string seeds = string.Join(" ", Seeds.For(1, "decisions", 5), Seeds.For(-77, "referee", 8), Seeds.For(0, "penalties", 14));
        if (print) Console.WriteLine($"      seeds: {seeds}");
        Check(string.Equals(seeds, SeedValues, StringComparison.Ordinal), $"a match seed splits into the same stream seeds everywhere, and seed 0 keeps the fixed ones ({seeds})");
        Check(Seeds.For(1, "decisions", 5) != Seeds.For(1, "defence", 5) && Seeds.For(1, "decisions", 5) != Seeds.For(2, "decisions", 5), "each system and each seed gets its own stream");
    }

    [Test]
    public void FileRoundTrip()
    {
        var frame = new InputFrame([32, 263, 265], [32], 3, [new PadFrame(true, 0b1010, 0b10, [0.25f, -1f, 0f, 0.5f, -0.000244140625f, 1f]), PadFrame.None, PadFrame.None, PadFrame.None], "Pause");
        var back = InputFrame.Read(frame.Write());
        Check(string.Equals(back.Write(), frame.Write(), StringComparison.Ordinal) && back.Pads[0].Axes.SequenceEqual(frame.Pads[0].Axes), $"a device frame reads back as written ({frame.Write()})");

        var setup = WorldBuilder.DefaultSetup.WithRules(RuleSet.Shl);
        var file = new ReplayFile
        {
            Header = new ReplayHeader { Seed = 99, Setup = ReplaySetup.Of(setup), Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["SAP_PERIOD"] = "60" } },
            Start = frame,
            End = "100 1-0 5-3 1 12.5 Play",
        };
        file.Inputs[10] = frame;
        file.Checks[0] = 0xdeadbeefcafef00dUL;
        file.Events.Add(ReplayFile.EventLine(3, new PassMade(0.05f, Side.Home, 2, new Vector3(1, 0, 2), 0)));
        Check(string.Equals(file.Events[0], "3 PassMade 0.05 Home 2 1,0,2 0", StringComparison.Ordinal), $"an event line names its type and fields ({file.Events[0]})");
        string path = Path.Combine(Path.GetTempPath(), $"sap-replaytest-{Environment.ProcessId}.sapreplay");
        file.Write(path);
        var read = ReplayFile.Read(path);
        File.Delete(path);
        var rebuilt = read.Header.Setup!.ToSetup(WorldBuilder.Clubs);
        Check(read.Header.Seed == 99 && string.Equals(read.Header.Env["SAP_PERIOD"], "60", StringComparison.Ordinal) && read.Inputs.Count == 1 && read.Checks[0] == 0xdeadbeefcafef00dUL && read.Events.Count == 1 && read.LastTick == 100,
            "a replay file reads back as written");
        Check(rebuilt.Home == setup.Home && rebuilt.Away == setup.Away && rebuilt.Rules == setup.Rules && rebuilt.Overtime == setup.Overtime && rebuilt.OvertimeFormat == setup.OvertimeFormat && rebuilt.Inputs.SequenceEqual(setup.Inputs),
            "the match setup comes back the same: clubs, rules, overtime and inputs");
    }

    [Test]
    public void SameSeedSameMatch()
    {
        const int Steps = 15000;
        var a = Play(31, Steps);
        var b = Play(31, Steps);
        var c = Play(32, Steps);
        int same = a.Sums.Zip(b.Sums).TakeWhile(p => p.First == p.Second).Count();
        Check(same == Steps, $"two matches with seed 31 agree on the world's checksum at every one of {Steps} steps ({same})");
        Check(a.Sums.Zip(c.Sums).Any(p => p.First != p.Second), "seed 32 plays a different match");
        string last = WorldChecksum.Text(a.Sums[^1]);
        if (print) Console.WriteLine($"      match checksum: {last}, {a.Summary}");
        Check(string.Equals(last, MatchSum, StringComparison.Ordinal), $"seed 31 ends on the checksum written down from the cloud ({last}, {a.Summary})");
    }

    static (List<ulong> Sums, string Summary) Play(int seed, int steps)
    {
        var world = new WorldBuilder()
            .Setup(s => s with { PeriodSeconds = 120, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] })
            .Seed(seed)
            .Build();
        ISystem[] systems =
        [
            new PositioningSystem(Seeds.Dice(seed, "positioning", PositioningSystem.Fallback)), new DefenceSystem(Seeds.Dice(seed, "defence", DefenceSystem.Fallback)),
            new SpecialTeamsSystem(Seeds.Dice(seed, "specialteams", SpecialTeamsSystem.Fallback)), new TacticsSystem(Seeds.Dice(seed, "tactics", TacticsSystem.Fallback)),
            new DecisionSystem(Seeds.Dice(seed, "decisions", DecisionSystem.Fallback)), new SkatingSystem(), new CheckingSystem(Seeds.Dice(seed, "checking", CheckingSystem.Fallback)),
            new PuckHandlingSystem(Seeds.Dice(seed, "puckhandling", PuckHandlingSystem.Fallback)), new GoalieSystem { Dice = Seeds.Dice(seed, "goalie", GoalieSystem.Fallback) },
            new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule(), new OvertimeRules(Seeds.Dice(seed, "overtime", OvertimeRules.Fallback)),
        ];
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        var sums = new List<ulong>(steps);
        for (int i = 0; i < steps; i++)
        {
            Game.StepWorld(world, systems);
            sums.Add(WorldChecksum.Of(world));
        }
        foreach (var s in systems.OfType<IDisposable>()) s.Dispose();
        var m = world.Match;
        return (sums, string.Create(CultureInfo.InvariantCulture, $"score {m.Score[0]}-{m.Score[1]}, shots {m.Shots[0]}-{m.Shots[1]}, period {m.Period}"));
    }
}
