using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;

namespace StickAndPuck.Lines;

/// <summary>
/// W13: what the bench knows, for HUD, AI and screens to read. <see cref="LinesSystem"/> keeps it
/// up to date in every world that runs the match flow.
/// </summary>
public static class Bench
{
    static readonly ConditionalWeakTable<World, LinesBoard> Boards = [];

    /// <summary>Gets a club's squad, or null in a world without the match flow.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The squad.</returns>
    public static Squad? SquadOf(World world, Side side)
    {
        return Boards.TryGetValue(world, out var b) ? b.Squads[(int)side] : null;
    }

    /// <summary>Gets whether a club has a change on the fly under way: skaters heading for the bench.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>True while skaters are leaving.</returns>
    public static bool IsChanging(World world, Side side)
    {
        return Boards.TryGetValue(world, out var b) && b.Changing[(int)side] != null;
    }

    /// <summary>Gets whether a skater is on the way to the bench.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>True while they leave.</returns>
    public static bool IsLeaving(World world, Player p)
    {
        return Boards.TryGetValue(world, out var b) && b.Changing[(int)p.Side] is { } c && c.Swaps.Exists(s => !s.Done && s.Out == p);
    }

    /// <summary>Gets the mean energy of a club's forwards or defencemen on the ice.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <param name="group">Forwards or defence.</param>
    /// <returns>Energy from 1 to 0; 1 when nobody of that group is on the ice.</returns>
    public static float OnIceEnergy(World world, Side side, Group group)
    {
        var on = OnIce(world, side, group).ToList();
        return on.Count == 0 ? 1f : on.Average(p => p.Energy);
    }

    /// <summary>Gets the unit most of a group on the ice belongs to.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <param name="group">Forwards or defence.</param>
    /// <returns>The unit, or null.</returns>
    public static Unit? OnIceUnit(World world, Side side, Group group)
    {
        var squad = SquadOf(world, side);
        return squad?.Units(group).MaxBy(u => u.Members.Count(m => m.OnIce));
    }

    /// <summary>
    /// Asks for an early change, as the change line button does: the more tired of forwards and
    /// defence goes off, on the fly in play, or at the next faceoff while play is stopped.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    public static void RequestChange(World world, Side side)
    {
        if (Boards.TryGetValue(world, out var b)) b.Requested[(int)side] = true;
    }

    /// <summary>
    /// Sends a skater on from the bench, W23's extra attacker or the skater a club adds for a 4 on 3
    /// power play in overtime: the freshest of the group who is not in the penalty box, from the
    /// other group when that one is empty. On the fly he steps out of the bench door; at a stoppage
    /// he waits at the door for the faceoff to place him.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <param name="group">The group wanted first.</param>
    /// <param name="onTheFly">True in play, false while play is stopped.</param>
    /// <returns>The skater, or null with nobody left on the bench.</returns>
    public static Player? SendOn(World world, Side side, Group group, bool onTheFly)
    {
        var on = world.Players
            .Where(p => p.Side == side && !p.OnIce && !p.IsGoalie && !Rules.Penalties.InBox(world, p.Id))
            .OrderByDescending(p => Squad.GroupOf(p) == group)
            .ThenByDescending(p => p.Energy)
            .FirstOrDefault();
        if (on == null) return null;
        var door = Squad.Door(side);
        on.OnIce = true;
        on.Position = new Vector3(door.X, 0, door.Y + (onTheFly ? 0.4f : 1.5f));
        on.Velocity = onTheFly ? new Vector3(0, 0, 1.5f) : Vector3.Zero;
        on.Heading = 0f;
        on.Command = default;
        on.Begin(PlayerAction.None);
        return on;
    }

    /// <summary>Sends a player on the ice to the bench, handing a human's control to the teammate nearest the puck.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The player.</param>
    public static void SendOff(World world, Player p)
    {
        if (!p.OnIce) return;
        if (p.Slot >= 0)
        {
            var to = world.OnIce(p.Side).Where(o => o != p && !o.IsGoalie && o.Slot < 0).MinBy(o => Vector3.DistanceSquared(o.Position, world.Puck.Position));
            if (to != null) to.Slot = p.Slot;
            p.Slot = -1;
        }
        var door = Squad.Door(p.Side);
        p.OnIce = false;
        p.Position = new Vector3(door.X, 0, door.Y - 1.5f);
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
        if (world.Puck.Carrier == p.Id) world.Puck.Carrier = -1;
        if (Boards.TryGetValue(world, out var b) && b.Changing[(int)p.Side] is { } change)
            foreach (var s in change.Swaps.Where(s => s.Out == p || s.In == p)) s.Done = true;
    }

    /// <summary>
    /// Gets the skater a club can spare when it has more on the ice than it should: the extra
    /// attacker (W23) if he is out there, else the most tired of the group with more than its
    /// share (one forward at three a side, two defence at four or more), a human's skater last.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <param name="target">The skaters the club should have.</param>
    /// <returns>The skater, or null with nobody on the ice.</returns>
    public static Player? Surplus(World world, Side side, int target)
    {
        if (GoaliePull.ExtraAttacker(world, side) is { } extra) return extra;
        var skaters = world.OnIce(side).Where(p => !p.IsGoalie).ToList();
        int defenceWanted = target >= 4 ? 2 : Math.Max(0, target - 1);
        int defence = skaters.Count(p => Squad.GroupOf(p) == Group.Defence);
        var group = defence > defenceWanted ? Group.Defence : Group.Forwards;
        return skaters.Where(p => Squad.GroupOf(p) == group).OrderBy(p => p.Slot >= 0).ThenBy(p => p.Energy).FirstOrDefault()
            ?? skaters.OrderBy(p => p.Slot >= 0).ThenBy(p => p.Energy).FirstOrDefault();
    }

    /// <summary>
    /// Takes an ejected player (W47) out of his club's squad for the rest of the match: off the ice
    /// if he is on it, out of his line or pair, and a skater from the last unit moves up into the gap
    /// (<see cref="Squad.Remove"/>). The penalty box keeps him off the bench (<see cref="Rules.Penalties.InBox"/>).
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The ejected player.</param>
    public static void Eject(World world, Player p)
    {
        SendOff(world, p);
        if (SquadOf(world, p.Side) is not { } squad) return;
        var unit = squad.UnitOf(p);
        var up = squad.Remove(p);
        if (unit != null && Rules.MatchFlow.State(world).Log)
            Console.WriteLine($"LINES: {world.Setup.Club(p.Side).ShortName} {unit.Name} refilled{(up == null ? " short" : $" with {up.Data.LastName}")}");
    }

    internal static IEnumerable<Player> OnIce(World world, Side side, Group group)
    {
        return world.OnIce(side).Where(p => !p.IsGoalie && Squad.GroupOf(p) == group);
    }

    internal static LinesBoard Board(World world)
    {
        return Boards.GetValue(world, _ => new LinesBoard());
    }
}

/// <summary>The bench's state for one match.</summary>
internal sealed class LinesBoard
{
    public Squad?[] Squads { get; } = new Squad?[2];

    /// <summary>Gets each club's change on the fly, if one is under way.</summary>
    public ShiftChange?[] Changing { get; } = new ShiftChange?[2];

    /// <summary>Gets the groups that change at the next faceoff, indexed by side then group.</summary>
    public bool[,] Pending { get; } = new bool[2, 2];

    /// <summary>Gets the clubs that send their first line and pair out at the next faceoff (a new period).</summary>
    public bool[] FirstUnits { get; } = new bool[2];

    /// <summary>Gets the clubs whose backup goalie comes in at the next faceoff.</summary>
    public bool[] GoalieSwap { get; } = new bool[2];

    /// <summary>Gets each club's pulled goalie (W23), or null while he is in the net.</summary>
    public Player?[] PulledGoalie { get; } = new Player?[2];

    /// <summary>Gets the id of the extra attacker who came on for each club's goalie, or -1.</summary>
    public int[] ExtraAttacker { get; } = [-1, -1];

    /// <summary>Gets whether each human slot held the pull goalie button last step.</summary>
    public bool[] PullHeld { get; } = new bool[MatchSetup.Slots];

    /// <summary>Gets the button presses not yet acted on, per side.</summary>
    public bool[] Requested { get; } = new bool[2];

    /// <summary>Gets the game seconds of recent goals against, per side.</summary>
    public List<float>[] GoalsAgainst { get; } = [[], []];

    public int[] LastScore { get; } = new int[2];

    /// <summary>Gets or sets the game seconds played: time in play, so the clock's time over all periods.</summary>
    public float GameTime { get; set; }

    public MatchPhase LastPhase { get; set; } = MatchPhase.Intro;

    /// <summary>Gets what each club last sent out and when, for the debug panel.</summary>
    public (string What, float At)[] Last { get; } = [("", float.NegativeInfinity), ("", float.NegativeInfinity)];
}

/// <summary>A change under way: who goes off, who comes on.</summary>
/// <param name="Side">The club.</param>
/// <param name="Group">Forwards or defence.</param>
/// <param name="Incoming">The unit coming on.</param>
/// <param name="Swaps">One skater off for one on.</param>
/// <param name="Started">World time the call was made.</param>
internal sealed record ShiftChange(Side Side, Group Group, Unit Incoming, List<Swap> Swaps, float Started)
{
    /// <summary>Gets or sets a value indicating whether the line change event went out.</summary>
    public bool Announced { get; set; }
}

/// <summary>One skater off for one on.</summary>
/// <param name="Out">Going to the bench.</param>
/// <param name="In">Coming on.</param>
internal sealed record Swap(Player Out, Player In)
{
    public bool Done { get; set; }

    /// <summary>Gets the spot (x, z) on the ice where the bench door is.</summary>
    public Vector2 Door => Squad.Door(Out.Side);
}
