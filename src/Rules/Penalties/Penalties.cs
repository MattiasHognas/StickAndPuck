using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.Core;
using StickAndPuck.Lines;

namespace StickAndPuck.Rules;

/// <summary>The three infractions the referee calls.</summary>
public static class Infraction
{
    /// <summary>Taking a skater's legs out.</summary>
    public const string Tripping = "tripping";

    /// <summary>Holding a carrier back with the stick.</summary>
    public const string Hooking = "hooking";

    /// <summary>Swinging the stick at a skater.</summary>
    public const string Slashing = "slashing";
}

/// <summary>A penalty the referee has signalled and not yet called: play goes on until the offending club touches the puck.</summary>
/// <param name="Side">The offending club.</param>
/// <param name="PlayerId">The offending skater.</param>
/// <param name="Infraction">What was seen.</param>
/// <param name="Time">When the arm went up, in <see cref="World.Time"/>.</param>
public readonly record struct DelayedPenalty(Side Side, int PlayerId, string Infraction, float Time);

/// <summary>
/// W14: the penalty calls other systems and scenes use. <see cref="PenaltyRules"/> runs them each step.
/// A call is delayed (<see cref="Signal"/>): the referee's arm goes up and play goes on until the
/// offending club touches the puck, which blows the whistle, or play stops by itself. Then the
/// skater goes to the box (<see cref="Player.OnIce"/> false) for a two minute minor
/// (<see cref="MinorSeconds"/>), listed in <see cref="MatchState.Penalties"/>, and his club plays a
/// skater short. The clock of a penalty runs only while the game clock runs. A goal by the club
/// with more skaters ends the shortest penalty of the other club; a goal against the offending club
/// while a call is delayed cancels the call. Penalties carry over between periods and end with
/// regulation (no calls in 3 on 3 overtime or the shootout).
/// </summary>
public static class Penalties
{
    static readonly ConditionalWeakTable<World, PenaltyBook> Books = new();

    /// <summary>Gets or sets how strict the referee is: 1 is normal, 0 calls nothing. Scenes raise it to see calls often.</summary>
    public static float Strictness { get; set; } = 1f;

    /// <summary>Gets the calls signalled and not yet made.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The delayed calls, oldest first.</returns>
    public static IReadOnlyList<DelayedPenalty> Delayed(World world)
    {
        return Book(world).Delayed;
    }

    /// <summary>Gets the length of a minor in this match: two minutes, shortened in proportion to shorter periods.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Game seconds.</returns>
    public static float MinorSeconds(World world)
    {
        return PenaltyTuning.MinorSeconds * world.Setup.PeriodSeconds / PenaltyTuning.FullPeriodSeconds;
    }

    /// <summary>Gets how many penalties a club is serving.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The count.</returns>
    public static int Serving(World world, Side side)
    {
        return world.Match.Penalties.Count(p => p.Side == side);
    }

    /// <summary>Gets the club on the power play: the one serving fewer penalties, or null at even strength.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The club with the extra skater.</returns>
    public static Side? PowerPlay(World world)
    {
        int home = Serving(world, Side.Home), away = Serving(world, Side.Away);
        return home < away ? Side.Home : away < home ? Side.Away : null;
    }

    /// <summary>Gets whether a player is in the penalty box.</summary>
    /// <param name="world">The match.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>True while serving.</returns>
    public static bool InBox(World world, int playerId)
    {
        return world.Match.Penalties.Exists(p => p.PlayerId == playerId);
    }

    /// <summary>
    /// The referee sees an infraction: the arm goes up (<see cref="Referee.Referee.Signal"/>) and the call
    /// waits for the offending club to touch the puck. Waved off when penalties are off in the settings,
    /// outside regulation, for a goalie, for a skater already called, or when it would put the club three short.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="playerId">The offending skater.</param>
    /// <param name="infraction">One of <see cref="Infraction"/>.</param>
    /// <returns>True if the call is now delayed.</returns>
    public static bool Signal(World world, int playerId, string infraction)
    {
        if (!world.Settings.Penalties || !MatchFlow.IsRunning(world)) return false;
        if (playerId < 0 || playerId >= world.Players.Count) return false;
        var m = world.Match;
        if (m.Period > world.Setup.Periods || m.Phase != MatchPhase.Play) return false;
        var p = world.Players[playerId];
        var book = Book(world);
        if (p.IsGoalie || InBox(world, playerId) || book.Delayed.Exists(d => d.PlayerId == playerId)) return false;
        if (Serving(world, p.Side) + book.Delayed.Count(d => d.Side == p.Side) >= PenaltyTuning.MaxServing) return false;
        book.Delayed.Add(new DelayedPenalty(p.Side, playerId, infraction, world.Time));
        Referee.Referee.Signal(world, infraction, playerId);
        book.Log(world, $"{infraction} on {Name(world, p)}, delayed");
        return true;
    }

    /// <summary>Gets the end-zone dot of the club that defends that end, on the puck's side: where play restarts after a call.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The penalised club.</param>
    /// <returns>The dot.</returns>
    public static Vector3 FaceoffDot(World world, Side side)
    {
        int end = FaceoffFormation.DefendedEnd(world.Match, side);
        float z = world.Puck.Position.Z < 0 ? -6.7f : 6.7f;
        return new Vector3(end * 20.7f, 0, z);
    }

    /// <summary>Gets the spot on the ice by a club's penalty box door, on the near boards opposite the benches.</summary>
    /// <param name="side">The club.</param>
    /// <returns>The spot.</returns>
    public static Vector3 BoxDoor(Side side)
    {
        return new Vector3(side == Side.Home ? -PenaltyTuning.BoxDoorX : PenaltyTuning.BoxDoorX, 0, RinkSpec.HalfWidth - PenaltyTuning.BoxDoorInset);
    }

    /// <summary>Makes every delayed call: each skater to the box, a Penalty event per call, the referee's signal.</summary>
    internal static void CallDelayed(World world, PenaltyBook book)
    {
        foreach (var d in book.Delayed) Assess(world, book, d);
        book.Delayed.Clear();
    }

    /// <summary>Sends a skater to the box for a minor and publishes the Penalty event (Strength = seconds to serve).</summary>
    internal static void Assess(World world, PenaltyBook book, DelayedPenalty d)
    {
        var p = world.Players[d.PlayerId];
        float seconds = MinorSeconds(world);
        world.Match.Penalties.Add(new PenaltyState(p.Id, p.Side, d.Infraction, seconds));
        // a skater who changed off during the delay still goes in; a teammate of his group leaves the ice so the club still plays short
        if (!p.OnIce && world.OnIce(p.Side).Where(o => !o.IsGoalie && Squad.GroupOf(o) == Squad.GroupOf(p)).MinBy(o => o.Position.Z) is { } stand)
            ToBench(world, stand);
        ToBox(world, p);
        book.Called[(int)p.Side]++;
        world.Events.Publish(new MatchEvent(MatchEventKind.Penalty, world.Time, p.Side, p.Id, p.Position, seconds));
        Referee.Referee.Announce(world, d.Infraction, p.Id);
        book.Log(world, $"{d.Infraction}, {Name(world, p)}, {MatchFlow.ClockText(seconds)}");
    }

    /// <summary>Ends a penalty: the skater comes out of the box door onto the ice.</summary>
    internal static void Release(World world, PenaltyBook book, PenaltyState penalty, string why)
    {
        world.Match.Penalties.Remove(penalty);
        var p = world.Players[penalty.PlayerId];
        var door = BoxDoor(p.Side);
        p.OnIce = true;
        p.Position = door;
        p.Velocity = new Vector3(0, 0, -1.5f);
        p.Heading = MathF.PI;
        p.Command = default;
        p.Begin(PlayerAction.None);
        book.Log(world, $"{Name(world, p)} back on the ice ({why})");
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
        if (to != null) to.Slot = p.Slot;
        p.Slot = -1;
    }

    internal static PenaltyBook Book(World world)
    {
        return Books.GetValue(world, _ => new PenaltyBook());
    }

    internal static string Name(World world, Player p)
    {
        return $"{world.Setup.Club(p.Side).ShortName} #{p.Data.Number} {p.Data.LastName}";
    }
}

/// <summary>The penalty state of one match beside <see cref="MatchState.Penalties"/>.</summary>
internal sealed class PenaltyBook
{
    public List<DelayedPenalty> Delayed { get; } = [];

    /// <summary>Gets the penalties called per club this match.</summary>
    public int[] Called { get; } = new int[2];

    /// <summary>Gets or sets the game clock at the last step, to run penalty clocks by exactly what the game clock ran.</summary>
    public float LastClock { get; set; } = float.NaN;

    public int LastPeriod { get; set; }

    /// <summary>Gets or sets the dice; scenes and tests seed it.</summary>
#pragma warning disable S2245 // gameplay dice, not security
    public Random Rng { get; set; } = new(14);
#pragma warning restore S2245

    /// <summary>Gets or sets a scripted call for the penalty scene: seconds after the first drop, or NaN.</summary>
    public float ScriptAfterDrop { get; set; } = float.NaN;

    public float FirstDrop { get; set; } = float.NaN;

    public void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine($"PENALTY: {what}; P{m.Period} {MatchFlow.ClockText(m.Clock)}");
    }
}
