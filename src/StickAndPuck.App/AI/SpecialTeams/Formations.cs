using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>Where each skater of a power play and a penalty kill stands, as pure maths with no state
    /// and no Raylib, so tests can ask for the spots. Spots are on the ice as (x, z). Inside the maths,
    /// "a" runs toward the net the power play attacks (a = end * x), so one set of numbers serves both
    /// ends; both clubs' spots are worked out from the power play's end.</summary>
    public static class Formations
    {
        private const float G = RinkSpec.GoalLineX;
        private const float Blue = RinkSpec.BlueLineX;

        /// <summary>Gets whether a formation is a power play shape.</summary>
        public static bool IsPowerPlay(Formation f)
        {
            return f is Formation.Umbrella or Formation.OneThreeOne;
        }

        /// <summary>Gets the kill shape that answers a power play shape: the diamond takes the 1-3-1's bumper away, the box the umbrella's net front.</summary>
        public static Formation Answer(Formation pp)
        {
            return pp == Formation.OneThreeOne ? Formation.Diamond : Formation.Box;
        }

        /// <summary>Gets the spots of a formation for a number of skaters, most important first.</summary>
        public static IReadOnlyList<Vector2> Spots(Formation f, int skaters, int end, Vector2 puck)
        {
            float pa = end * puck.X;
            float pz = puck.Y;
            (float A, float Z)[] shape = GetShape(f, skaters, pz);

            // the power play leans a little to the puck; the kill slides and steps up toward it, keeping its shape
            bool kill = !IsPowerPlay(f);
            float slide = kill ? 0.3f : 0.12f;
            float step = kill ? Math.Clamp((pa - (G - 9f)) * -0.12f, -1.2f, 1.2f) : 0f;
            List<Vector2> spots = [];
            for (int i = 0; i < Math.Min(skaters, shape.Length); i++)
            {
                (float a, float z) = shape[i];
                z = AdjustZ(kill, z, pz, slide);
                spots.Add(TeamShape.Inside(new Vector2(end * (a - step), z), 1.2f));
            }
            return spots;
        }

        private static (float A, float Z)[] GetShape(Formation f, int skaters, float puckZ)
        {
            float strong = puckZ >= 0 ? 1f : -1f;
            return f switch
            {
                Formation.Umbrella =>
                [
                    (Blue + 2.2f, 0),
                    (G - 11.5f, -8.5f),
                    (G - 11.5f, 8.5f),
                    (G - 2.4f, 0.6f * strong),
                    (G - 1.2f, 5f * strong),
                ],
                Formation.OneThreeOne =>
                [
                    (Blue + 2.2f, 0),
                    (G - 9f, -9f),
                    (G - 9f, 9f),
                    (G - 2.2f, 0),
                    (G - 8f, 0),
                ],
                Formation.Box when skaters <= 3 =>
                [
                    (G - 4.2f, -2.8f),
                    (G - 4.2f, 2.8f),
                    (G - 10f, 0),
                ],
                Formation.Box =>
                [
                    (G - 4.3f, -2.9f),
                    (G - 4.3f, 2.9f),
                    (G - 10.7f, -5.2f),
                    (G - 10.7f, 5.2f),
                ],
                Formation.Diamond when skaters <= 3 =>
                [
                    (G - 4.2f, -2.8f),
                    (G - 4.2f, 2.8f),
                    (G - 10f, 0),
                ],
                Formation.Diamond =>
                [
                    (G - 3.1f, 0),
                    (G - 7f, -5.6f),
                    (G - 7f, 5.6f),
                    (G - 11.5f, 0),
                ],
                Formation.None => [],
                _ => throw new NotSupportedException("no shape for formation " + f),
            };
        }

        private static float AdjustZ(bool kill, float z, float puckZ, float slide)
        {
            if (kill)
            {
                return Math.Clamp(
                    z + (puckZ * slide),
                    -RinkSpec.HalfWidth + 2f,
                    RinkSpec.HalfWidth - 2f
                );
            }

            float centerBias = MathF.Abs(z) < 1f ? 1f : 0.5f;
            return z + (puckZ * slide * centerBias);
        }

        /// <summary>Gets whether a spot (x, z) is in the zone the power play attacks.</summary>
        public static bool InZone(int end, Vector2 at, float margin = 0f)
        {
            return end * at.X > Blue + margin;
        }

        /// <summary>Hands the spots to skaters: the cheapest pairing over every order (five skaters is 120
        /// orders), each skater's cost his distance to the spot, with a defenceman cheaper at the point.</summary>
        public static (int[] Spot, float Cost) Assign(
            IReadOnlyList<(Vector2 At, bool Defence)> skaters,
            IReadOnlyList<Vector2> spots
        )
        {
            int n = Math.Min(skaters.Count, spots.Count);
            int[] best = new int[skaters.Count];
            Array.Fill(best, -1);
            float bestCost = float.MaxValue;
            int[] order = new int[n];
            bool[] used = new bool[spots.Count];
            void Walk(int i, float cost)
            {
                if (cost >= bestCost)
                {
                    return;
                }

                if (i == n)
                {
                    bestCost = cost;
                    Array.Fill(best, -1);
                    for (int k = 0; k < n; k++)
                    {
                        best[k] = order[k];
                    }

                    return;
                }
                for (int s = 0; s < spots.Count; s++)
                {
                    if (used[s])
                    {
                        continue;
                    }

                    used[s] = true;
                    order[i] = s;
                    Walk(i + 1, cost + Cost(skaters[i], spots[s], s));
                    used[s] = false;
                }
            }
            Walk(0, 0f);
            return (best, bestCost);
        }

        /// <summary>Gets what one pairing costs, as <see cref="Assign"/> counts it.</summary>
        public static float Cost((Vector2 At, bool Defence) skater, Vector2 spot, int index)
        {
            return Vector2.Distance(skater.At, spot) - (skater.Defence && index == 0 ? 4f : 0f);
        }
    }
}
