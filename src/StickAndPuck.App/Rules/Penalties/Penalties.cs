using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Rules;

/// <summary>The infractions the referee calls: tripping, hooking and slashing always, six more with a switch each, and checking from behind.</summary>
public static class Infraction
{
    /// <summary>Taking a skater's legs out.</summary>
    public const string Tripping = "tripping";

    /// <summary>Holding a carrier back with the stick.</summary>
    public const string Hooking = "hooking";

    /// <summary>Swinging the stick at a skater.</summary>
    public const string Slashing = "slashing";

    /// <summary>Hitting a skater who is nowhere near the puck.</summary>
    public const string Interference = "interference";

    /// <summary>Hanging on to a slowed carrier from behind.</summary>
    public const string Holding = "holding";

    /// <summary>The stick up at the face on a check; a double minor when it draws blood.</summary>
    public const string HighSticking = "high-sticking";

    /// <summary>A hard hit with both hands on the stick from the front; a major when very hard.</summary>
    public const string CrossChecking = "cross-checking";

    /// <summary>A hard hit from behind into the boards; a major when very hard.</summary>
    public const string Boarding = "boarding";

    /// <summary>A hit after the whistle, or the same skater hit again at once.</summary>
    public const string Roughing = "roughing";

    /// <summary>A hard hit from behind in open ice, a minor and misconduct or a major; called only with <see cref="PenaltySeverity.Full"/>.</summary>
    public const string CheckingFromBehind = "checking from behind";

    /// <summary>Gets an infraction's name as the penalty banner shows it, in the language shown.</summary>
    public static string Label(string infraction) => Text.Get(infraction switch
    {
        Tripping => K.InfTripping,
        Hooking => K.InfHooking,
        Slashing => K.InfSlashing,
        Interference => K.InfInterference,
        Holding => K.InfHolding,
        HighSticking => K.InfHighSticking,
        CrossChecking => K.InfCrossChecking,
        Boarding => K.InfBoarding,
        Roughing => K.InfRoughing,
        CheckingFromBehind => K.InfCheckingFromBehind,
        Fights.FightTuning.Infraction => K.InfFighting,
        _ => K.InfPenalty,
    });

    /// <summary>Gets whether the rules let the referee call an infraction: six each have a switch, the rest are always called.</summary>
    public static bool Called(RuleSet rules, string infraction) => infraction switch
    {
        Interference => rules.Interference,
        Holding => rules.Holding,
        HighSticking => rules.HighSticking,
        CrossChecking => rules.CrossChecking,
        Boarding => rules.Boarding,
        Roughing => rules.Roughing,
        CheckingFromBehind => rules.PenaltySeverity == PenaltySeverity.Full,
        _ => true,
    };
}

/// <summary>How much a call costs: what the club serves and what the offender sits out.</summary>
public enum Severity
{
    /// <summary>Two minutes; a power play goal ends it.</summary>
    Minor,

    /// <summary>Two and two; a power play goal ends the half that is running.</summary>
    DoubleMinor,

    /// <summary>Five minutes served in full, a goal or not; the offender comes back after.</summary>
    Major,

    /// <summary>Five minutes served in full by a teammate; the offender is ejected and leaves the squad.</summary>
    MajorAndGameMisconduct,

    /// <summary>Two minutes for the club, served by a teammate; the offender sits ten more, the club not short.</summary>
    MinorAndMisconduct,

    /// <summary>Ten minutes for the offender alone; the club plays at full strength.</summary>
    Misconduct,

    /// <summary>The offender is ejected; the club plays at full strength.</summary>
    GameMisconduct,
}

/// <summary>A penalty the referee has signalled and not yet called: play goes on until the offending club touches the puck.</summary>
/// <param name="Side">The offending club.</param>
/// <param name="PlayerId">The offending skater.</param>
/// <param name="Infraction">What was seen.</param>
/// <param name="Time">When the arm went up, in <see cref="World.Time"/>.</param>
/// <param name="Severity">What the call costs; a minor unless the referee saw worse.</param>
public readonly record struct DelayedPenalty(Side Side, int PlayerId, string Infraction, float Time, Severity Severity = Severity.Minor);

/// <summary>The penalty calls other systems and scenes use. <see cref="PenaltyRules"/> runs them each step.
/// A call is delayed (<see cref="Signal"/>): the referee's arm goes up and play goes on until the
/// offending club touches the puck, which blows the whistle, or play stops by itself. Then the
/// skater goes to the box (<see cref="Player.OnIce"/> false) for a two minute minor
/// (<see cref="MinorSeconds"/>), listed in <see cref="MatchState.Penalties"/>, and his club plays a
/// skater short. The clock of a penalty runs only while the game clock runs. A goal by the club
/// with more skaters ends the shortest penalty of the other club; a goal against the offending club
/// while a call is delayed cancels the call. Penalties carry over between periods and end with
/// regulation; none are called in the shootout.
/// Calls happen in overtime too. The strength each club plays at comes from the skaters
/// on the ice when the period started (<see cref="TargetSkaters"/>): at 5 on 5 the penalised
/// club loses a skater, at 3 on 3 it keeps three and the other club adds one (4 on 3, then 5 on 3).
/// A club that has pulled its goalie (<see cref="Lines.GoaliePull"/>) has one skater more.
/// As <see cref="MatchSetup.Rules"/> says: penalties running at the end of regulation carry
/// into overtime (<see cref="RuleSet.PenaltiesCarryIntoOvertime"/>); a club already two short
/// takes a third penalty into the box, where it waits and starts when one of the two ends, the
/// skater whose time is up staying in the box until play stops (<see cref="RuleSet.StackPenalties"/>,
/// <see cref="Waiting"/>); and equal minors on both clubs at the same whistle are substituted,
/// leaving the strength as it was, or in the NHL played 4 on 4 when both clubs were at full
/// strength (<see cref="RuleSet.CoincidentalMinors"/>, <see cref="Substituted"/>).
/// With <see cref="RuleSet.PenaltySeverity"/> Full: a call can cost more than a minor
/// (<see cref="Severity"/>). A double minor is two minors back to back, a power play goal ending
/// the half that runs; a major is five minutes no goal ends; a misconduct keeps the offender in the
/// box ten minutes while his club plays at full strength (<see cref="Misconducts"/>); a game
/// misconduct ejects him (<see cref="Ejected"/>): he leaves the squad, the lines refill without him
/// (<see cref="Lines.Bench.Eject"/>) and a teammate comes out of the box when the club's time is up.
/// With MinorsOnly every referee's call is a minor. <see cref="Give"/> hands out any severity at
/// once, for fights.</summary>
public static class Penalties
{


    /// <summary>Gets the calls signalled and not yet made.</summary>
    public static IReadOnlyList<DelayedPenalty> Delayed(World world)
    {
        return Book(world).Delayed;
    }

    /// <summary>Gets the length of a minor in this match: two minutes, shortened in proportion to shorter periods.</summary>
    public static float MinorSeconds(World world)
    {
        return PenaltyTuning.MinorSeconds * world.Setup.PeriodSeconds / PenaltyTuning.FullPeriodSeconds;
    }

    /// <summary>Gets how many penalties a club is serving.</summary>
    public static int Serving(World world, Side side)
    {
        var book = Book(world);
        return world.Match.Penalties.Count(p => p.Side == side && !book.Substituted.Contains(p.PlayerId));
    }

    /// <summary>Gets the penalties waiting in the box for one of the club's two running penalties to end.</summary>
    /// <returns>The waiting penalties, in the order they start; their clocks stand still.</returns>
    public static IReadOnlyList<PenaltyState> Waiting(World world)
    {
        return Book(world).Waiting;
    }

    /// <summary>Gets whether a running penalty is one of a coincidental pair that was substituted: it leaves the strength alone.</summary>
    public static bool Substituted(World world, int playerId)
    {
        return Book(world).Substituted.Contains(playerId);
    }

    /// <summary>Gets the club on the power play: the one serving fewer penalties, or None at even strength.</summary>
    public static SideOrNone PowerPlay(World world)
    {
        int home = Serving(world, Side.Home), away = Serving(world, Side.Away);
        return home < away ? SideOrNone.Home : away < home ? SideOrNone.Away : SideOrNone.None;
    }

    /// <summary>Gets the skaters each club plays at full strength this period: 5 in regulation, 3 (or 5) in overtime, read off the ice when the period started.</summary>
    public static int BaseSkaters(World world)
    {
        return Book(world).BaseSkaters;
    }

    /// <summary>Gets how many skaters a club should have on the ice now, its goalie aside: the short-handed
    /// club plays <see cref="BaseSkaters"/> less its penalties but never under three, the club with
    /// fewer penalties plays that count plus its advantage, never over five. An extra attacker for
    /// a pulled goalie adds one.</summary>
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
    public static bool InBox(World world, int playerId)
    {
        var book = Book(world);
        return world.Match.Penalties.Exists(p => p.PlayerId == playerId) || book.Waiting.Exists(p => p.PlayerId == playerId) || book.Held.Contains(playerId)
            || book.Misconducts.Exists(p => p.PlayerId == playerId) || book.Ejected.Contains(playerId);
    }

    /// <summary>Gets the misconducts running: the offender sits them out, his club at full strength.</summary>
    /// <returns>Each misconduct with the time the offender has left in the box, minor included.</returns>
    public static IReadOnlyList<PenaltyState> Misconducts(World world)
    {
        return Book(world).Misconducts;
    }

    /// <summary>Gets whether a player was thrown out of the match by a game misconduct.</summary>
    public static bool Ejected(World world, int playerId)
    {
        return Book(world).Ejected.Contains(playerId);
    }

    /// <summary>Gets what a running or waiting penalty costs.</summary>
    public static Severity SeverityOf(World world, int playerId)
    {
        return Book(world).Lengths.TryGetValue(playerId, out var severity) ? severity : Severity.Minor;
    }

    /// <summary>Gets the time a club serves for a call: a minor, two of them, a major, or none for a misconduct alone.</summary>
    public static float ClubSeconds(World world, Severity severity)
    {
        float minor = MinorSeconds(world);
        return severity switch
        {
            Severity.Minor or Severity.MinorAndMisconduct => minor,
            Severity.DoubleMinor => 2 * minor,
            Severity.Major or Severity.MajorAndGameMisconduct => minor * PenaltyTuning.MajorSeconds / PenaltyTuning.MinorSeconds,
            _ => 0,
        };
    }

    /// <summary>Gets the length of a misconduct in this match: ten minutes, shortened in proportion to shorter periods.</summary>
    public static float MisconductSeconds(World world)
    {
        return MinorSeconds(world) * PenaltyTuning.MisconductSeconds / PenaltyTuning.MinorSeconds;
    }

    /// <summary>Hands out a penalty at once, with no delayed call: what a fight costs uses it. Calls on
    /// both clubs at the same moment are not paired as coincidental here.</summary>
    public static void Give(World world, int playerId, string infraction, Severity severity)
    {
        if (playerId < 0 || playerId >= world.Players.Count || world.Players[playerId].IsGoalie || InBox(world, playerId)) return;
        var p = world.Players[playerId];
        Assess(world, Book(world), new DelayedPenalty(p.Side, p.Id, infraction, world.Time, severity));
    }

    /// <summary>The referee sees an infraction: the arm goes up (<see cref="Referee.Referee.Signal"/>) and the call
    /// waits for the offending club to touch the puck. Waved off when penalties are off in the settings,
    /// outside play (the shootout included), for a goalie, for a skater already called, or when the club already serves two
    /// and the rules do not stack penalties (<see cref="RuleSet.StackPenalties"/>). Never called when the
    /// infraction's switch is off (<see cref="Infraction.Called"/>); every call is a minor under MinorsOnly; a call
    /// after the whistle (roughing) is made at the stoppage it came in.</summary>
    public static bool Signal(World world, int playerId, string infraction, Severity severity = Severity.Minor, bool afterWhistle = false)
    {
        if (!world.Settings.Penalties || !MatchFlow.IsRunning(world)) return false;
        if (playerId < 0 || playerId >= world.Players.Count) return false;
        var m = world.Match;
        if (m.Phase != MatchPhase.Play && !(afterWhistle && m.Phase == MatchPhase.Stopped)) return false;
        if (!Infraction.Called(world.Setup.Rules, infraction)) return false;
        if (world.Setup.Rules.PenaltySeverity == PenaltySeverity.MinorsOnly) severity = Severity.Minor;
        var p = world.Players[playerId];
        var book = Book(world);
        if (p.IsGoalie || InBox(world, playerId) || book.Delayed.Exists(d => d.PlayerId == playerId)) return false;
        if (!world.Setup.Rules.StackPenalties && Serving(world, p.Side) + book.Delayed.Count(d => d.Side == p.Side) >= PenaltyTuning.MaxServing) return false;
        book.Delayed.Add(new DelayedPenalty(p.Side, playerId, infraction, world.Time, severity));
        Referee.Referee.Signal(world, infraction, playerId);
        book.Log(world, $"{infraction} on {Name(world, p)}{(severity == Severity.Minor ? "" : $" ({severity})")}, {(afterWhistle ? "after the whistle" : "delayed")}");
        return true;
    }

    /// <summary>Gets the end-zone dot of the club that defends that end, on the puck's side: where play restarts after a call.</summary>
    public static Vector3 FaceoffDot(World world, Side side)
    {
        int end = FaceoffFormation.DefendedEnd(world.Match, side);
        float z = world.Puck.Position.Z < 0 ? -6.7f : 6.7f;
        return new Vector3(end * 20.7f, 0, z);
    }

    /// <summary>Gets the spot on the ice by a club's penalty box door, on the near boards opposite the benches.</summary>
    public static Vector3 BoxDoor(Side side)
    {
        return new Vector3(side == Side.Home ? -PenaltyTuning.BoxDoorX : PenaltyTuning.BoxDoorX, 0, RinkSpec.HalfWidth - PenaltyTuning.BoxDoorInset);
    }

    /// <summary>Makes every delayed call: each skater to the box, a Penalty event per call, the referee's signal.
    /// Calls on both clubs at the same whistle pair up as coincidental minors: substituted, so
    /// the strength stays, except one each from full strength under the NHL's 4 on 4.</summary>
    internal static void CallDelayed(World world, PenaltyBook book)
    {
        // Only minors pair up; a major, double minor or misconduct is served as called
        int pairs = Math.Min(book.Delayed.Count(d => d.Side == Side.Home && d.Severity == Severity.Minor), book.Delayed.Count(d => d.Side == Side.Away && d.Severity == Severity.Minor));
        bool fourOnFour = world.Setup.Rules.CoincidentalMinors == CoincidentalMinors.FourOnFour
            && pairs == 1 && book.Delayed.Count == 2 && Serving(world, Side.Home) == 0 && Serving(world, Side.Away) == 0;
        var paired = new HashSet<int>();
        if (!fourOnFour)
        {
            foreach (var side in new[] { Side.Home, Side.Away })
                foreach (var d in book.Delayed.Where(d => d.Side == side && d.Severity == Severity.Minor).Take(pairs)) paired.Add(d.PlayerId);
        }

        foreach (var d in book.Delayed) Assess(world, book, d, paired.Contains(d.PlayerId));
        book.Delayed.Clear();
    }

    /// <summary>Sends a skater to the box and publishes the Penalty event (Strength = seconds the club serves,
    /// or the misconduct's for a misconduct alone). A substituted coincidental minor leaves the
    /// strength alone; a club already serving two puts the penalty on <see cref="Waiting"/>.
    /// A misconduct keeps the offender in the box on his own clock, a game misconduct ejects him.</summary>
    internal static void Assess(World world, PenaltyBook book, DelayedPenalty d, bool substitute = false)
    {
        var p = world.Players[d.PlayerId];
        var severity = d.Severity;
        float seconds = ClubSeconds(world, severity);
        bool waits = false;
        if (seconds > 0)
        {
            var penalty = new PenaltyState(p.Id, p.Side, d.Infraction, seconds);
            waits = !substitute && Serving(world, p.Side) >= PenaltyTuning.MaxServing;
            if (waits) book.Waiting.Add(penalty);
            else world.Match.Penalties.Add(penalty);
            if (substitute) book.Substituted.Add(p.Id);
            if (severity != Severity.Minor) book.Lengths[p.Id] = severity;
            // a skater who changed off during the delay still goes in; a teammate of his group leaves the ice so the club still plays short
            if (!p.OnIce && !waits && !substitute && world.OnIce(p.Side).Where(o => !o.IsGoalie && Squad.GroupOf(o) == Squad.GroupOf(p)).MinBy(o => o.Position.Z) is { } stand)
                ToBench(world, stand);
        }
        if (severity is Severity.Misconduct or Severity.MinorAndMisconduct)
            book.Misconducts.Add(new PenaltyState(p.Id, p.Side, d.Infraction, seconds + MisconductSeconds(world)));

        if (severity is Severity.MajorAndGameMisconduct or Severity.GameMisconduct) Eject(world, book, p);
        else ToBox(world, p);
        book.Called[(int)p.Side]++;
        float shown = seconds > 0 ? seconds : MisconductSeconds(world);
        world.Events.Publish(new PenaltyCalled(world.Time, p.Side, p.Id, p.Position, shown));
        Referee.Referee.Announce(world, d.Infraction, p.Id);
        string what = severity == Severity.Minor ? "" : $", {severity}";
        book.Log(world, $"{d.Infraction}, {Name(world, p)}, {MatchFlow.ClockText(shown)}{what}{(substitute ? ", coincidental, substituted" : waits ? ", waits in the box" : "")}");
    }

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

    internal static PenaltyBook Book(World world)
    {
        return world.Penalties.Get(() => new PenaltyBook { Rng = Seeds.Dice(world.Seed, "penalties", 14) });
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

    /// <summary>Gets the penalties waiting for one of their club's two running penalties to end.</summary>
    public List<PenaltyState> Waiting { get; } = [];

    /// <summary>Gets the skaters whose time is up but who stay in the box until play stops, because a waiting penalty took their place.</summary>
    public HashSet<int> Held { get; } = [];

    /// <summary>Gets the skaters serving a substituted coincidental minor.</summary>
    public HashSet<int> Substituted { get; } = [];

    /// <summary>Gets what each running or waiting penalty costs when it is more than a minor, by player.</summary>
    public Dictionary<int, Severity> Lengths { get; } = [];

    /// <summary>Gets the misconducts running: the time each offender still sits, his club's penalty included.</summary>
    public List<PenaltyState> Misconducts { get; } = [];

    /// <summary>Gets the players thrown out of the match.</summary>
    public HashSet<int> Ejected { get; } = [];

    /// <summary>Gets each skater's last hit, checker id to the target and when, for roughing.</summary>
    public Dictionary<int, (int Target, float Time)> LastHit { get; } = [];

    /// <summary>Gets or sets when play last stopped, in <see cref="World.Time"/>, for hits after the whistle.</summary>
    public float StoppedAt { get; set; } = float.NegativeInfinity;

    /// <summary>Gets the penalties called per club this match.</summary>
    public int[] Called { get; } = new int[2];

    /// <summary>Gets or sets the game clock at the last step, to run penalty clocks by exactly what the game clock ran.</summary>
    public float LastClock { get; set; } = float.NaN;

    public int LastPeriod { get; set; }

    /// <summary>Gets or sets the skaters a side at full strength this period, read at the first faceoff of a period with nobody in the box.</summary>
    public int BaseSkaters { get; set; } = 5;

    /// <summary>Gets or sets the period whose first faceoff set <see cref="BaseSkaters"/>.</summary>
    public int BasePeriod { get; set; }

    public MatchPhase LastPhase { get; set; } = MatchPhase.Intro;

    /// <summary>Gets or sets the dice: the match seed's stream; scenes and tests seed it.</summary>
    public required Random Rng { get; set; }

    /// <summary>Gets or sets a scripted call for the penalty scene: seconds after the first drop, or NaN.</summary>
    public float ScriptAfterDrop { get; set; } = float.NaN;

    /// <summary>Gets or sets the scripted call's infraction and what it costs (the major scene).</summary>
    public (string Infraction, Severity Severity) ScriptCall { get; set; } = (Infraction.Tripping, Severity.Minor);

    public float FirstDrop { get; set; } = float.NaN;

    public void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine($"PENALTY: {what}; P{m.Period} {MatchFlow.ClockText(m.Clock)}");
    }
}
