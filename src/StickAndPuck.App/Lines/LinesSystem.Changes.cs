using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Lines;

internal sealed partial class LinesSystem
{
    /// <summary>Calls a change: pairs each skater of the group on the ice with one coming on, and takes the human off a leaving skater.</summary>
    static Maybe<ShiftChange> Begin(World world, LinesBoard b, Side side, Group group, Unit unit)
    {
        var leaving = Bench.OnIce(world, side, group).ToList();
        if (leaving.Count == 0) return Maybe<ShiftChange>.None;
        // a skater in the penalty box never comes on
        var coming = unit.Members.Where(p => !p.OnIce && !Penalties.InBox(world, p.Id)).ToList();
        var spares = world.Players
            .Where(p => p.Side == side && !p.OnIce && !p.IsGoalie && Squad.GroupOf(p) == group && !coming.Contains(p) && !Penalties.InBox(world, p.Id))
            .OrderByDescending(p => p.Energy)
            .ToList();

        var swaps = new List<Swap>();
        foreach (var off in leaving)
        {
            if ((coming.Find(p => p.Data.Position == off.Data.Position) ?? coming.FirstOrDefault() ?? spares.FirstOrDefault()) is not { } on) break;
            coming.Remove(on);
            spares.Remove(on);
            swaps.Add(new Swap(off, on));
        }
        if (swaps.Count == 0) return Maybe<ShiftChange>.None;

        var change = new ShiftChange(side, group, unit, swaps, world.Time);
        b.Changing[(int)side] = change;
        foreach (var s in swaps) HandOff(world, s.Out, change);
        return change;
    }

    /// <summary>Moves the skaters of a change on the fly toward the bench and swaps each one at the door.</summary>
    static void Progress(World world, LinesBoard b, ShiftChange change)
    {
        bool late = world.Time - change.Started > LinesTuning.LeaveTimeout;
        foreach (var s in change.Swaps.Where(s => !s.Done))
        {
            var off = s.Out;
            if (!off.OnIce || s.In.OnIce) { s.Done = true; continue; }   // taken off by someone else (a penalty), or already on
            if (world.Puck.Carrier == off.Id) continue;                  // nobody leaves with the puck
            float d = Vector2.Distance(Flat(off), s.Door);
            if (d < LinesTuning.DoorReach || late)
            {
                Swap(world, b, change, s, onTheFly: true);
                continue;
            }
            if (off.Slot >= 0) HandOff(world, off, change);
            if (off.Slot < 0) off.Command = Steering.Toward(off, s.Door, 1.2f, sprint: d > Steering.SprintFrom);
        }
        if (change.Swaps.TrueForAll(s => s.Done)) b.Changing[(int)change.Side] = Maybe<ShiftChange>.None;
    }

    static void Finish(World world, LinesBoard b, ShiftChange change, bool onTheFly)
    {
        foreach (var s in change.Swaps.Where(s => !s.Done))
        {
            if (s.Out.OnIce && !s.In.OnIce) Swap(world, b, change, s, onTheFly);
            s.Done = true;
        }
        b.Changing[(int)change.Side] = Maybe<ShiftChange>.None;
    }

    static void Swap(World world, LinesBoard b, ShiftChange change, Swap s, bool onTheFly)
    {
        Put(s.Out, s.In, onTheFly);
        s.Done = true;
        if (change.Announced) return;
        change.Announced = true;
        world.Events.Publish(new LineChanged(world.Time, change.Side, s.In.Id, s.In.Position));
        b.Last[(int)change.Side] = (change.Incoming.Name, world.Time);
    }

    /// <summary>One player off, the other on: at the bench door on the fly, in the same spot at a stoppage (the faceoff places them).</summary>
    static void Put(Player off, Player on, bool onTheFly)
    {
        var door = Squad.Door(off.Side);
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
    static void HandOff(World world, Player off, ShiftChange change)
    {
        if (off.Slot < 0) return;
        if (world.OnIce(off.Side)
            .Where(p => !p.IsGoalie && p.Slot < 0 && !change.Swaps.Exists(s => !s.Done && s.Out == p))
            .MinBy(p => Vector3.DistanceSquared(p.Position, world.Puck.Position)) is not { } to)
        {
            return;
        }
        var carried = off.Command;
        to.Command = carried with { Pass = false, Check = false, Shoot = false, SwitchPlayer = false, ChangeLine = false };
        to.Slot = off.Slot;
        off.Slot = -1;
        off.Command = default;
    }
}
