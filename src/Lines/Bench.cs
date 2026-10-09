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
