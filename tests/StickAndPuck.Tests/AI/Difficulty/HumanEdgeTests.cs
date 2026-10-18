using System.Globalization;
using StickAndPuck.App.AI.Difficulty;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.AI.Difficulty
{
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
            Dictionary<App.Core.Difficulty, float> knock = [];
            Dictionary<App.Core.Difficulty, float> deke = [];
            Dictionary<App.Core.Difficulty, float> hit = [];
            foreach (
                App.Core.Difficulty level in new[]
                {
                    App.Core.Difficulty.Easy,
                    App.Core.Difficulty.Normal,
                    App.Core.Difficulty.Hard,
                }
            )
            {
                World world = new WorldBuilder()
                    .Human()
                    .Settings(s => s with { Difficulty = level })
                    .Build();
                Player human = world.Players.First(p => p.Slot == 0);
                Player checker = world.OnIce(Side.Away).First(p => !p.IsGoalie);
                float strength =
                    CheckingModel.Strength(checker, human, 6f, carrying: true)
                    * HumanEdge.CheckStrength(world, checker, human);
                knock[level] = CheckingModel.KnockdownChance(
                    strength,
                    HumanEdge.KnockdownCap(world, human)
                );
                deke[level] = DekeModel.Chance(
                    0f,
                    human.Data.Traits.PuckControl,
                    HumanEdge.DekeFloor(world, human)
                );
                hit[level] = HumanEdge.HitChance(world, human);
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      {level}: an AI check at 6 m/s (strength {strength:0.00}) knocks the carrier down {knock[level]:P0}; a mistimed deke gets through {deke[level]:P0}; hits tried at {hit[level]:P0} of the usual rate"
                    )
                );
            }
            Check(
                knock[App.Core.Difficulty.Easy] < knock[App.Core.Difficulty.Normal]
                    && knock[App.Core.Difficulty.Normal] < knock[App.Core.Difficulty.Hard],
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"easy knocks the human's carrier down least, hard most ({knock[App.Core.Difficulty.Easy]:P0}, {knock[App.Core.Difficulty.Normal]:P0}, {knock[App.Core.Difficulty.Hard]:P0})"
                )
            );
            Check(
                knock[App.Core.Difficulty.Easy] <= 0.6f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"on easy a big hit takes the carrier off the puck at most six times in ten ({knock[App.Core.Difficulty.Easy]:P0})"
                )
            );
            VerifyDifficultyResults(deke, hit);
        }

        private void VerifyDifficultyResults(
            Dictionary<App.Core.Difficulty, float> deke,
            Dictionary<App.Core.Difficulty, float> hit
        )
        {
            Check(
                deke[App.Core.Difficulty.Easy] >= 0.5f
                    && deke[App.Core.Difficulty.Easy] > deke[App.Core.Difficulty.Normal]
                    && deke[App.Core.Difficulty.Normal] > deke[App.Core.Difficulty.Hard],
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a mistimed deke gets through most on easy ({deke[App.Core.Difficulty.Easy]:P0}, {deke[App.Core.Difficulty.Normal]:P0}, {deke[App.Core.Difficulty.Hard]:P0})"
                )
            );
            Check(
                hit[App.Core.Difficulty.Easy] < hit[App.Core.Difficulty.Normal]
                    && hit[App.Core.Difficulty.Hard] > 0.999f,
                "the AI tries fewest hits on easy and every hit on hard"
            );

            World allAi = new WorldBuilder()
                .Setup(s =>
                    s with
                    {
                        Inputs =
                        [
                            new InputSlot(InputDevice.None, Side.Home),
                            new InputSlot(InputDevice.None, Side.Away),
                        ],
                    }
                )
                .Settings(s => s with { Difficulty = App.Core.Difficulty.Easy })
                .Build();
            Player skater = allAi.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player other = allAi.OnIce(Side.Away).First(p => !p.IsGoalie);
            Check(
                HumanEdge.CheckStrength(allAi, other, skater) > 0.999f
                    && HumanEdge.KnockdownCap(allAi, skater) > HumanEdgeTuning.KnockdownCap - 0.001f
                    && HumanEdge.HitChance(allAi, skater) > 0.999f
                    && HumanEdge.DekeFloor(allAi, skater) < 0.001f,
                "an all-AI match is never eased"
            );
        }
    }
}
