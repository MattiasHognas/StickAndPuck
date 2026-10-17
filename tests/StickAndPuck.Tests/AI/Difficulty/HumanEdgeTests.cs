using System.Globalization;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;
using Level = StickAndPuck.App.Core.Difficulty;

namespace StickAndPuck.Tests;

/// <summary>
/// How the difficulty eases the AI's play on the human's carrier: an AI body check at 6 m/s closing
/// knocks the carrier down less often on easy, an AI presser tries fewer hits, and a mistimed deke gets
/// through more often, while hard plays in full and versus and all-AI matches are never eased.
/// </summary>
public sealed class HumanEdgeTests : CheckedTest
{
    [Test]
    public void PerDifficulty()
    {
        var knock = new Dictionary<Level, float>();
        var deke = new Dictionary<Level, float>();
        var hit = new Dictionary<Level, float>();
        foreach (var level in new[] { Level.Easy, Level.Normal, Level.Hard })
        {
            var world = new WorldBuilder().Human().Settings(s => s with { Difficulty = level }).Build();
            var human = world.Players.First(p => p.Slot == 0);
            var checker = world.OnIce(Side.Away).First(p => !p.IsGoalie);
            float strength = CheckingModel.Strength(checker, human, 6f, carrying: true) * HumanEdge.CheckStrength(world, checker, human);
            knock[level] = CheckingModel.KnockdownChance(strength, HumanEdge.KnockdownCap(world, human));
            deke[level] = DekeModel.Chance(0f, human.Data.Traits.PuckControl, HumanEdge.DekeFloor(world, human));
            hit[level] = HumanEdge.HitChance(world, human);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"      {level}: an AI check at 6 m/s (strength {strength:0.00}) knocks the carrier down {knock[level]:P0}; a mistimed deke gets through {deke[level]:P0}; hits tried at {hit[level]:P0} of the usual rate"));
        }
        Check(knock[Level.Easy] < knock[Level.Normal] && knock[Level.Normal] < knock[Level.Hard], $"easy knocks the human's carrier down least, hard most ({knock[Level.Easy]:P0}, {knock[Level.Normal]:P0}, {knock[Level.Hard]:P0})");
        Check(knock[Level.Easy] <= 0.6f, $"on easy a big hit takes the carrier off the puck at most six times in ten ({knock[Level.Easy]:P0})");
        Check(deke[Level.Easy] >= 0.5f && deke[Level.Easy] > deke[Level.Normal] && deke[Level.Normal] > deke[Level.Hard], $"a mistimed deke gets through most on easy ({deke[Level.Easy]:P0}, {deke[Level.Normal]:P0}, {deke[Level.Hard]:P0})");
        Check(hit[Level.Easy] < hit[Level.Normal] && hit[Level.Hard] > 0.999f, "the AI tries fewest hits on easy and every hit on hard");

        var allAi = new WorldBuilder().Setup(s => s with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] })
            .Settings(s => s with { Difficulty = Level.Easy }).Build();
        var skater = allAi.OnIce(Side.Home).First(p => !p.IsGoalie);
        var other = allAi.OnIce(Side.Away).First(p => !p.IsGoalie);
        Check(HumanEdge.CheckStrength(allAi, other, skater) > 0.999f && HumanEdge.KnockdownCap(allAi, skater) > HumanEdgeTuning.KnockdownCap - 0.001f
            && HumanEdge.HitChance(allAi, skater) > 0.999f && HumanEdge.DekeFloor(allAi, skater) < 0.001f, "an all-AI match is never eased");
    }
}
