using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines;

/// <summary>Which half of the skaters a unit belongs to; forwards and defence change separately.</summary>
public enum Group
{
    /// <summary>Centres and wings.</summary>
    Forwards,

    /// <summary>Defencemen.</summary>
    Defence,
}

/// <summary>A forward line (left wing, centre, right wing) or a defence pair.</summary>
/// <param name="Group">Forwards or defence.</param>
/// <param name="Number">1 for the first line or pair.</param>
/// <param name="Members">The players, in the order they fill spots: C, LW, RW for a line; left, right for a pair.</param>
public sealed record Unit(Group Group, int Number, IReadOnlyList<Player> Members)
{
    /// <summary>Gets the unit's name as a bench boss says it: L2 or P1.</summary>
    public string Name => $"{(Group == Group.Forwards ? "L" : "P")}{Number}";

    /// <summary>Gets the members' mean energy.</summary>
    public float Energy => Members.Count == 0 ? 0 : Members.Average(p => p.Energy);
}

/// <summary>One club's match squad: four forward lines, three defence pairs and two goalies, all of them in <see cref="World.Players"/>.</summary>
public sealed class Squad
{
    readonly List<Unit> lines;
    readonly List<Unit> pairs;

    Squad(Side side, List<Unit> lines, List<Unit> pairs, List<Player> goalies)
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
    /// <param name="group">Forwards or defence.</param>
    /// <returns>The lines or the pairs.</returns>
    public IReadOnlyList<Unit> Units(Group group)
    {
        return group == Group.Forwards ? Lines : Pairs;
    }

    /// <summary>Gets the group a skater plays in.</summary>
    /// <param name="p">The skater.</param>
    /// <returns>Defence for a D, forwards otherwise.</returns>
    public static Group GroupOf(Player p)
    {
        return p.Data.Position == PlayingPosition.D ? Group.Defence : Group.Forwards;
    }

    /// <summary>Gets the bench door of a club: on the far boards, home left of centre and away right of it.</summary>
    /// <param name="side">The club.</param>
    /// <returns>The spot on the ice (x, z) where skaters leave and come on.</returns>
    public static Vector2 Door(Side side)
    {
        return new Vector2(side == Side.Home ? -6f : 6f, -(RinkSpec.HalfWidth - 0.9f));
    }

    /// <summary>
    /// Builds a club's squad from its roster, adding everyone not yet in the world to the bench.
    /// Players already on the ice (the starters) keep their place. Lines take centres, left and
    /// right wings in roster order, pairs take defencemen two by two; gaps are filled from the
    /// forwards or defencemen left over.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The squad.</returns>
    public static Squad Build(World world, Side side)
    {
        var door = Door(side);
        var players = new List<Player>();
        foreach (var data in world.Setup.Club(side).Roster)
        {
            if (world.Players.Find(o => o.Side == side && ReferenceEquals(o.Data, data)) is not { } p)
            {
                p = world.AddPlayer(side, data, new Vector3(door.X, 0, door.Y - 1.5f));
                p.OnIce = false;
            }
            players.Add(p);
        }

        var centres = Queue(players, PlayingPosition.C);
        var lefts = Queue(players, PlayingPosition.LW);
        var rights = Queue(players, PlayingPosition.RW);
        var defence = Queue(players, PlayingPosition.D);
        var lines = new List<Unit>();
        for (int i = 0; i < 4; i++)
        {
            var members = new List<Player>();
            foreach (var q in new[] { centres, lefts, rights })
            {
                var from = q.Count > 0 ? q : Spare(centres, lefts, rights);
                if (from.TryGet(out var queue)) members.Add(queue.Dequeue());
            }
            if (members.Count > 0) lines.Add(new Unit(Group.Forwards, i + 1, members));
        }
        var pairs = new List<Unit>();
        for (int i = 0; i < 3 && defence.Count > 0; i++)
        {
            var members = new List<Player> { defence.Dequeue() };
            if (defence.Count > 0) members.Add(defence.Dequeue());
            if (members.Count == 2 && members[0].Data.Shoots == Hand.Right && members[1].Data.Shoots == Hand.Left) members.Reverse();
            pairs.Add(new Unit(Group.Defence, i + 1, members));
        }
        var goalies = players.Where(p => p.IsGoalie).OrderByDescending(p => p.OnIce).ToList();
        return new Squad(side, lines, pairs, goalies);
    }

    /// <summary>Gets the unit a player belongs to, if any.</summary>
    /// <param name="p">The player.</param>
    /// <returns>The unit, or None for a goalie or an extra.</returns>
    public Maybe<Unit> UnitOf(Player p)
    {
        return Lines.Concat(Pairs).FirstOrNone(u => u.Members.Contains(p));
    }

    /// <summary>
    /// Takes a player out of the squad for good, as an ejection does (W47), and refills his unit:
    /// the last unit of his group gives up a skater, of the same position if it has one, so the top
    /// lines and pairs stay whole and the last one plays short (a change fills it from the bench).
    /// A unit left with nobody is dropped.
    /// </summary>
    /// <param name="p">The player.</param>
    /// <returns>The skater who moved up into the gap, or None when nobody did.</returns>
    public Maybe<Player> Remove(Player p)
    {
        if (!UnitOf(p).TryGet(out var unit)) return Maybe<Player>.None;
        var units = unit.Group == Group.Forwards ? lines : pairs;
        int at = units.IndexOf(unit);
        var members = unit.Members.ToList();
        int slot = members.IndexOf(p);
        var last = units[^1];
        var donor = Maybe<Player>.None;
        if (last != unit && last.Members.Count > 0)
        {
            var up = last.Members.FirstOrDefault(m => m.Data.Position == p.Data.Position) ?? last.Members[^1];
            donor = up;
            members[slot] = up;
            units[^1] = last with { Members = [.. last.Members.Where(m => m != up)] };
        }
        else
        {
            members.RemoveAt(slot);
        }
        units[at] = unit with { Members = members };
        units.RemoveAll(u => u.Members.Count == 0);
        return donor;
    }

    static Queue<Player> Queue(List<Player> players, PlayingPosition position)
    {
        // whoever is already on the ice leads, so the starters make up line 1 and pair 1
        return new Queue<Player>(players.Where(p => p.Data.Position == position).OrderByDescending(p => p.OnIce));
    }

    static Maybe<Queue<Player>> Spare(params Queue<Player>[] queues)
    {
        return Maybe.From<Queue<Player>>(queues.Where(q => q.Count > 0).MaxBy(q => q.Count));
    }
}
