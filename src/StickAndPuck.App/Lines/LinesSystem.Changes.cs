using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Lines
{
    internal sealed partial class LinesSystem
    {
        /// <summary>Calls a change: pairs each skater of the group on the ice with one coming on, and takes the human off a leaving skater.</summary>
        private static Maybe<ShiftChange> Begin(
            World world,
            LinesBoard b,
            Side side,
            Group group,
            Unit unit
        )
        {
            List<Player> leaving = [.. Bench.OnIce(world, side, group)];
            if (leaving.Count == 0)
            {
                return Maybe.None<ShiftChange>();
            }
            // a skater in the penalty box never comes on
            List<Player> coming =
            [
                .. unit.Members.Where(p => !p.OnIce && !PenaltyCalls.InBox(world, p.Id)),
            ];
            List<Player> spares =
            [
                .. world
                    .Players.Where(p =>
                        p.Side == side
                        && !p.OnIce
                        && !p.IsGoalie
                        && Squad.GroupOf(p) == group
                        && !coming.Contains(p)
                        && !PenaltyCalls.InBox(world, p.Id)
                    )
                    .OrderByDescending(p => p.Energy),
            ];

            List<Swap> swaps = [];
            foreach (Player off in leaving)
            {
                if (
                    (
                        coming.Find(p => p.Data.Position == off.Data.Position)
                        ?? coming.FirstOrDefault()
                        ?? spares.FirstOrDefault()
                    )
                    is not { } on
                )
                {
                    break;
                }

                _ = coming.Remove(on);
                _ = spares.Remove(on);
                swaps.Add(new Swap(off, on));
            }
            if (swaps.Count == 0)
            {
                return Maybe.None<ShiftChange>();
            }

            ShiftChange change = new(side, group, unit, swaps, world.Time);
            b.Changing[(int)side] = change;
            foreach (Swap s in swaps)
            {
                HandOff(world, s.Out, change);
            }

            return change;
        }

        /// <summary>Moves the skaters of a change on the fly toward the bench and swaps each one at the door.</summary>
        private static void Progress(World world, LinesBoard b, ShiftChange change)
        {
            bool late = world.Time - change.Started > LinesTuning.LeaveTimeout;
            foreach (Swap s in change.Swaps.Where(s => !s.Done))
            {
                Player off = s.Out;
                if (!off.OnIce || s.In.OnIce)
                {
                    s.Done = true;
                    continue;
                } // taken off by someone else (a penalty), or already on
                if (world.Puck.Carrier == off.Id)
                {
                    continue; // nobody leaves with the puck
                }

                float d = Vector2.Distance(Flat(off), s.Door);
                if (d < LinesTuning.DoorReach || late)
                {
                    Swap(world, b, change, s, onTheFly: true);
                    continue;
                }
                if (off.Slot >= 0)
                {
                    HandOff(world, off, change);
                }

                if (off.Slot < 0)
                {
                    off.Command = Steering.Toward(
                        off,
                        s.Door,
                        1.2f,
                        sprint: d > Steering.SprintFrom
                    );
                }
            }
            if (change.Swaps.TrueForAll(s => s.Done))
            {
                b.Changing[(int)change.Side] = Maybe.None<ShiftChange>();
            }
        }

        private static void Finish(World world, LinesBoard b, ShiftChange change, bool onTheFly)
        {
            foreach (Swap s in change.Swaps.Where(s => !s.Done))
            {
                if (s.Out.OnIce && !s.In.OnIce)
                {
                    Swap(world, b, change, s, onTheFly);
                }

                s.Done = true;
            }
            b.Changing[(int)change.Side] = Maybe.None<ShiftChange>();
        }

        private static void Swap(
            World world,
            LinesBoard b,
            ShiftChange change,
            Swap s,
            bool onTheFly
        )
        {
            Put(s.Out, s.In, onTheFly);
            s.Done = true;
            if (change.Announced)
            {
                return;
            }

            change.Announced = true;
            world.Events.Publish(new LineChanged(world.Time, change.Side, s.In.Id, s.In.Position));
            b.Last[(int)change.Side] = (change.Incoming.Name, world.Time);
        }

        /// <summary>One player off, the other on: at the bench door on the fly, in the same spot at a stoppage (the faceoff places them).</summary>
        private static void Put(Player off, Player on, bool onTheFly)
        {
            Vector2 door = Squad.Door(off.Side);
            on.OnIce = true;
            on.Position = onTheFly ? new Vector3(door.X, 0, door.Y + 0.4f) : off.Position;
            on.Velocity = onTheFly ? new Vector3(0, 0, 1.5f) : Vector3.Zero;
            on.Heading = onTheFly ? 0f : off.Heading;
            on.Command = default;
            on.Begin(PlayerAction.None);
            if (off.Slot >= 0)
            {
                on.Slot = off.Slot;
                off.Slot = -1;
            }
            off.OnIce = false;
            off.Velocity = Vector3.Zero;
            off.Command = default;
            off.Position = new Vector3(door.X, 0, door.Y - 1.5f);
            off.Begin(PlayerAction.None);
        }

        /// <summary>Gives a human's control from a leaving skater to the teammate nearest the puck who is staying, if there is one.</summary>
        private static void HandOff(World world, Player off, ShiftChange change)
        {
            if (off.Slot < 0)
            {
                return;
            }

            if (
                world
                    .OnIce(off.Side)
                    .Where(p =>
                        !p.IsGoalie
                        && p.Slot < 0
                        && !change.Swaps.Exists(s => !s.Done && s.Out == p)
                    )
                    .MinBy(p => Vector3.DistanceSquared(p.Position, world.Puck.Position))
                is not { } to
            )
            {
                return;
            }
            PlayerCommand carried = off.Command;
            to.Command = carried with
            {
                Pass = false,
                Check = false,
                Shoot = false,
                SwitchPlayer = false,
                ChangeLine = false,
            };
            to.Slot = off.Slot;
            off.Slot = -1;
            off.Command = default;
        }
    }
}
