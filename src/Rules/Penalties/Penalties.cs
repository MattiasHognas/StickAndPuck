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
/// regulation; none are called in the shootout.
/// W23: calls happen in overtime too. The strength each club plays at comes from the skaters
/// on the ice when the period started (<see cref="TargetSkaters"/>): at 5 on 5 the penalised
/// club loses a skater, at 3 on 3 it keeps three and the other club adds one (4 on 3, then 5 on 3).
/// A club that has pulled its goalie (<see cref="Lines.GoaliePull"/>) has one skater more.
/// W46, as <see cref="MatchSetup.Rules"/> says: penalties running at the end of regulation carry
/// into overtime (<see cref="RuleSet.PenaltiesCarryIntoOvertime"/>); a club already two short
/// takes a third penalty into the box, where it waits and starts when one of the two ends, the
/// skater whose time is up staying in the box until play stops (<see cref="RuleSet.StackPenalties"/>,
/// <see cref="Waiting"/>); and equal minors on both clubs at the same whistle are substituted,
/// leaving the strength as it was, or in the NHL played 4 on 4 when both clubs were at full
/// strength (<see cref="RuleSet.CoincidentalMinors"/>, <see cref="Substituted"/>).
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
        var book = Book(world);
        return world.Match.Penalties.Count(p => p.Side == side && !book.Substituted.Contains(p.PlayerId));
    }

    /// <summary>Gets the penalties waiting in the box for one of the club's two running penalties to end (W46).</summary>
    /// <param name="world">The match.</param>
    /// <returns>The waiting penalties, in the order they start; their clocks stand still.</returns>
    public static IReadOnlyList<PenaltyState> Waiting(World world)
    {
        return Book(world).Waiting;
    }

    /// <summary>Gets whether a running penalty is one of a coincidental pair that was substituted: it leaves the strength alone (W46).</summary>
    /// <param name="world">The match.</param>
    /// <param name="playerId">The penalised player.</param>
    /// <returns>True for a substituted coincidental minor.</returns>
    public static bool Substituted(World world, int playerId)
    {
        return Book(world).Substituted.Contains(playerId);
    }

    /// <summary>Gets the club on the power play: the one serving fewer penalties, or null at even strength.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The club with the extra skater.</returns>
    public static Side? PowerPlay(World world)
    {
        int home = Serving(world, Side.Home), away = Serving(world, Side.Away);
        return home < away ? Side.Home : away < home ? Side.Away : null;
    }

    /// <summary>Gets the skaters each club plays at full strength this period: 5 in regulation, 3 (or 5) in overtime, read off the ice when the period started.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Skaters a side, 3 to 5.</returns>
    public static int BaseSkaters(World world)
    {
        return Book(world).BaseSkaters;
    }

    /// <summary>
    /// Gets how many skaters a club should have on the ice now, its goalie aside: the short-handed
    /// club plays <see cref="BaseSkaters"/> less its penalties but never under three, the club with
    /// fewer penalties plays that count plus its advantage, never over five. An extra attacker for
    /// a pulled goalie adds one.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The skater count.</returns>
    public static int TargetSkaters(World world, Side side)
    {
        var other = side == Side.Home ? Side.Away : Side.Home;
        int mine = Serving(world, side), theirs = Serving(world, other), full = BaseSkaters(world);
        int skaters = mine < theirs
            ? Math.Min(PenaltyTuning.MaxSkaters, Math.Max(PenaltyTuning.MinSkaters, full - theirs) + theirs - mine)
            : Math.Max(PenaltyTuning.MinSkaters, full - mine);
        return skaters + (Lines.GoaliePull.IsPulled(world, side) ? 1 : 0);
    }

    /// <summary>Gets whether a player is in the penalty box.</summary>
    /// <param name="world">The match.</param>
    /// <param name="playerId">The player.</param>
    /// <returns>True while serving.</returns>
    public static bool InBox(World world, int playerId)
    {
        var book = Book(world);
        return world.Match.Penalties.Exists(p => p.PlayerId == playerId) || book.Waiting.Exists(p => p.PlayerId == playerId) || book.Held.Contains(playerId);
    }

    /// <summary>
    /// The referee sees an infraction: the arm goes up (<see cref="Referee.Referee.Signal"/>) and the call
    /// waits for the offending club to touch the puck. Waved off when penalties are off in the settings,
    /// outside play (the shootout included), for a goalie, for a skater already called, or when the club already serves two
    /// and the rules do not stack penalties (<see cref="RuleSet.StackPenalties"/>).
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
        if (m.Phase != MatchPhase.Play) return false;
        var p = world.Players[playerId];
        var book = Book(world);
        if (p.IsGoalie || InBox(world, playerId) || book.Delayed.Exists(d => d.PlayerId == playerId)) return false;
        if (!world.Setup.Rules.StackPenalties && Serving(world, p.Side) + book.Delayed.Count(d => d.Side == p.Side) >= PenaltyTuning.MaxServing) return false;
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

    /// <summary>
    /// Makes every delayed call: each skater to the box, a Penalty event per call, the referee's signal.
    /// Calls on both clubs at the same whistle pair up as coincidental minors (W46): substituted, so
    /// the strength stays, except one each from full strength under the NHL's 4 on 4.
    /// </summary>
    internal static void CallDelayed(World world, PenaltyBook book)
    {
        int pairs = Math.Min(book.Delayed.Count(d => d.Side == Side.Home), book.Delayed.Count(d => d.Side == Side.Away));
        bool fourOnFour = world.Setup.Rules.CoincidentalMinors == CoincidentalMinors.FourOnFour
            && pairs == 1 && book.Delayed.Count == 2 && Serving(world, Side.Home) == 0 && Serving(world, Side.Away) == 0;
        var paired = new HashSet<int>();
        if (!fourOnFour)
        {
            foreach (var side in new[] { Side.Home, Side.Away })
                foreach (var d in book.Delayed.Where(d => d.Side == side).Take(pairs)) paired.Add(d.PlayerId);
        }

        foreach (var d in book.Delayed) Assess(world, book, d, paired.Contains(d.PlayerId));
        book.Delayed.Clear();
    }

    /// <summary>
    /// Sends a skater to the box for a minor and publishes the Penalty event (Strength = seconds to serve).
    /// A substituted coincidental minor leaves the strength alone; a club already serving two puts the
    /// penalty on <see cref="Waiting"/> (W46).
    /// </summary>
    internal static void Assess(World world, PenaltyBook book, DelayedPenalty d, bool substitute = false)
    {
        var p = world.Players[d.PlayerId];
        float seconds = MinorSeconds(world);
        var penalty = new PenaltyState(p.Id, p.Side, d.Infraction, seconds);
        bool waits = !substitute && Serving(world, p.Side) >= PenaltyTuning.MaxServing;
        if (waits) book.Waiting.Add(penalty);
        else world.Match.Penalties.Add(penalty);
        if (substitute) book.Substituted.Add(p.Id);
        // a skater who changed off during the delay still goes in; a teammate of his group leaves the ice so the club still plays short
        if (!p.OnIce && !waits && !substitute && world.OnIce(p.Side).Where(o => !o.IsGoalie && Squad.GroupOf(o) == Squad.GroupOf(p)).MinBy(o => o.Position.Z) is { } stand)
            ToBench(world, stand);
        ToBox(world, p);
        book.Called[(int)p.Side]++;
        world.Events.Publish(new MatchEvent(MatchEventKind.Penalty, world.Time, p.Side, p.Id, p.Position, seconds));
        Referee.Referee.Announce(world, d.Infraction, p.Id);
        book.Log(world, $"{d.Infraction}, {Name(world, p)}, {MatchFlow.ClockText(seconds)}{(substitute ? ", coincidental, substituted" : waits ? ", waits in the box" : "")}");
    }

    /// <summary>
    /// Ends a running penalty, served or ended by a goal (W46): a substituted skater goes to the bench,
    /// the club's next waiting penalty starts with the skater whose time is up kept in the box until
    /// play stops, and otherwise he comes out of the box door onto the ice.
    /// </summary>
    internal static void End(World world, PenaltyBook book, PenaltyState penalty, string why)
    {
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
            book.Held.Add(penalty.PlayerId);
            book.Log(world, $"{Name(world, world.Players[next.PlayerId])}'s penalty starts, {Name(world, world.Players[penalty.PlayerId])} waits for the whistle ({why})");
            return;
        }

        Release(world, book, penalty, why);
    }

    /// <summary>Play has stopped: skaters kept in the box after their time was up go to the bench, free to come on (W46).</summary>
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
        var ids = world.Match.Penalties.Select(p => p.PlayerId).Concat(book.Waiting.Select(p => p.PlayerId)).Concat(book.Held).ToList();
        foreach (int id in ids.Where(id => !world.Players[id].OnIce)) ToBench(world, world.Players[id]);
        world.Match.Penalties.Clear();
        book.Waiting.Clear();
        book.Held.Clear();
        book.Substituted.Clear();
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

    /// <summary>Gets the penalties waiting for one of their club's two running penalties to end (W46).</summary>
    public List<PenaltyState> Waiting { get; } = [];

    /// <summary>Gets the skaters whose time is up but who stay in the box until play stops, because a waiting penalty took their place (W46).</summary>
    public HashSet<int> Held { get; } = [];

    /// <summary>Gets the skaters serving a substituted coincidental minor (W46).</summary>
    public HashSet<int> Substituted { get; } = [];

    /// <summary>Gets the penalties called per club this match.</summary>
    public int[] Called { get; } = new int[2];

    /// <summary>Gets or sets the game clock at the last step, to run penalty clocks by exactly what the game clock ran.</summary>
    public float LastClock { get; set; } = float.NaN;

    public int LastPeriod { get; set; }

    /// <summary>Gets or sets the skaters a side at full strength this period (W23), read at the first faceoff of a period with nobody in the box.</summary>
    public int BaseSkaters { get; set; } = 5;

    /// <summary>Gets or sets the period whose first faceoff set <see cref="BaseSkaters"/>.</summary>
    public int BasePeriod { get; set; }

    public MatchPhase LastPhase { get; set; } = MatchPhase.Intro;

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
