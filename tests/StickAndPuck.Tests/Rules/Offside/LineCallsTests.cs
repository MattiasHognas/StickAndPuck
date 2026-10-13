using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Scripted plays with puck physics, the match rules, offside and icing (W7b): offside and icing
/// are called correctly and lead to the right faceoff. In period 1 home defends -x, so home attacks
/// the zone at +x. Formerly SAP_SCENE=offsidetest.
/// </summary>
public sealed class LineCallsTests : CheckedTest
{
    /// <summary>A 600-second-period match in play at the given period, every skater parked along the boards.</summary>
    static World Make(int period = 1)
    {
        return new WorldBuilder()
            .Setup(s => s with { PeriodSeconds = 600 })
            .Then(w =>
            {
                if (period > 1) MatchFlow.StartPeriod(w, period, 600);
                Run(w, x => x.Match.Phase == MatchPhase.Play, null, 10);
                Park(w);
            })
            .Build();
    }

    /// <summary>Runs physics and rules step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
    static List<MatchEvent> Run(World world, Func<World, bool> until, Action<World>? each = null, float limit = 10)
    {
        var systems = Systems(world);
        var events = new List<MatchEvent>();
        for (int i = 0; i < limit / Game.Step && !until(world); i++)
        {
            world.Events.Clear();
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, ISystem[]> Sets = new();

    /// <summary>One set of systems per world, so physics keeps its own state across runs.</summary>
    static ISystem[] Systems(World world)
    {
        return Sets.GetValue(world, _ => [new PuckPhysics(), new MatchRules(), new OffsideRule(), new IcingRule()]);
    }

    static List<MatchEvent> RunFor(World world, float seconds, Action<World>? each = null)
    {
        float end = world.Time + seconds;
        return Run(world, w => w.Time >= end - Game.Step / 2, each);
    }

    /// <summary>Moves every skater out of the way along the boards: each club in its own half of the neutral zone.</summary>
    static void Park(World w)
    {
        float[] z = [-11, -9.5f, 9.5f, 11, 11];
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int own = FaceoffFormation.DefendedEnd(w.Match, side);
            int i = 0;
            foreach (var p in w.OnIce(side).Where(p => !p.IsGoalie))
            {
                p.Position = new Vector3(own * (3 + (i == 4 ? 1.5f : 0)), 0, z[i]);
                p.Velocity = Vector3.Zero;
                i++;
            }
        }
    }

    static Player Skater(World w, Side side, PlayingPosition position)
    {
        return w.OnIce(side).First(p => p.Data.Position == position);
    }

    /// <summary>Sends the puck loose from a point, last touched by a player.</summary>
    static void Send(World w, Player by, Vector3 from, Vector3 velocity)
    {
        var pk = w.Puck;
        pk.Carrier = -1;
        pk.LastTouch = by.Id;
        pk.Position = from;
        pk.Velocity = velocity;
    }

    static int Count<T>(List<MatchEvent> events)
        where T : MatchEvent
    {
        return events.OfType<T>().Count();
    }

    /// <summary>The whistle leads to a faceoff at <paramref name="dot"/> with the puck there.</summary>
    void FaceoffAt(World w, Vector3 dot, string what)
    {
        Check(w.Match.Phase == MatchPhase.Stopped && MatchFlow.FaceoffDot(w) == dot, $"{what}: play stops for a faceoff at ({dot.X}, {dot.Z})");
        Run(w, x => x.Match.Phase == MatchPhase.Faceoff, null, 5);
        Check(w.Match.Phase == MatchPhase.Faceoff && w.Puck.Position == dot, $"{what}: the puck is placed on that dot");
    }

    /// <summary>A carrier skates the puck in with every teammate behind him: no offside.</summary>
    [Test]
    public void CleanCarry()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        c.Position = new Vector3(4, 0, 2);
        w.Puck.Carrier = c.Id;
        w.Puck.LastTouch = c.Id;
        var ev = RunFor(w, 1.5f, x =>
        {
            c.Position += new Vector3(9 * Game.Step, 0, 0);
            x.Puck.Position = c.Position + new Vector3(0.6f, 0, 0);
        });
        Check(c.Position.X > 15 && Count<OffsideCalled>(ev) == 0 && w.Match.Phase == MatchPhase.Play, "a clean carry-in is onside");
    }

    /// <summary>A pass over the line to a winger already in the zone: pending, then called when he plays it.</summary>
    [Test]
    public void PassToOffsidePlayer()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        rw.Position = new Vector3(13, 0, 4);
        Send(w, c, new Vector3(2, 0, 4), new Vector3(14, 0, 0));
        var pending = SideOrNone.None;
        var ev = Run(w, x => x.Match.Phase != MatchPhase.Play, x => { if (!pending.IsSet()) pending = OffsideRule.Pending(x); }, 3);
        Check(pending == SideOrNone.Home, "the pass over the line makes an offside pending for home");
        var off = ev.OfType<OffsideCalled>().ToList();
        Check(off.Count == 1 && off[0].Attacker == Side.Home, "the winger touching it is called offside, home's offence");
        Check(Count<WhistleBlown>(ev) == 1, "one whistle");
        FaceoffAt(w, new Vector3(6.1f, 0, 6.7f), "offside");
    }

    /// <summary>The offside winger tags up at the line, then plays the puck: no call.</summary>
    [Test]
    public void TagUp()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        rw.Position = new Vector3(13, 0, -8);
        Send(w, c, new Vector3(2, 0, 4), new Vector3(12, 0, 0));
        Run(w, x => OffsideRule.Pending(x) != SideOrNone.None, null, 2);
        Check(OffsideRule.Pending(w) == SideOrNone.Home, "a dump-in past an offside winger is pending");
        w.Puck.Velocity = Vector3.Zero;
        rw.Position = new Vector3(RinkSpec.BlueLineX + 0.2f, 0, -8);   // a skate on the line
        var ev = RunFor(w, 0.2f);
        Check(OffsideRule.Pending(w) == SideOrNone.None, "tagging up at the blue line waves it off");
        rw.Position = w.Puck.Position + new Vector3(0.5f, 0, 0);
        ev.AddRange(RunFor(w, 0.5f, x => x.Puck.Carrier = rw.Id));
        Check(Count<OffsideCalled>(ev) == 0 && w.Match.Phase == MatchPhase.Play, "after tagging up he may play the puck");
    }

    /// <summary>The puck goes back out over the line while offside is pending: waved off.</summary>
    [Test]
    public void PuckLeavesZone()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        var d = Skater(w, Side.Away, PlayingPosition.D);
        rw.Position = new Vector3(13, 0, -8);
        Send(w, c, new Vector3(2, 0, 4), new Vector3(12, 0, 0));
        Run(w, x => OffsideRule.Pending(x) != SideOrNone.None, null, 2);
        Send(w, d, w.Puck.Position, new Vector3(-15, 0, 0));
        Run(w, x => x.Puck.Position.X < 3, null, 2);
        Check(OffsideRule.Pending(w) == SideOrNone.None, "the defence clearing the zone waves the offside off");
        w.Puck.Velocity = Vector3.Zero;
        var ev = RunFor(w, 0.3f, x => x.Puck.Carrier = rw.Id);
        Check(Count<OffsideCalled>(ev) == 0, "and no call follows");
    }

    /// <summary>The defending club sends the puck into its own zone with an attacker inside: never offside.</summary>
    [Test]
    public void DefenceSendsItIn()
    {
        var w = Make();
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        var d = Skater(w, Side.Away, PlayingPosition.D);
        rw.Position = new Vector3(13, 0, -8);
        Send(w, d, new Vector3(2, 0, 4), new Vector3(12, 0, 0));
        var ev = Run(w, x => x.Puck.Position.X > 10, null, 2);
        Check(OffsideRule.Pending(w) == SideOrNone.None, "a puck the defence sends in is not offside");
        w.Puck.Velocity = Vector3.Zero;
        rw.Position = w.Puck.Position;
        ev.AddRange(RunFor(w, 0.3f, x => x.Puck.Carrier = rw.Id));
        Check(Count<OffsideCalled>(ev) == 0, "the attacker may play it");
    }

    /// <summary>Carrying the puck in with a teammate already deep in the zone: called at once.</summary>
    [Test]
    public void CarriedWithTeammateAhead()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var lw = Skater(w, Side.Home, PlayingPosition.LW);
        lw.Position = new Vector3(15, 0, 8);
        c.Position = new Vector3(5, 0, -3);
        w.Puck.Carrier = c.Id;
        w.Puck.LastTouch = c.Id;
        var ev = Run(w, x => x.Match.Phase != MatchPhase.Play, x =>
        {
            c.Position += new Vector3(9 * Game.Step, 0, 0);
            x.Puck.Position = c.Position + new Vector3(0.6f, 0, 0);
        }, 2);
        var off = ev.OfType<OffsideCalled>().ToList();
        Check(off.Count == 1 && off[0].Attacker == Side.Home && off[0].Position.X < 9, "carrying in with a teammate ahead is offside as the puck crosses");
        FaceoffAt(w, new Vector3(6.1f, 0, -6.7f), "carried offside");
    }

    /// <summary>A shot at the net from outside the zone with a winger offside: called before it can count.</summary>
    [Test]
    public void ShotOnGoalWhileOffside()
    {
        var w = Make();
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        rw.Position = new Vector3(14, 0, -8);
        var from = new Vector3(3, 0, -1);
        Send(w, c, from, Vector3.Normalize(RinkSpec.NetMouth(1) - from) * 32);
        var ev = RunFor(w, 1.5f);
        Check(Count<OffsideCalled>(ev) == 1 && Count<GoalScored>(ev) == 0 && w.Match.Score[0] == 0, "a shot on goal past an offside winger is called, no goal");
        Check(MatchFlow.FaceoffDot(w) == new Vector3(6.1f, 0, -6.7f), "faceoff at the neutral dot on the puck's side");
    }

    /// <summary>In period 2 home attacks -x; offside there goes to the neutral dot at -x.</summary>
    [Test]
    public void OffsideAfterEndsSwap()
    {
        var w = Make(2);
        var c = Skater(w, Side.Home, PlayingPosition.C);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        rw.Position = new Vector3(-13, 0, -4);
        Send(w, c, new Vector3(-2, 0, -4), new Vector3(-14, 0, 0));
        var ev = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 3);
        Check(ev.Any(e => e is OffsideCalled { Attacker: Side.Home }), "period 2: home offside in the -x zone");
        Check(MatchFlow.FaceoffDot(w) == new Vector3(-6.1f, 0, -6.7f), "faceoff at the -x neutral dot");
    }

    /// <summary>A dump from the home half over the away goal line, away defender closer: icing.</summary>
    [Test]
    public void Icing()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        ad.Position = new Vector3(22, 0, 10);
        Send(w, d, new Vector3(-10, 0, 5), new Vector3(30, 0, 0));
        var ev = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 3);
        var icing = ev.OfType<IcingCalled>().ToList();
        Check(icing.Count == 1 && icing[0].Offender == Side.Home && icing[0].PlayerId == d.Id, "home ices the puck");
        Check(icing.Count == 1 && icing[0].Position.X > RinkSpec.GoalLineX, "called as it crosses the goal line");
        FaceoffAt(w, new Vector3(-20.7f, 0, 6.7f), "icing");
    }

    /// <summary>Sent from beyond the red line: no icing.</summary>
    [Test]
    public void NoIcingFromOverRedLine()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        ad.Position = new Vector3(22, 0, 10);
        Send(w, d, new Vector3(1, 0, 5), new Vector3(30, 0, 0));
        var ev = RunFor(w, 1.5f);
        Check(Count<IcingCalled>(ev) == 0 && w.Match.Phase == MatchPhase.Play, "a dump from past the red line is not icing");
    }

    /// <summary>Hybrid icing: an attacker closer to the dot wins the race and waves it off.</summary>
    [Test]
    public void AttackerWinsRace()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var rw = Skater(w, Side.Home, PlayingPosition.RW);
        rw.Position = new Vector3(20.7f, 0, 9);
        Send(w, d, new Vector3(-10, 0, 3), new Vector3(30, 0, 0));
        var ev = RunFor(w, 1.5f);
        Check(Count<IcingCalled>(ev) == 0 && w.Match.Phase == MatchPhase.Play, "an attacker winning the race waves icing off");
    }

    /// <summary>The goalie plays the puck before the line: no icing.</summary>
    [Test]
    public void GoalieTouch()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        var g = w.OnIce(Side.Away).First(p => p.IsGoalie);
        ad.Position = new Vector3(22, 0, 10);
        Send(w, d, new Vector3(-10, 0, 5), new Vector3(30, 0, 0));
        var ev = RunFor(w, 1.5f, x =>
        {
            if (x.Puck.Position.X > 24 && x.Puck.LastTouch == d.Id) x.Puck.LastTouch = g.Id;
        });
        Check(Count<IcingCalled>(ev) == 0, "a puck the goalie touched is not icing");
    }

    /// <summary>A short-handed club may ice the puck.</summary>
    [Test]
    public void ShortHanded()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        ad.Position = new Vector3(22, 0, 10);
        w.Match.Penalties.Add(new PenaltyState(d.Id, Side.Home, "hooking", 120));
        Send(w, d, new Vector3(-10, 0, 5), new Vector3(30, 0, 0));
        var ev = RunFor(w, 1.5f);
        Check(Count<IcingCalled>(ev) == 0, "a short-handed club may ice the puck");
        w.Match.Penalties.Add(new PenaltyState(ad.Id, Side.Away, "slashing", 120));
        Send(w, d, new Vector3(-10, 0, 5), new Vector3(30, 0, 0));
        ev = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 3);
        Check(Count<IcingCalled>(ev) == 1, "at even strength again it is icing");
    }

    /// <summary>A long shot from the own half that goes in is a goal, not icing.</summary>
    [Test]
    public void IcedIntoTheNet()
    {
        var w = Make();
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        ad.Position = new Vector3(22, 0, 10);
        var from = new Vector3(-10, 0, 0.2f);
        Send(w, d, from, Vector3.Normalize(RinkSpec.NetMouth(1) - from) * 30);
        var ev = RunFor(w, 2);
        Check(Count<GoalScored>(ev) == 1 && Count<IcingCalled>(ev) == 0 && w.Match.Score[0] == 1, "a long shot into the net is a goal, not icing");
    }

    /// <summary>In period 2 home defends +x: its icing toward -x brings a faceoff at its +x end-zone dot.</summary>
    [Test]
    public void IcingAfterEndsSwap()
    {
        var w = Make(2);
        var d = Skater(w, Side.Home, PlayingPosition.D);
        var ad = Skater(w, Side.Away, PlayingPosition.D);
        ad.Position = new Vector3(-22, 0, -10);
        Send(w, d, new Vector3(10, 0, -5), new Vector3(-30, 0, 0));
        var ev = Run(w, x => x.Match.Phase != MatchPhase.Play, null, 3);
        Check(ev.Any(e => e is IcingCalled { Offender: Side.Home }), "period 2: home ices toward -x");
        Check(MatchFlow.FaceoffDot(w) == new Vector3(20.7f, 0, -6.7f), "faceoff at home's own end-zone dot at +x");
    }
}
