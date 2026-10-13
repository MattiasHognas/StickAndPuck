using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>Where each player lines up for a faceoff: centres at the dot, wings beside them, defence behind, goalies in their crease.</summary>
public static class FaceoffFormation
{
    // spots for the club defending -x, relative to the dot: centre, left wing, right wing, two defence
    static readonly Vector3[] Spots = [new(-0.8f, 0, 0), new(-1.2f, 0, -4.8f), new(-1.2f, 0, 4.8f), new(-6f, 0, -3.5f), new(-6f, 0, 3.5f)];
    static readonly PlayingPosition[] SpotPositions = [PlayingPosition.C, PlayingPosition.LW, PlayingPosition.RW, PlayingPosition.D, PlayingPosition.D];

    // which spots are used when a club has fewer than five skaters (3 on 3: centre and two defence)
    static readonly int[] FillOrder = [0, 3, 4, 1, 2];

    /// <summary>Gets the end a club defends this period: -1 for -x, +1 for +x.</summary>
    /// <param name="match">The match state.</param>
    /// <param name="side">The club.</param>
    /// <returns>-1 or +1.</returns>
    public static int DefendedEnd(MatchState match, Side side)
    {
        return side == match.DefendsLeft ? -1 : 1;
    }

    /// <summary>Puts every on-ice player on their faceoff spot around a dot, standing still and facing up ice.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="dot">The faceoff dot.</param>
    public static void Place(World world, Vector3 dot)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int end = DefendedEnd(world.Match, side);
            float heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            var skaters = world.OnIce(side).Where(p => !p.IsGoalie).ToList();
            foreach (var (player, spot) in Assign(skaters))
            {
                var offset = Spots[spot];
                Put(player, Clamp(dot + new Vector3(-end * offset.X, 0, offset.Z)), heading);
            }
            foreach (var g in world.OnIce(side).Where(p => p.IsGoalie))
                Put(g, new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0), heading);
        }
    }

    /// <summary>Gets the spot of each skater: the first skaters fill <see cref="FillOrder"/>, each spot preferring a matching position.</summary>
    static IEnumerable<(Player Player, int Spot)> Assign(List<Player> skaters)
    {
        var free = new List<Player>(skaters);
        var spots = FillOrder.Take(Math.Min(skaters.Count, FillOrder.Length)).ToList();
        var pairs = new List<(Player, int)>();
        foreach (int spot in spots)
        {
            var match = free.Find(p => p.Data.Position == SpotPositions[spot]);
            if (match == null) continue;
            pairs.Add((match, spot));
            free.Remove(match);
        }
        foreach (int spot in spots.Where(s => pairs.All(pr => pr.Item2 != s)))
        {
            pairs.Add((free[0], spot));
            free.RemoveAt(0);
        }
        // more than five skaters (should not happen): stack the rest behind the defence
        foreach (var extra in free) pairs.Add((extra, 3));
        return pairs;
    }

    static void Put(Player p, Vector3 position, float heading)
    {
        p.Position = position;
        p.Velocity = Vector3.Zero;
        p.Heading = heading;
    }

    static Vector3 Clamp(Vector3 v)
    {
        const float margin = 0.8f;
        return new Vector3(
            Math.Clamp(v.X, -RinkSpec.GoalLineX, RinkSpec.GoalLineX),
            0,
            Math.Clamp(v.Z, -RinkSpec.HalfWidth + margin, RinkSpec.HalfWidth - margin));
    }
}
