using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.AI;

/// <summary>The shape a club plays on a power play or a penalty kill.</summary>
public enum Formation
{
    /// <summary>Even strength, or the puck out of the zone: positioning's shape.</summary>
    None,

    /// <summary>Power play: a quarterback at the point, two shooters on the half walls, one at the net and one low.</summary>
    Umbrella,

    /// <summary>Power play: one at the point, three across the middle (two half walls and the bumper in the slot), one at the net.</summary>
    OneThreeOne,

    /// <summary>Penalty kill: two low beside the net, two high at the tops of the circles (a triangle with three).</summary>
    Box,

    /// <summary>Penalty kill: one high, one each side, one low in front of the net; it covers the slot and the bumper.</summary>
    Diamond,
}

/// <summary>
/// W22c: where each skater of a power play and a penalty kill stands, as pure maths with no state
/// and no Raylib, so tests can ask for the spots. Spots are on the ice as (x, z). Inside the maths,
/// "a" runs toward the net the power play attacks (a = end * x), so one set of numbers serves both
/// ends; both clubs' spots are worked out from the power play's end.
/// </summary>
public static class Formations
{
    const float G = RinkSpec.GoalLineX;
    const float Blue = RinkSpec.BlueLineX;

    /// <summary>Gets whether a formation is a power play shape.</summary>
    /// <param name="f">The formation.</param>
    /// <returns>True for the umbrella and the 1-3-1.</returns>
    public static bool IsPowerPlay(Formation f)
    {
        return f is Formation.Umbrella or Formation.OneThreeOne;
    }

    /// <summary>Gets the kill shape that answers a power play shape: the diamond takes the 1-3-1's bumper away, the box the umbrella's net front.</summary>
    /// <param name="pp">The power play shape.</param>
    /// <returns>The kill shape.</returns>
    public static Formation Answer(Formation pp)
    {
        return pp == Formation.OneThreeOne ? Formation.Diamond : Formation.Box;
    }

    /// <summary>Gets the spots of a formation for a number of skaters, most important first.</summary>
    /// <param name="f">The formation.</param>
    /// <param name="skaters">Skaters on the ice for that club, 1 to 5; more get no spot.</param>
    /// <param name="end">The end the power play attacks: +1 or -1.</param>
    /// <param name="puck">The puck on the ice (x, z).</param>
    /// <returns>The spots on the ice (x, z), inside the boards.</returns>
    public static IReadOnlyList<Vector2> Spots(Formation f, int skaters, int end, Vector2 puck)
    {
        float pa = end * puck.X, pz = puck.Y;
        float strong = pz >= 0 ? 1f : -1f;
        (float A, float Z)[] shape = f switch
        {
            Formation.Umbrella => [(Blue + 2.2f, 0), (G - 11.5f, -8.5f), (G - 11.5f, 8.5f), (G - 2.4f, 0.6f * strong), (G - 1.2f, 5f * strong)],
            Formation.OneThreeOne => [(Blue + 2.2f, 0), (G - 9f, -9f), (G - 9f, 9f), (G - 2.2f, 0), (G - 8f, 0)],
            Formation.Box when skaters <= 3 => [(G - 4.2f, -2.8f), (G - 4.2f, 2.8f), (G - 10f, 0)],
            Formation.Box => [(G - 4.3f, -2.9f), (G - 4.3f, 2.9f), (G - 10.7f, -5.2f), (G - 10.7f, 5.2f)],
            Formation.Diamond when skaters <= 3 => [(G - 4.2f, -2.8f), (G - 4.2f, 2.8f), (G - 10f, 0)],
            Formation.Diamond => [(G - 3.1f, 0), (G - 7f, -5.6f), (G - 7f, 5.6f), (G - 11.5f, 0)],
            _ => [],
        };

        // the power play leans a little to the puck; the kill slides and steps up toward it, keeping its shape
        bool kill = !IsPowerPlay(f);
        float slide = kill ? 0.3f : 0.12f;
        float step = kill ? Math.Clamp((pa - (G - 9f)) * -0.12f, -1.2f, 1.2f) : 0f;
        var spots = new List<Vector2>();
        for (int i = 0; i < Math.Min(skaters, shape.Length); i++)
        {
            var (a, z) = shape[i];
            if (kill) z = Math.Clamp(z + pz * slide, -RinkSpec.HalfWidth + 2f, RinkSpec.HalfWidth - 2f);
            else z += pz * slide * (MathF.Abs(z) < 1f ? 1f : 0.5f);
            spots.Add(TeamShape.Inside(new Vector2(end * (a - step), z), 1.2f));
        }
        return spots;
    }

    /// <summary>Gets whether a spot (x, z) is in the zone the power play attacks.</summary>
    /// <param name="end">The end the power play attacks.</param>
    /// <param name="at">The spot.</param>
    /// <param name="margin">How far past the blue line it has to be.</param>
    /// <returns>True inside the zone.</returns>
    public static bool InZone(int end, Vector2 at, float margin = 0f)
    {
        return end * at.X > Blue + margin;
    }

    /// <summary>
    /// Hands the spots to skaters: the cheapest pairing over every order (five skaters is 120
    /// orders), each skater's cost his distance to the spot, with a defenceman cheaper at the point.
    /// </summary>
    /// <param name="skaters">Where each skater is, and whether he is a defenceman.</param>
    /// <param name="spots">The spots, as many or more than the skaters.</param>
    /// <returns>For each skater, the index of his spot, and the total cost.</returns>
    public static (int[] Spot, float Cost) Assign(IReadOnlyList<(Vector2 At, bool Defence)> skaters, IReadOnlyList<Vector2> spots)
    {
        int n = Math.Min(skaters.Count, spots.Count);
        int[] best = new int[skaters.Count];
        Array.Fill(best, -1);
        float bestCost = float.MaxValue;
        int[] order = new int[n];
        bool[] used = new bool[spots.Count];
        void Walk(int i, float cost)
        {
            if (cost >= bestCost) return;
            if (i == n)
            {
                bestCost = cost;
                Array.Fill(best, -1);
                for (int k = 0; k < n; k++) best[k] = order[k];
                return;
            }
            for (int s = 0; s < spots.Count; s++)
            {
                if (used[s]) continue;
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
    /// <param name="skater">Where the skater is, and whether he is a defenceman.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="index">The spot's place in its formation; 0 is the point on a power play.</param>
    /// <returns>The cost.</returns>
    public static float Cost((Vector2 At, bool Defence) skater, Vector2 spot, int index)
    {
        return Vector2.Distance(skater.At, spot) - (skater.Defence && index == 0 ? 4f : 0f);
    }
}
