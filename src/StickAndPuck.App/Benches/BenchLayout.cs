using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Benches;

/// <summary>Where everyone sits on a club's bench behind the far boards (arena.glb's Bench part):
/// one seat row 10 m long centred on the club's door. Forwards sit on the side toward centre ice,
/// line 1 nearest the door and line 4 at the end; the defence pairs sit on the other side of the
/// door, pair 1 nearest it, with the seat by the door kept for a pulled goalie and the far end for
/// the goalie who is not playing. Each unit keeps its seats, so a skater coming off goes back to
/// the free seat of his unit.</summary>
public static class BenchLayout
{
    /// <summary>Where a seated figure sits (z): at the back of the seat (13.05 to 13.5 m out), so his skates stay 0.45 m out behind the boards as the bench clips expect.</summary>
    public const float SeatZ = -13.55f;

    /// <summary>Where a standing figure stands (z): in front of the seats, clear of the boards.</summary>
    public const float StandZ = -13.25f;

    /// <summary>The space from the door to the first seat on either side (m).</summary>
    const float DoorGap = 0.7f;

    /// <summary>Half a bench (m), each side of the door, less the end wall.</summary>
    const float Reach = 4.82f;

    /// <summary>The space between standing figures waiting at the door (m).</summary>
    const float QueueGap = 0.42f;

    /// <summary>Gets the door's x on a club's bench.</summary>
    public static float DoorX(Side side) => Squad.Door(side).X;

    /// <summary>Gets the way (+1 or -1 in x) from the door to the forwards' end: toward centre ice.</summary>
    public static float ForwardsWay(Side side) => side == Side.Home ? 1f : -1f;

    /// <summary>Gets the x range of a club's bench.</summary>
    public static (float From, float To) Span(Side side) => (DoorX(side) - 5f, DoorX(side) + 5f);

    /// <summary>Gets the spot (x) where the k-th skater of a group waits at the door for a change, k = 0 at the door.</summary>
    public static float QueueX(Side side, Group group, int k)
    {
        float way = group == Group.Forwards ? ForwardsWay(side) : -ForwardsWay(side);
        return DoorX(side) + way * (0.1f + k * QueueGap);
    }

    /// <summary>Gets every seat of a club, by player id: the forwards' lines, then the defence pairs,
    /// then any skater outside a unit after them on his group's side; goalies off the ice take the
    /// far end of the defence side, and a pulled goalie the seat by the door.</summary>
    /// <param name="squad">The club's squad.</param>
    /// <param name="others">Skaters of the club outside the squad's units, in the order they sit.</param>
    /// <param name="pulled">The club's pulled goalie, if any.</param>
    public static Dictionary<int, float> Seats(Squad squad, IEnumerable<Player> others, Maybe<Player> pulled)
    {
        var seats = new Dictionary<int, float>();
        var side = squad.Side;
        var forwards = squad.Lines.SelectMany(u => u.Members).ToList();
        var defence = squad.Pairs.SelectMany(u => u.Members).ToList();
        foreach (var p in others)
        {
            if (p.IsGoalie || seats.ContainsKey(p.Id) || forwards.Contains(p) || defence.Contains(p)) continue;
            (Squad.GroupOf(p) == Group.Forwards ? forwards : defence).Add(p);
        }
        Row(seats, forwards, side, ForwardsWay(side), first: 0, count: Math.Max(12, forwards.Count));

        // the defence side: the door seat for a pulled goalie, the pairs, then the goalie's seat at the end
        var goalies = squad.Goalies.Where(g => !pulled.Is(p => p == g)).OrderBy(g => g.OnIce).ToList();   // the one on the bench at the very end
        int count = Math.Max(6, defence.Count) + 1 + Math.Max(1, goalies.Count);
        Row(seats, defence, side, -ForwardsWay(side), first: 1, count: count);
        if (pulled.TryGet(out var puller)) seats[puller.Id] = SeatX(side, -ForwardsWay(side), 0, count);
        for (int i = 0; i < goalies.Count; i++) seats[goalies[i].Id] = SeatX(side, -ForwardsWay(side), count - 1 - i, count);
        return seats;
    }

    static void Row(Dictionary<int, float> seats, List<Player> players, Side side, float way, int first, int count)
    {
        for (int i = 0; i < players.Count; i++) seats.TryAdd(players[i].Id, SeatX(side, way, first + i, count));
    }

    /// <summary>The i-th of count seats along one side of the door, 0 nearest it.</summary>
    static float SeatX(Side side, float way, int i, int count)
    {
        float pitch = (Reach - DoorGap) / Math.Max(1, count - 1);
        return DoorX(side) + way * (DoorGap + Math.Min(i, count - 1) * pitch);
    }
}
