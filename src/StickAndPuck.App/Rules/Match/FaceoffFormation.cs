using System.Collections.Immutable;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>Where each player lines up for a faceoff: centres at the dot, wings beside them, defence behind, goalies in their crease.</summary>
    public static class FaceoffFormation
    {
        // spots for the club defending -x, relative to the dot: centre, left wing, right wing, two defence
        private static readonly ImmutableArray<Vector3> Spots =
        [
            new(-0.8f, 0, 0),
            new(-1.2f, 0, -4.8f),
            new(-1.2f, 0, 4.8f),
            new(-6f, 0, -3.5f),
            new(-6f, 0, 3.5f),
        ];
        private static readonly ImmutableArray<PlayingPosition> SpotPositions =
        [
            PlayingPosition.C,
            PlayingPosition.LW,
            PlayingPosition.RW,
            PlayingPosition.D,
            PlayingPosition.D,
        ];

        // which spots are used when a club has fewer than five skaters (3 on 3: centre and two defence)
        private static readonly ImmutableArray<int> FillOrder = [0, 3, 4, 1, 2];

        /// <summary>Gets the end a club defends this period: -1 for -x, +1 for +x.</summary>
        public static int DefendedEnd(MatchState match, Side side)
        {
            return side == match.DefendsLeft ? -1 : 1;
        }

        /// <summary>Puts every on-ice player on their faceoff spot around a dot, standing still and facing up ice.</summary>
        public static void Place(World world, Vector3 dot)
        {
            Place(world, dot, []);
        }

        /// <summary>Puts every on-ice player on their faceoff spot, with the skaters in <paramref name="kicked"/>
        /// (waved out after a faceoff violation) kept off the centre spot: a winger takes the draw instead.</summary>
        public static void Place(World world, Vector3 dot, IReadOnlyCollection<int> kicked)
        {
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int end = DefendedEnd(world.Match, side);
                float heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
                List<Player> skaters = [.. world.OnIce(side).Where(p => !p.IsGoalie)];
                foreach ((Player player, int spot) in Assign(skaters, kicked))
                {
                    Vector3 offset = Spots[spot];
                    Put(player, Clamp(dot + new Vector3(-end * offset.X, 0, offset.Z)), heading);
                }
                foreach (Player g in world.OnIce(side).Where(p => p.IsGoalie))
                {
                    Put(g, new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0), heading);
                }
            }
        }

        /// <summary>Gets the spot of each skater: the first skaters fill <see cref="FillOrder"/>, each spot preferring a matching position.
        /// A kicked skater never takes the centre spot while another can: the centre first, then a winger, then anyone.</summary>
        private static List<(Player Player, int Spot)> Assign(
            List<Player> skaters,
            IReadOnlyCollection<int> kicked
        )
        {
            List<Player> free = [with(skaters)];
            List<int> spots = [.. FillOrder.Take(Math.Min(skaters.Count, FillOrder.Length))];
            List<(Player, int)> pairs = [];
            foreach (int spot in spots)
            {
                if (
                    (
                        spot == 0 && kicked.Count > 0
                            ? free.Where(p => !kicked.Contains(p.Id)).MinBy(DrawRank)
                            : free.Find(p => p.Data.Position == SpotPositions[spot])
                    )
                    is not { } match
                )
                {
                    continue;
                }

                pairs.Add((match, spot));
                _ = free.Remove(match);
            }
            foreach (int spot in spots.Where(s => pairs.TrueForAll(pr => pr.Item2 != s)))
            {
                pairs.Add((free[0], spot));
                free.RemoveAt(0);
            }
            // more than five skaters (should not happen): stack the rest behind the defence
            foreach (Player extra in free)
            {
                pairs.Add((extra, 3));
            }

            return pairs;
        }

        /// <summary>Gets who takes the draw first once the centre is waved out: a centre, then a winger, then a defenceman.</summary>
        private static int DrawRank(Player p)
        {
            return p.Data.Position switch
            {
                PlayingPosition.C => 0,
                PlayingPosition.LW or PlayingPosition.RW => 1,
                PlayingPosition.D => 2,
                PlayingPosition.G => 3,
                _ => 3,
            };
        }

        private static void Put(Player p, Vector3 position, float heading)
        {
            p.Position = position;
            p.Velocity = Vector3.Zero;
            p.Heading = heading;
        }

        private static Vector3 Clamp(Vector3 v)
        {
            const float margin = 0.8f;
            return new Vector3(
                Math.Clamp(v.X, -RinkSpec.GoalLineX, RinkSpec.GoalLineX),
                0,
                Math.Clamp(v.Z, -RinkSpec.HalfWidth + margin, RinkSpec.HalfWidth - margin)
            );
        }
    }
}
