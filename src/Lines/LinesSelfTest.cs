using System.Numerics;
using StickAndPuck.AI;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.Lines;

/// <summary>
/// SAP_SCENE=linestest: checks fatigue, line changes and the goalie swap with no window, prints
/// one line per check and exits with 0 when all pass, 1 otherwise. Covers the W13 "done when":
/// tired lines slow down and change, and the backup goalie comes in after the trigger; and W23's
/// pulled goalie: the extra attacker comes on and the goalie is back at the next stoppage.
/// </summary>
public sealed class LinesSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "linestest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        LinesTuning.Pace = 1f;
        var setup = Scenes.DefaultSetup(clubs);
        World Make(Settings? s = null, bool human = false, RuleSet? rules = null)
        {
            var w = new World(rules is { } r ? setup.WithRules(r) : setup, s ?? settings);
            Scenes.AddStarters(w);
            if (human) w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.C).Slot = 0;
            MatchFlow.Begin(w, log: false);
            new LinesSystem().Start(w);
            return w;
        }

        SquadOnTheBench(Make());
        TiringAndResting(Make());
        TiredSkatersSlowDown(Make());
        ChangeOnTheFly(Make(human: true));
        NotWhileDefending(Make());
        ChangeAtFaceoff(Make(human: true));
        ManualMode(Make(settings with { Lines = LineChanges.Manual }, human: true));
        GoalieAfterABurst(Make());
        NoSwapForSpreadGoals(Make());
        NewPeriodFirstLine(Make());
        OvertimeThreeOnThree(Make());
        PulledGoalie(Make(human: true));
        NoChangeAfterIcing(Make(human: true, rules: RuleSet.Shl), hold: true, "SHL");
        NoChangeAfterIcing(Make(human: true, rules: RuleSet.Nhl), hold: true, "NHL");
        NoChangeAfterIcing(Make(human: true, rules: RuleSet.Arcade with { Icing = IcingCall.Hybrid }), hold: false, "Arcade");
        HeldChangeOnTheFly(Make(human: true, rules: RuleSet.Shl));

        Console.WriteLine(failures == 0 ? "LINESTEST: all checks passed" : $"LINESTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    /// <summary>Runs lines and skating for a while, calling <paramref name="each"/> before each step; returns the events.</summary>
    static List<MatchEvent> Run(World world, float seconds, Action<World>? each = null, Func<World, bool>? until = null)
    {
        var systems = new ISystem[] { new LinesSystem(), new SkatingSystem() };
        var events = new List<MatchEvent>();
        for (int i = 0; i < seconds / Game.Step && !(until?.Invoke(world) ?? false); i++)
        {
            world.Events.Clear();
            each?.Invoke(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static void Phase(World world, MatchPhase phase)
    {
        world.Match.Phase = phase;
        world.Puck.Live = phase == MatchPhase.Play;
    }

    static List<Player> On(World world, Side side, Group group)
    {
        return [.. world.OnIce(side).Where(p => !p.IsGoalie && Squad.GroupOf(p) == group)];
    }

    static void SetEnergy(IEnumerable<Player> players, float energy)
    {
        foreach (var p in players) p.Energy = energy;
    }

    /// <summary>Puts the puck in a club's attacking zone, so a change on the fly is safe.</summary>
    static void PuckUpIce(World world, Side side)
    {
        int end = -FaceoffFormation.DefendedEnd(world.Match, side);
        world.Puck.Position = new Vector3(end * 15f, 0, 0);
        world.Puck.Carrier = -1;
    }

    void SquadOnTheBench(World w)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var squad = Bench.SquadOf(w, side)!;
            int roster = w.Setup.Club(side).Roster.Count;
            Check(w.Players.Count(p => p.Side == side) == roster && w.OnIce(side).Count() == 6,
                $"{side}: whole roster of {roster} in the match, 6 on the ice");
            Check(squad.Lines.Count == 4 && squad.Lines.All(l => l.Members.Count == 3) && squad.Pairs.Count == 3
                && squad.Pairs.All(p => p.Members.Count == 2) && squad.Goalies.Count == 2,
                $"{side}: 4 lines of 3, 3 pairs, 2 goalies");
            Check(squad.Lines[0].Members.All(p => p.OnIce) && squad.Pairs[0].Members.All(p => p.OnIce) && squad.Goalies[0].OnIce,
                $"{side}: the starters are line 1, pair 1 and goalie 1");
        }
    }

    void TiringAndResting(World w)
    {
        Phase(w, MatchPhase.Play);
        var skater = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        var idle = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        var bench = w.Players.First(p => !p.OnIce && !p.IsGoalie);
        bench.Energy = 0.5f;
        Run(w, 10, world =>
        {
            skater.Command = new PlayerCommand { Move = new Vector2(SimMath.Sin(world.Time), SimMath.Cos(world.Time)), Sprint = true };
            foreach (var p in world.Players) if (p.OnIce && p != skater) p.Command = default;
            SetEnergy(world.Players.Where(p => p.OnIce && p != skater && p != idle), 1f);   // nobody else changes
        });
        Check(skater.Energy < idle.Energy - 0.05f && idle.Energy < 1f,
            $"a sprinting skater tires faster than a standing one ({skater.Energy:0.00} vs {idle.Energy:0.00} after 10 s)");
        Check(bench.Energy > 0.65f, $"the bench rests ({bench.Energy:0.00} from 0.50 after 10 s)");
        Check(w.Players.Where(p => p.IsGoalie).All(g => g.Energy >= 1f), "goalies do not tire");
    }

    void TiredSkatersSlowDown(World w)
    {
        var p = w.OnIce(Side.Home).First(o => !o.IsGoalie);
        float fresh = SkatingModel.TopSpeed(p.Data.Traits, 1f), spent = SkatingModel.TopSpeed(p.Data.Traits, 0.2f);
        Check(spent < fresh * 0.8f, $"a tired skater is slower ({spent:0.0} m/s vs {fresh:0.0} m/s fresh)");
        Check(LinesTuning.ShotFactor(0.2f) < LinesTuning.ShotFactor(1f), "and shoots softer");

        // the same skater racing over 20 m, fresh and tired
        float Race(float energy)
        {
            p.Position = new Vector3(-10, 0, 0);
            p.Velocity = Vector3.Zero;
            p.Energy = energy;
            float t = 0;
            while (p.Position.X < 10 && t < 10)
            {
                p.Command = new PlayerCommand { Move = Vector2.UnitX };
                SkatingModel.Step(p, Game.Step);
                p.Energy = energy;
                t += Game.Step;
            }
            return t;
        }
        float a = Race(1f), b = Race(0.2f);
        Check(b > a * 1.1f, $"over 20 m the tired skater is beaten ({b:0.00} s vs {a:0.00} s)");
    }

    void ChangeOnTheFly(World w)
    {
        Phase(w, MatchPhase.Play);
        PuckUpIce(w, Side.Home);
        var squad = Bench.SquadOf(w, Side.Home)!;
        var first = squad.Lines[0].Members.ToList();
        var human = w.Controlled(0)!;
        SetEnergy(first, 0.35f);
        var events = Run(w, 12, until: world => first.TrueForAll(p => !p.OnIce));
        var on = On(w, Side.Home, Group.Forwards);
        Check(first.TrueForAll(p => !p.OnIce), "tired line 1 goes to the bench on the fly");
        Check(on.Count == 3 && on.All(p => squad.UnitOf(p) is { Number: > 1 }), $"a fresh line comes on ({Bench.OnIceUnit(w, Side.Home, Group.Forwards)?.Name})");
        Check(On(w, Side.Home, Group.Defence).Count == 2 && squad.Pairs[0].Members.All(p => p.OnIce), "the defence pair stays on");
        Check(events.Count(e => e.Kind == MatchEventKind.LineChange && e.Side == Side.Home) == 1, "one LineChange event");
        Check(!human.OnIce || human.Slot < 0, "the human was taken off the leaving centre");
        Check(w.Controlled(0) is { OnIce: true }, "and controls a skater on the ice");
        var roles = Roles.Assign([.. w.OnIce(Side.Home).Where(p => !p.IsGoalie)]);
        Check(roles.Values.Distinct().Count() == 5, "the new five get five different roles");
        Check(on.Exists(p => p.Data.Position == PlayingPosition.C) && roles[on.First(p => p.Data.Position == PlayingPosition.C).Id] == Role.Centre,
            "the new centre plays centre");
    }

    void NotWhileDefending(World w)
    {
        Phase(w, MatchPhase.Play);
        var first = Bench.SquadOf(w, Side.Home)!.Lines[0].Members.ToList();
        int end = FaceoffFormation.DefendedEnd(w.Match, Side.Home);
        Run(w, 3, world =>
        {
            SetEnergy(first, 0.4f);
            world.Puck.Position = new Vector3(end * 20f, 0, 0);
            world.Puck.Carrier = -1;
        });
        Check(first.TrueForAll(p => p.OnIce) && !Bench.IsChanging(w, Side.Home), "a tired line stays on while the puck is in its own zone");
        Run(w, 10, world =>
        {
            foreach (var p in first) p.Energy = Math.Min(p.Energy, 0.2f);
            world.Puck.Position = new Vector3(end * 20f, 0, 0);
        }, until: _ => first.TrueForAll(p => !p.OnIce));
        Check(first.TrueForAll(p => !p.OnIce), "but goes off once spent");
    }

    void ChangeAtFaceoff(World w)
    {
        Phase(w, MatchPhase.Play);
        Run(w, 0.1f);
        var squad = Bench.SquadOf(w, Side.Home)!;
        var pair = squad.Pairs[0].Members.ToList();
        var line = squad.Lines[0].Members.ToList();
        SetEnergy(pair, 0.6f);
        SetEnergy(line, 0.6f);
        Phase(w, MatchPhase.Stopped);
        Run(w, 0.1f);
        Phase(w, MatchPhase.Faceoff);
        var events = Run(w, Game.Step);
        Check(pair.TrueForAll(p => !p.OnIce) && line.TrueForAll(p => !p.OnIce), "a tiring line and pair change at the faceoff");
        Check(On(w, Side.Home, Group.Forwards).Count == 3 && On(w, Side.Home, Group.Defence).Count == 2, "still five skaters");
        Check(events.Count(e => e.Kind == MatchEventKind.LineChange) == 2, "one LineChange per group");
        Check(w.Controlled(0) is { OnIce: true, Data.Position: PlayingPosition.C }, "the human moves to the new centre");
        Check(On(w, Side.Away, Group.Forwards).All(p => p.Energy > 0.9f), "the fresh away line stays on");
    }

    void ManualMode(World w)
    {
        Phase(w, MatchPhase.Play);
        PuckUpIce(w, Side.Home);
        var line = Bench.SquadOf(w, Side.Home)!.Lines[0].Members.ToList();
        var away = Bench.SquadOf(w, Side.Away)!.Lines[0].Members.ToList();
        Run(w, 3, world => { SetEnergy(line, 0.3f); SetEnergy(away, 0.3f); PuckUpIce(world, Side.Home); });
        Check(line.TrueForAll(p => p.OnIce), "manual: the human's tired line waits for the button");
        Check(Bench.IsChanging(w, Side.Away) || away.TrueForAll(p => !p.OnIce), "the AI club still changes by itself");
        bool pressed = false;
        Run(w, 12, world =>
        {
            if (!pressed && world.Controlled(0) is { } me) { me.Command = me.Command with { ChangeLine = true }; pressed = true; }
            else if (world.Controlled(0) is { } me2)
            {
                me2.Command = me2.Command with { ChangeLine = false };
            }
        }, until: _ => line.TrueForAll(p => !p.OnIce));
        Check(line.TrueForAll(p => !p.OnIce), "the button sends the tired line off");
    }

    void GoalieAfterABurst(World w)
    {
        Phase(w, MatchPhase.Play);
        var squad = Bench.SquadOf(w, Side.Away)!;
        var starter = squad.Goalies[0];
        var backup = squad.Goalies[1];
        for (int goal = 0; goal < LinesTuning.GoalieGoals; goal++)
        {
            Run(w, 20);
            w.Match.Score[(int)Side.Home]++;
            Phase(w, MatchPhase.Goal);
            Run(w, 0.5f);
            Check(starter.OnIce, $"goal {goal + 1}: the starter is still in until the faceoff");
            Phase(w, MatchPhase.Faceoff);
            var events = Run(w, 0.1f);
            if (goal < LinesTuning.GoalieGoals - 1)
            {
                Check(starter.OnIce && !backup.OnIce, $"goal {goal + 1}: the starter stays in");
            }
            else
            {
                Check(!starter.OnIce && backup.OnIce, $"{LinesTuning.GoalieGoals} goals in a minute: the backup comes in at the faceoff");
                Check(events.Exists(e => e.Kind == MatchEventKind.LineChange && e.PlayerId == backup.Id), "a LineChange event names the backup");
                Check(w.OnIce(Side.Away).Count(p => p.IsGoalie) == 1, "one goalie on the ice");
            }
            Phase(w, MatchPhase.Play);
        }
        Check(Bench.SquadOf(w, Side.Home)!.Goalies[0].OnIce, "the scoring club keeps its goalie");
    }

    void NoSwapForSpreadGoals(World w)
    {
        var starter = Bench.SquadOf(w, Side.Away)!.Goalies[0];
        float gap = LinesTuning.GoalieSpell * w.Setup.PeriodSeconds / 2 + 5;
        for (int goal = 0; goal < LinesTuning.GoalieGoals + 1; goal++)
        {
            Phase(w, MatchPhase.Play);
            Run(w, gap, world => SetEnergy(world.Players, 1f));
            w.Match.Score[(int)Side.Home]++;
            Phase(w, MatchPhase.Goal);
            Run(w, 0.1f);
            Phase(w, MatchPhase.Faceoff);
            Run(w, 0.1f);
        }
        Check(starter.OnIce, $"{LinesTuning.GoalieGoals + 1} goals spread over {(int)(gap * LinesTuning.GoalieGoals)} s: the starter stays in");
    }

    void NewPeriodFirstLine(World w)
    {
        Phase(w, MatchPhase.Play);
        PuckUpIce(w, Side.Home);
        var squad = Bench.SquadOf(w, Side.Home)!;
        SetEnergy(squad.Lines[0].Members, 0.3f);
        Run(w, 12, until: _ => squad.Lines[0].Members.All(p => !p.OnIce));
        Phase(w, MatchPhase.Intermission);
        Run(w, 0.5f);
        Check(w.Players.All(p => p.Energy >= 1f), "everyone rests between periods");
        Phase(w, MatchPhase.Faceoff);
        Run(w, 0.1f);
        Check(squad.Lines[0].Members.All(p => p.OnIce) && squad.Pairs[0].Members.All(p => p.OnIce), "the new period starts with line 1 and pair 1");
    }

    void OvertimeThreeOnThree(World w)
    {
        OvertimeRules.ThreeOnThree(w);
        int Skaters(Side side) => w.OnIce(side).Count(p => !p.IsGoalie);
        var kept = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        SetEnergy(w.Players.Where(p => p.OnIce && !p.IsGoalie), 0.5f);
        Phase(w, MatchPhase.Faceoff);
        Run(w, 0.1f);
        Check(kept.TrueForAll(p => !p.OnIce) && Skaters(Side.Home) == 3 && Skaters(Side.Away) == 3,
            "overtime: a tired three changes at the faceoff, still 3 on 3");
        Check(w.OnIce(Side.Home).Count(p => p.Data.Position == PlayingPosition.C) == 1 && On(w, Side.Home, Group.Defence).Count == 2,
            "a centre for the centre and a pair for the pair, not a full line");
        Phase(w, MatchPhase.Play);
        PuckUpIce(w, Side.Home);
        var three = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        bool alwaysThree = true;
        Run(w, 15, world =>
        {
            foreach (var p in three) p.Energy = Math.Min(p.Energy, 0.3f);
            PuckUpIce(world, Side.Home);
            alwaysThree &= Skaters(Side.Home) <= 3 && Skaters(Side.Away) <= 3;
        }, until: _ => three.TrueForAll(p => !p.OnIce));
        Check(three.TrueForAll(p => !p.OnIce) && Skaters(Side.Home) == 3 && alwaysThree, "on the fly in overtime: never more than three a side");
        Phase(w, MatchPhase.Shootout);
        var before = w.Players.Where(p => p.OnIce).ToList();
        SetEnergy(w.Players, 0.2f);
        var events = Run(w, 3);
        Check(!events.Exists(e => e.Kind == MatchEventKind.LineChange) && w.Players.Where(p => p.OnIce).SequenceEqual(before), "no changes in a shootout");
    }

    void PulledGoalie(World w)
    {
        Phase(w, MatchPhase.Play);
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        var benchForward = w.Players.Where(p => p.Side == Side.Home && !p.OnIce && Squad.GroupOf(p) == Group.Forwards).ToList();
        bool pulled = GoaliePull.Pull(w, Side.Home);
        var extra = GoaliePull.ExtraAttacker(w, Side.Home);
        Check(pulled && !goalie.OnIce && extra != null && benchForward.Contains(extra) && extra.OnIce && w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 6,
            "pulling the goalie: he goes to the bench and a forward from the bench comes on, six skaters");
        Check(Vector2.Distance(new Vector2(extra!.Position.X, extra.Position.Z), Squad.Door(Side.Home)) < 1f, "the extra attacker steps out of the bench door");
        Check(!GoaliePull.Pull(w, Side.Home), "a second pull does nothing");
        PuckUpIce(w, Side.Home);
        SetEnergy(w.OnIce(Side.Home).Where(p => !p.IsGoalie), 0.3f);
        bool alwaysSix = true;
        Run(w, 12, world =>
        {
            PuckUpIce(world, Side.Home);
            alwaysSix &= world.OnIce(Side.Home).Count(p => !p.IsGoalie) == 6 && !goalie.OnIce;
        });
        Check(alwaysSix, "line changes on the fly keep six out and the net empty");
        Phase(w, MatchPhase.Stopped);
        Run(w, 0.1f);
        Check(!GoaliePull.IsPulled(w, Side.Home) && goalie.OnIce && w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 5,
            "at the whistle the goalie is back and the extra attacker is off: five skaters");
        Check(w.Controlled(0) is { OnIce: true, Side: Side.Home }, "the human still controls a skater on the ice");
    }

    /// <summary>
    /// W49: home dumps the puck from its own half over the away goal line and icing is called. Both
    /// clubs' units are tired and home asks for a change while play is stopped: under SHL and NHL
    /// home faces off with the same five skaters and away changes; under Arcade both change. After
    /// the drop the hold is over and home's next stoppage changes again.
    /// </summary>
    void NoChangeAfterIcing(World w, bool hold, string preset)
    {
        var events = IceThePuck(w);
        Check(events.Count(e => e.Kind == MatchEventKind.Icing && e.Side == Side.Home) == 1, $"{preset}: home ices the puck");
        Check(IcingRule.HoldsLines(w, Side.Home) == hold && !IcingRule.HoldsLines(w, Side.Away),
            $"{preset}: {(hold ? "the icing club is held, the other is not" : "nobody is held")}");

        var home = w.OnIce(Side.Home).Where(p => !p.IsGoalie).ToList();
        var away = w.OnIce(Side.Away).Where(p => !p.IsGoalie).ToList();
        SetEnergy(home, 0.5f);
        SetEnergy(away, 0.5f);
        Bench.RequestChange(w, Side.Home);
        events = Rules(w, 15f, until: x => x.Match.Phase == MatchPhase.Play);
        Check(w.Match.Phase == MatchPhase.Play, $"{preset}: the faceoff after the icing is dropped");
        bool kept = home.TrueForAll(p => p.OnIce);
        Check(hold ? kept && !events.Exists(e => e.Kind == MatchEventKind.LineChange && e.Side == Side.Home) : home.TrueForAll(p => !p.OnIce),
            hold ? $"{preset}: the icing club faces off with the same five skaters, whatever the bench or button asks" : $"{preset}: the icing club changes freely");
        Check(away.TrueForAll(p => !p.OnIce) && On(w, Side.Away, Group.Forwards).Count == 3 && On(w, Side.Away, Group.Defence).Count == 2,
            $"{preset}: the other club changes, still five skaters");
        Check(w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 5, $"{preset}: home still has five skaters");

        Rules(w, 0.2f);
        Check(!IcingRule.HoldsLines(w, Side.Home), $"{preset}: the hold ends with the drop");
        if (!hold) return;
        Phase(w, MatchPhase.Stopped);
        Run(w, 0.1f);
        Phase(w, MatchPhase.Faceoff);
        Run(w, Game.Step);
        Check(home.TrueForAll(p => !p.OnIce), $"{preset}: at the next stoppage the tired icing club changes");
    }

    /// <summary>W49: home is mid-change on the fly when it ices the puck; the change stops where it is and nobody else comes on.</summary>
    void HeldChangeOnTheFly(World w)
    {
        Phase(w, MatchPhase.Play);
        var line = Bench.SquadOf(w, Side.Home)!.Lines[0].Members.ToList();
        IcingRule.HoldLines(w, Side.Home);
        Bench.RequestChange(w, Side.Home);
        SetEnergy(line, 0.3f);
        Run(w, Game.Step);
        Check(Bench.IsChanging(w, Side.Home), "home calls a change on the fly");
        Phase(w, MatchPhase.Stopped);
        Run(w, 0.1f);
        Phase(w, MatchPhase.Faceoff);
        Run(w, Game.Step);
        Check(!Bench.IsChanging(w, Side.Home) && line.TrueForAll(p => p.OnIce) && On(w, Side.Home, Group.Forwards).TrueForAll(line.Contains),
            "a change under way at the icing stops: the skaters on the ice stay on");
        Check(w.OnIce(Side.Home).Count(p => !p.IsGoalie) == 5, "five skaters after the stopped change");
    }

    /// <summary>A dump from the home half over the away goal line with an away defender nearest the dot; runs until the icing whistle.</summary>
    static List<MatchEvent> IceThePuck(World w)
    {
        Rules(w, 10f, until: x => x.Match.Phase == MatchPhase.Play);
        int z = 0;
        foreach (var p in w.OnIce(Side.Home).Where(p => !p.IsGoalie)) p.Position = new Vector3(-12, 0, -11 + z++ * 0.8f);
        foreach (var p in w.OnIce(Side.Away).Where(p => !p.IsGoalie)) p.Position = new Vector3(4, 0, -11 + z++ * 0.8f);
        w.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.D).Position = new Vector3(22, 0, 10);
        int own = FaceoffFormation.DefendedEnd(w.Match, Side.Home);
        var pk = w.Puck;
        pk.Carrier = -1;
        pk.LastTouch = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.D).Id;
        pk.Position = new Vector3(own * 10, 0, 5);
        pk.Velocity = new Vector3(-own * 30, 0, 0);
        return Rules(w, 3f, until: x => x.Match.Phase != MatchPhase.Play);
    }

    /// <summary>Runs the puck, match flow, icing and lines with nobody skating; returns the events.</summary>
    static List<MatchEvent> Rules(World world, float seconds, Func<World, bool>? until = null)
    {
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules(), new IcingRule(), new LinesSystem() };
        var events = new List<MatchEvent>();
        for (int i = 0; i < seconds / Game.Step && !(until?.Invoke(world) ?? false); i++)
        {
            world.Events.Clear();
            foreach (var p in world.Players) p.Command = default;
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }
}
