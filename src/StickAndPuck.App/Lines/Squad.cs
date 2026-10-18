using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>One club's match squad: four forward lines, three defence pairs and two goalies, all of them in <see cref="World.Players"/>.</summary>
    public sealed class Squad
    {
        private readonly List<Unit> lines;
        private readonly List<Unit> pairs;

        private Squad(Side side, List<Unit> lines, List<Unit> pairs, List<Player> goalies)
        {
            Side = side;
            this.lines = lines;
            this.pairs = pairs;
            Goalies = goalies;
        }

        /// <summary>Gets the club.</summary>
        public Side Side { get; }

        /// <summary>Gets the forward lines, line 1 first.</summary>
        public IReadOnlyList<Unit> Lines => lines;

        /// <summary>Gets the defence pairs, pair 1 first.</summary>
        public IReadOnlyList<Unit> Pairs => pairs;

        /// <summary>Gets the goalies, the starter first.</summary>
        public IReadOnlyList<Player> Goalies { get; }

        /// <summary>Gets the units of one group.</summary>
        public IReadOnlyList<Unit> Units(Group group)
        {
            return group == Group.Forwards ? Lines : Pairs;
        }

        /// <summary>Gets the group a skater plays in.</summary>
        public static Group GroupOf(Player p)
        {
            return p.Data.Position == PlayingPosition.D ? Group.Defence : Group.Forwards;
        }

        /// <summary>Gets the bench door of a club: on the far boards, home left of centre and away right of it.</summary>
        public static Vector2 Door(Side side)
        {
            return new Vector2(side == Side.Home ? -6f : 6f, -(RinkSpec.HalfWidth - 0.9f));
        }

        /// <summary>Builds a club's squad from its roster, adding everyone not yet in the world to the bench.
        /// Players already on the ice (the starters) keep their place. Lines take centres, left and
        /// right wings in roster order, pairs take defencemen two by two; gaps are filled from the
        /// forwards or defencemen left over.</summary>
        public static Squad Build(World world, Side side)
        {
            Vector2 door = Door(side);
            List<Player> players = BuildPlayers(world, side, door);
            List<Unit> forwardLines = BuildForwardLines(players);
            List<Unit> defencePairs = BuildDefencePairs(players);
            List<Player> goalies = BuildGoalies(players);
            return new Squad(side, forwardLines, defencePairs, goalies);
        }

        private static List<Player> BuildPlayers(World world, Side side, Vector2 door)
        {
            List<Player> players = [];
            foreach (PlayerData data in world.Setup.Club(side).Roster)
            {
                if (
                    world.Players.Find(o => o.Side == side && ReferenceEquals(o.Data, data))
                    is not { } p
                )
                {
                    p = world.AddPlayer(side, data, new Vector3(door.X, 0, door.Y - 1.5f));
                    p.OnIce = false;
                }
                players.Add(p);
            }
            return players;
        }

        private static List<Unit> BuildForwardLines(List<Player> players)
        {
            Queue<Player> centres = Queue(players, PlayingPosition.C);
            Queue<Player> lefts = Queue(players, PlayingPosition.LW);
            Queue<Player> rights = Queue(players, PlayingPosition.RW);
            List<Unit> forwardLines = [];
            for (int i = 0; i < 4; i++)
            {
                List<Player> members = [];
                foreach (Queue<Player> q in new[] { centres, lefts, rights })
                {
                    Maybe<Queue<Player>> from = q.Count > 0 ? q : Spare(centres, lefts, rights);
                    if (from.TryGet(out Queue<Player> queue))
                    {
                        members.Add(queue.Dequeue());
                    }
                }
                if (members.Count > 0)
                {
                    forwardLines.Add(new Unit(Group.Forwards, i + 1, members));
                }
            }
            return forwardLines;
        }

        private static List<Unit> BuildDefencePairs(List<Player> players)
        {
            Queue<Player> defence = Queue(players, PlayingPosition.D);
            List<Unit> defencePairs = [];
            for (int i = 0; i < 3 && defence.Count > 0; i++)
            {
                List<Player> members = [defence.Dequeue()];
                if (defence.Count > 0)
                {
                    members.Add(defence.Dequeue());
                }

                if (
                    members.Count == 2
                    && members[0].Data.Shoots == Hand.Right
                    && members[1].Data.Shoots == Hand.Left
                )
                {
                    members.Reverse();
                }

                defencePairs.Add(new Unit(Group.Defence, i + 1, members));
            }
            return defencePairs;
        }

        private static List<Player> BuildGoalies(List<Player> players)
        {
            return [.. players.Where(p => p.IsGoalie).OrderByDescending(p => p.OnIce)];
        }

        /// <summary>Gets the unit a player belongs to, if any.</summary>
        public Maybe<Unit> UnitOf(Player p)
        {
            return Lines.Concat(Pairs).FirstOrNone(u => u.Members.Contains(p));
        }

        /// <summary>Takes a player out of the squad for good, as an ejection does, and refills his unit:
        /// the last unit of his group gives up a skater, of the same position if it has one, so the top
        /// lines and pairs stay whole and the last one plays short (a change fills it from the bench).
        /// A unit left with nobody is dropped.</summary>
        public Maybe<Player> Remove(Player p)
        {
            if (!UnitOf(p).TryGet(out Unit unit))
            {
                return Maybe.None<Player>();
            }

            List<Unit> units = unit.Group == Group.Forwards ? lines : pairs;
            int at = units.IndexOf(unit);
            List<Player> members = [.. unit.Members];
            int slot = members.IndexOf(p);
            Unit last = units[^1];
            Maybe<Player> donor = Maybe.None<Player>();
            if (last != unit && last.Members.Count > 0)
            {
                Player up =
                    last.Members.FirstOrDefault(m => m.Data.Position == p.Data.Position)
                    ?? last.Members[^1];
                donor = up;
                members[slot] = up;
                units[^1] = last with { Members = [.. last.Members.Where(m => m != up)] };
            }
            else
            {
                members.RemoveAt(slot);
            }
            units[at] = unit with { Members = members };
            _ = units.RemoveAll(u => u.Members.Count == 0);
            return donor;
        }

        private static Queue<Player> Queue(List<Player> players, PlayingPosition position)
        {
            // whoever is already on the ice leads, so the starters make up line 1 and pair 1
            return new Queue<Player>(
                players.Where(p => p.Data.Position == position).OrderByDescending(p => p.OnIce)
            );
        }

        private static Maybe<Queue<Player>> Spare(params Queue<Player>[] queues)
        {
            return Maybe.From(queues.Where(q => q.Count > 0).MaxBy(q => q.Count));
        }
    }
}
