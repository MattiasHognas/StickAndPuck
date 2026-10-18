using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>What the bench knows, for HUD, AI and screens to read. <see cref="LinesSystem"/> keeps it
    /// up to date in every world that runs the match flow.</summary>
    public static class Bench
    {
        /// <summary>Gets a club's squad, or None in a world without the match flow.</summary>
        public static Maybe<Squad> SquadOf(World world, Side side)
        {
            return world.Lines.TryGet(out LinesBoard b) ? b.Squads[(int)side] : Maybe.None<Squad>();
        }

        /// <summary>Gets whether a club has a change on the fly under way: skaters heading for the bench.</summary>
        public static bool IsChanging(World world, Side side)
        {
            return world.Lines.TryGet(out LinesBoard b) && b.Changing[(int)side].HasValue;
        }

        /// <summary>Gets whether a skater is on the way to the bench.</summary>
        public static bool IsLeaving(World world, Player p)
        {
            return world.Lines.TryGet(out LinesBoard b)
                && b.Changing[(int)p.Side].TryGet(out ShiftChange c)
                && c.Swaps.Exists(s => !s.Done && s.Out == p);
        }

        /// <summary>Gets the mean energy of a club's forwards or defencemen on the ice.</summary>
        public static float OnIceEnergy(World world, Side side, Group group)
        {
            List<Player> on = [.. OnIce(world, side, group)];
            return on.Count == 0 ? 1f : on.Average(p => p.Energy);
        }

        /// <summary>Gets the unit most of a group on the ice belongs to.</summary>
        public static Maybe<Unit> OnIceUnit(World world, Side side, Group group)
        {
            return SquadOf(world, side).TryGet(out Squad squad)
                ? Maybe.From(squad.Units(group).MaxBy(u => u.Members.Count(m => m.OnIce)))
                : Maybe.None<Unit>();
        }

        /// <summary>Asks for an early change, as the change line button does: the more tired of forwards and
        /// defence goes off, on the fly in play, or at the next faceoff while play is stopped.</summary>
        public static void RequestChange(World world, Side side)
        {
            if (world.Lines.TryGet(out LinesBoard b))
            {
                b.Requested[(int)side] = true;
            }
        }

        /// <summary>Sends a skater on from the bench, the extra attacker or the skater a club adds for a 4 on 3
        /// power play in overtime: the freshest of the group who is not in the penalty box, from the
        /// other group when that one is empty. On the fly he steps out of the bench door; at a stoppage
        /// he waits at the door for the faceoff to place him.</summary>
        public static Maybe<Player> SendOn(World world, Side side, Group group, bool onTheFly)
        {
            if (
                world
                    .Players.Where(p =>
                        p.Side == side
                        && !p.OnIce
                        && !p.IsGoalie
                        && !Rules.Penalties.PenaltyCalls.InBox(world, p.Id)
                    )
                    .OrderByDescending(p => Squad.GroupOf(p) == group)
                    .ThenByDescending(p => p.Energy)
                    .FirstOrDefault()
                is not { } on
            )
            {
                return Maybe.None<Player>();
            }
            Vector2 door = Squad.Door(side);
            on.OnIce = true;
            on.Position = new Vector3(door.X, 0, door.Y + (onTheFly ? 0.4f : 1.5f));
            on.Velocity = onTheFly ? new Vector3(0, 0, 1.5f) : Vector3.Zero;
            on.Heading = 0f;
            on.Command = default;
            on.Begin(PlayerAction.None);
            return on;
        }

        /// <summary>Sends a player on the ice to the bench, handing a human's control to the teammate nearest the puck.</summary>
        public static void SendOff(World world, Player p)
        {
            if (!p.OnIce)
            {
                return;
            }

            if (p.Slot >= 0)
            {
                if (
                    world
                        .OnIce(p.Side)
                        .Where(o => o != p && !o.IsGoalie && o.Slot < 0)
                        .MinBy(o => Vector3.DistanceSquared(o.Position, world.Puck.Position)) is
                    { } to
                )
                {
                    to.Slot = p.Slot;
                }

                p.Slot = -1;
            }
            Vector2 door = Squad.Door(p.Side);
            p.OnIce = false;
            p.Position = new Vector3(door.X, 0, door.Y - 1.5f);
            p.Velocity = Vector3.Zero;
            p.Command = default;
            p.Begin(PlayerAction.None);
            if (world.Puck.Carrier == p.Id)
            {
                world.Puck.Carrier = -1;
            }

            if (
                world.Lines.TryGet(out LinesBoard b)
                && b.Changing[(int)p.Side].TryGet(out ShiftChange change)
            )
            {
                foreach (Swap s in change.Swaps.Where(s => s.Out == p || s.In == p))
                {
                    s.Done = true;
                }
            }
        }

        /// <summary>Gets the skater a club can spare when it has more on the ice than it should: the extra
        /// attacker if he is out there, else the most tired of the group with more than its
        /// share (one forward at three a side, two defence at four or more), a human's skater last.</summary>
        public static Maybe<Player> Surplus(World world, Side side, int target)
        {
            if (GoaliePull.ExtraAttacker(world, side).TryGet(out Player extra))
            {
                return extra;
            }

            List<Player> skaters = [.. world.OnIce(side).Where(p => !p.IsGoalie)];
            int defenceWanted = target >= 4 ? 2 : Math.Max(0, target - 1);
            int defence = skaters.Count(p => Squad.GroupOf(p) == Group.Defence);
            Group group = defence > defenceWanted ? Group.Defence : Group.Forwards;
            return Maybe.From(
                skaters
                    .Where(p => Squad.GroupOf(p) == group)
                    .OrderBy(p => p.Slot >= 0)
                    .ThenBy(p => p.Energy)
                    .FirstOrDefault()
                    ?? skaters.OrderBy(p => p.Slot >= 0).ThenBy(p => p.Energy).FirstOrDefault()
            );
        }

        /// <summary>Takes an ejected player out of his club's squad for the rest of the match: off the ice
        /// if he is on it, out of his line or pair, and a skater from the last unit moves up into the gap
        /// (<see cref="Squad.Remove"/>). The penalty box keeps him off the bench (<see cref="Rules.Penalties.PenaltyCalls.InBox"/>).</summary>
        public static void Eject(World world, Player p)
        {
            SendOff(world, p);
            if (!SquadOf(world, p.Side).TryGet(out Squad squad))
            {
                return;
            }

            Maybe<Unit> unit = squad.UnitOf(p);
            Maybe<Player> up = squad.Remove(p);
            if (unit.TryGet(out Unit refilled) && Rules.Match.MatchFlow.State(world).Log)
            {
                Console.WriteLine(
                    $"LINES: {world.Setup.Club(p.Side).ShortName} {refilled.Name} refilled{(up.TryGet(out Player mover) ? $" with {mover.Data.LastName}" : " short")}"
                );
            }
        }

        internal static IEnumerable<Player> OnIce(World world, Side side, Group group)
        {
            return world.OnIce(side).Where(p => !p.IsGoalie && Squad.GroupOf(p) == group);
        }

        internal static LinesBoard Board(World world)
        {
            return world.Lines.Get(() => new LinesBoard());
        }
    }
}
