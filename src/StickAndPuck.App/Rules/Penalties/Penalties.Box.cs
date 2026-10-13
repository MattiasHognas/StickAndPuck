using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Rules;

public static partial class Penalties
{
    /// <summary>A game misconduct: the offender leaves the ice for the match, out of every line, and never comes back.</summary>
    static void Eject(World world, PenaltyBook book, Player p)
    {
        HandOff(world, p);
        if (world.Puck.Carrier == p.Id) world.Puck.Carrier = -1;
        book.Ejected.Add(p.Id);
        Lines.Bench.Eject(world, p);
        p.OnIce = false;
        p.Position = new Vector3(0, 0, RinkSpec.HalfWidth + 6f);
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
        book.Log(world, $"{Name(world, p)} ejected, lines refill without him");
    }

    /// <summary>Gets whether a penalised skater stays in the box when his club's time is up: ejected, or a misconduct still to sit.</summary>
    static bool Stays(PenaltyBook book, int playerId)
    {
        return book.Ejected.Contains(playerId) || book.Misconducts.Exists(m => m.PlayerId == playerId);
    }

    /// <summary>Ends a running penalty, served or ended by a goal: a substituted skater goes to the bench,
    /// the club's next waiting penalty starts with the skater whose time is up kept in the box until
    /// play stops, and otherwise he comes out of the box door onto the ice.</summary>
    internal static void End(World world, PenaltyBook book, PenaltyState penalty, string why)
    {
        book.Lengths.Remove(penalty.PlayerId);
        if (book.Substituted.Remove(penalty.PlayerId))
        {
            world.Match.Penalties.Remove(penalty);
            ToBench(world, world.Players[penalty.PlayerId]);
            book.Log(world, $"{Name(world, world.Players[penalty.PlayerId])} to the bench ({why}, substituted)");
            return;
        }

        if (book.Waiting.Find(w => w.Side == penalty.Side) is { } next)
        {
            world.Match.Penalties.Remove(penalty);
            book.Waiting.Remove(next);
            world.Match.Penalties.Add(next);
            if (!Stays(book, penalty.PlayerId)) book.Held.Add(penalty.PlayerId);
            book.Log(world, $"{Name(world, world.Players[next.PlayerId])}'s penalty starts, {Name(world, world.Players[penalty.PlayerId])} waits for the whistle ({why})");
            return;
        }

        if (Stays(book, penalty.PlayerId))
        {
            // The offender stays (ejected or a misconduct to sit); the teammate serving for him comes out
            world.Match.Penalties.Remove(penalty);
            var sub = Maybe.From<Player>(world.Players
                .Where(o => o.Side == penalty.Side && !o.OnIce && !o.IsGoalie && !InBox(world, o.Id))
                .MaxBy(o => o.Energy));
            if (sub.TryGet(out var serving)) OutOfTheBox(serving);
            book.Log(world, $"{(sub.TryGet(out var named) ? Name(world, named) : "nobody")} out of the box for {Name(world, world.Players[penalty.PlayerId])} ({why})");
            return;
        }

        Release(world, book, penalty, why);
    }

    /// <summary>A misconduct is up: the skater goes to the bench at the next stoppage, or straight away if his club is still serving for him.</summary>
    internal static void EndMisconduct(World world, PenaltyBook book, PenaltyState misconduct)
    {
        book.Misconducts.Remove(misconduct);
        if (world.Match.Penalties.Exists(p => p.PlayerId == misconduct.PlayerId) || book.Waiting.Exists(p => p.PlayerId == misconduct.PlayerId)) return;
        book.Held.Add(misconduct.PlayerId);
        book.Log(world, $"{Name(world, world.Players[misconduct.PlayerId])}'s misconduct is up, out at the next whistle");
    }

    /// <summary>Play has stopped: skaters kept in the box after their time was up go to the bench, free to come on.</summary>
    internal static void LetOutHeld(World world, PenaltyBook book)
    {
        if (book.Held.Count == 0) return;
        foreach (int id in book.Held) ToBench(world, world.Players[id]);
        book.Held.Clear();
    }

    /// <summary>Empties the box: every skater in it, waiting or done, goes to the bench and no call stays pending.</summary>
    internal static void ClearBox(World world, PenaltyBook book)
    {
        book.Delayed.Clear();
        var ids = world.Match.Penalties.Select(p => p.PlayerId).Concat(book.Waiting.Select(p => p.PlayerId)).Concat(book.Held).Concat(book.Misconducts.Select(p => p.PlayerId)).ToList();
        foreach (int id in ids.Where(id => !world.Players[id].OnIce && !book.Ejected.Contains(id))) ToBench(world, world.Players[id]);
        world.Match.Penalties.Clear();
        book.Waiting.Clear();
        book.Held.Clear();
        book.Substituted.Clear();
        book.Misconducts.Clear();
        book.Lengths.Clear();
    }

    /// <summary>Ends a penalty: the skater comes out of the box door onto the ice.</summary>
    internal static void Release(World world, PenaltyBook book, PenaltyState penalty, string why)
    {
        world.Match.Penalties.Remove(penalty);
        var p = world.Players[penalty.PlayerId];
        OutOfTheBox(p);
        book.Log(world, $"{Name(world, p)} back on the ice ({why})");
    }

    static void OutOfTheBox(Player p)
    {
        var door = BoxDoor(p.Side);
        p.OnIce = true;
        p.Position = door;
        p.Velocity = new Vector3(0, 0, -1.5f);
        p.Heading = MathF.PI;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    static void ToBox(World world, Player p)
    {
        HandOff(world, p);
        var door = BoxDoor(p.Side);
        p.OnIce = false;
        p.Position = door with { Z = RinkSpec.HalfWidth + 1.2f };
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
        if (world.Puck.Carrier == p.Id) world.Puck.Carrier = -1;
    }

    static void ToBench(World world, Player p)
    {
        HandOff(world, p);
        var door = Squad.Door(p.Side);
        p.OnIce = false;
        p.Position = new Vector3(door.X, 0, door.Y - 1.5f);
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
        if (world.Puck.Carrier == p.Id) world.Puck.Carrier = -1;
    }

    /// <summary>A human controlling a skater who leaves the ice takes the teammate nearest the puck.</summary>
    static void HandOff(World world, Player p)
    {
        if (p.Slot < 0) return;
        var to = world.OnIce(p.Side)
            .Where(o => o != p && !o.IsGoalie && o.Slot < 0)
            .MinBy(o => Vector3.DistanceSquared(o.Position, world.Puck.Position));
        if (to is { } mate) mate.Slot = p.Slot;
        p.Slot = -1;
    }
}
