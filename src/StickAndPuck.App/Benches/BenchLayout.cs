using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Benches;

/// <summary>Where everyone on a club's bench sits behind the far boards (arena.glb's Bench part):
/// one seat row 10 m long centred on the club's door. Only the players on the bench get a seat,
/// spread evenly out from the door on their group's side, so the row closes up when a unit goes
/// on and opens when it comes off. Forwards sit on the side toward centre ice and the defence on
/// the other, in the order their units go on: the unit after the one on the ice nearest the door,
/// then the following ones. A pulled goalie sits by the door on the defence side and the goalie
/// who is not playing at the end of it.</summary>
public static class BenchLayout
{
    /// <summary>Where a seated figure sits (z): at the back of the seat (13.05 to 13.5 m out), so his skates stay 0.45 m out behind the boards as the bench clips expect.</summary>
    public const float SeatZ = -13.55f;

    /// <summary>Where a standing figure stands (z): in front of the seats, clear of the boards.</summary>
    public const float StandZ = -13.25f;

    /// <summary>Where a figure sits in a penalty box (z): at the back of its seat (13.75 to 14.25 m out), mirroring <see cref="SeatZ"/>.</summary>
    public const float BoxSeatZ = 14.3f;

    /// <summary>Where a figure stands in a penalty box (z): in front of the seat.</summary>
    public const float BoxStandZ = 13.4f;

    /// <summary>The seats of a penalty box out from its door (m), in the order they fill: two out from the door away from centre, where the box has room, then two toward the timekeeper.</summary>
    static readonly float[] BoxSeats = [0.45f, 1.2f, -0.45f, -1.2f];

    /// <summary>The space from the door to the first seat on either side (m).</summary>
    public const float DoorGap = 0.7f;

    /// <summary>Half a bench (m), each side of the door, less the end wall.</summary>
    const float Reach = 4.82f;

    /// <summary>The widest space between seated figures (m), so a short row stays by the door.</summary>
    public const float MaxPitch = 0.75f;

    /// <summary>The space between standing figures waiting at the door (m).</summary>
    const float QueueGap = 0.42f;

    /// <summary>Gets the door's x on a club's bench.</summary>
    public static float DoorX(Side side) => Squad.Door(side).X;

    /// <summary>Gets the door's x on a club's penalty box.</summary>
    public static float BoxDoorX(Side side) => Rules.Penalties.BoxDoor(side).X;

    /// <summary>Gets the x of the k-th seat in a club's penalty box, k = 0 the first one filled.</summary>
    public static float BoxSeatX(Side side, int k) => BoxDoorX(side) + MathF.Sign(BoxDoorX(side)) * BoxSeats[Math.Min(k, BoxSeats.Length - 1)];

    /// <summary>Gets the x range of a club's penalty box.</summary>
    public static (float From, float To) BoxSpan(Side side) => (BoxDoorX(side) - 1.9f, BoxDoorX(side) + 1.9f);

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

    /// <summary>Gets whether a player of a club is on its bench: off the ice and not in the penalty box.</summary>
    public static bool OnBench(World world, Player p) => !p.OnIce && !Rules.Penalties.InBox(world, p.Id);

    /// <summary>Gets the seat (x) of every player on a club's bench, by player id: the forwards'
    /// lines on their side and the defence pairs on the other, each side in the order the units go
    /// on, with any skater outside a unit after them on his group's side; a pulled goalie takes the
    /// seat by the door and the goalie who is not playing the end of the defence side.</summary>
    /// <param name="squad">The club's squad.</param>
    /// <param name="others">Skaters of the club outside the squad's units, in the order they sit.</param>
    /// <param name="pulled">The club's pulled goalie, if any.</param>
    /// <param name="onBench">Whether a player is on the bench (see <see cref="OnBench"/>); nobody else gets a seat.</param>
    public static Dictionary<int, float> Seats(Squad squad, IEnumerable<Player> others, Maybe<Player> pulled, Func<Player, bool> onBench)
    {
        var side = squad.Side;
        var forwards = InTurn(squad.Lines);
        var defence = InTurn(squad.Pairs);
        foreach (var p in others)
        {
            if (p.IsGoalie || forwards.Contains(p) || defence.Contains(p)) continue;
            (Squad.GroupOf(p) == Group.Forwards ? forwards : defence).Add(p);
        }
        if (pulled.TryGet(out var puller)) defence.Insert(0, puller);
        defence.AddRange(squad.Goalies.Where(g => !pulled.Is(p => p == g)));

        var seats = new Dictionary<int, float>();
        Row(seats, forwards.Where(onBench).ToList(), side, ForwardsWay(side));
        Row(seats, defence.Where(onBench).ToList(), side, -ForwardsWay(side));
        return seats;
    }

    /// <summary>The members of a group's units in the order they go on: from the unit after the
    /// first one with a skater on the ice, round to it.</summary>
    static List<Player> InTurn(IReadOnlyList<Unit> units)
    {
        int on = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].Members.Any(p => p.OnIce))
            {
                on = i + 1;
                break;
            }
        }
        return [.. Enumerable.Range(0, units.Count).SelectMany(i => units[(on + i) % units.Count].Members)];
    }

    static void Row(Dictionary<int, float> seats, List<Player> players, Side side, float way)
    {
        float pitch = players.Count > 1 ? MathF.Min(MaxPitch, (Reach - DoorGap) / (players.Count - 1)) : 0f;
        for (int i = 0; i < players.Count; i++) seats.TryAdd(players[i].Id, DoorX(side) + way * (DoorGap + i * pitch));
    }
}
