using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The rule presets and the player's changes. Checks the presets, CUSTOM and back, that a
/// change survives a restart (settings.json written and read again), the overtime switch on a match
/// setup, scripted plays under each preset (icing hybrid, touch and off, offside on and off, goalie
/// pull, penalties and fights), the arcade entries fixed to Arcade and a season keeping the rules in
/// its save. Formerly SAP_SCENE=rulestest.
/// </summary>
public sealed class RuleSetTests : CheckedTest
{
    static readonly MatchSetup Setup = WorldBuilder.DefaultSetup with { PeriodSeconds = 600 };

    readonly string dir = Path.Combine(Path.GetTempPath(), $"sap_rulestest_{Environment.ProcessId}_{Guid.NewGuid():N}");

    [Before(Test)]
    public void MakeFolder() => Directory.CreateDirectory(dir);

    [After(Test)]
    public void CleanUp()
    {
        SeasonSession.End();
        Directory.Delete(dir, recursive: true);
    }

    [Test]
    public void Presets()
    {
        var shl = RuleSet.Shl;
        Check(shl is { Offside: true, Icing: IcingCall.Hybrid, GoaliePull: true, Overtime: OvertimeStyle.ThreeOnThree, PlayoffOvertime: OvertimeStyle.SuddenDeath, Penalties: true, Fights: true, Strictness: RefereeStrictness.Normal },
            "SHL: offside, hybrid icing, goalie pull, 3 on 3 then shootout, playoff sudden death, penalties, fights");
        Check(RuleSet.Arcade is { Offside: false, Icing: IcingCall.Off, Penalties: true, Fights: true, Strictness: RefereeStrictness.Lenient, FightPenalty: FightPenalty.LoserMinor },
            "Arcade: no offside, no icing, a lenient referee, the loser's 2 minutes for a fight");
        Check(RuleSet.Nhl is { Offside: true, Icing: IcingCall.Hybrid, ShootoutFirst: ShootoutFirst.HomeChooses, FightPenalty: FightPenalty.Major, CoincidentalMinors: CoincidentalMinors.FourOnFour },
            "NHL: SHL rules with home choosing, a fight major and 4 on 4 coincidentals");
        Check(string.Equals(RuleSet.Shl.Name + RuleSet.Arcade.Name + RuleSet.Nhl.Name, "SHLARCADENHL", StringComparison.Ordinal), "the presets show their names");
        Check(RuleRows.All.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() == RuleRows.All.Count
            && RuleRows.All.All(r => typeof(RuleSet).GetProperty(r.Key) is { }), "every rule key is a RuleSet property, once");
        Check(RuleRows.All.All(r => r.Values.All(v => string.Equals(r.Get(r.Set(RuleSet.Shl, v)), v, StringComparison.Ordinal))), "every value of every rule reads back as set");
    }

    [Test]
    public void Choices()
    {
        var offside = RuleRows.All.First(r => string.Equals(r.Key, nameof(RuleSet.Offside), StringComparison.Ordinal));
        var choice = RuleChoice.Of(RulePreset.Shl).With(offside, "Off");
        Check(choice.Resolve() is { Offside: false, Custom: true, Preset: RulePreset.Shl } && string.Equals(choice.Resolve().Name, "CUSTOM", StringComparison.Ordinal), "a changed rule plays and shows CUSTOM");
        var back = choice.With(offside, "On");
        Check(back.Overrides.Count == 0 && back.Resolve() == RuleSet.Shl && string.Equals(back.Resolve().Name, "SHL", StringComparison.Ordinal), "setting it back to the preset's value clears CUSTOM");
        var arcade = RuleChoice.Of(RulePreset.Arcade).With(offside, "On");
        Check(arcade.Resolve() is { Offside: true, Icing: IcingCall.Off, Custom: true }, "a change lays over its own preset");
        Check(new RuleChoice { Preset = RulePreset.Nhl, Overrides = new Dictionary<string, string>(StringComparer.Ordinal) { ["Nonsense"] = "x", ["Icing"] = "Sideways" } }.Resolve() == RuleSet.Nhl,
            "unknown keys and values in settings.json are ignored");

        ScreenFlow.Prefs = new ScreenPrefs { Rules = choice };
        RulesScreen.Pick(RulePreset.Nhl);
        Check(ScreenFlow.Prefs.Rules.Overrides.Count == 0 && ScreenFlow.Prefs.Rules.Resolve() == RuleSet.Nhl, "picking a preset clears the changes");
        Check(RulesScreen.Cycle(RulePreset.Nhl, 1) == RulePreset.Shl && RulesScreen.Cycle(RulePreset.Shl, -1) == RulePreset.Nhl, "the preset row wraps");
    }

    [Test]
    public void Restart()
    {
        string path = Path.Combine(dir, "settings.json");
        var icing = RuleRows.All.First(r => string.Equals(r.Key, nameof(RuleSet.Icing), StringComparison.Ordinal));
        var prefs = new ScreenPrefs { Rules = RuleChoice.Of(RulePreset.Nhl).With(icing, "Touch") };
        ScreenPrefsFile.Write(prefs, path);
        var read = ScreenPrefsFile.Read(path);
        Check(read.Rules.Resolve() is { Preset: RulePreset.Nhl, Icing: IcingCall.Touch, Custom: true }, "a changed rule survives a restart (settings.json written and read)");
        Check(File.ReadAllText(path).Contains("\"Icing\": \"Touch\"", StringComparison.Ordinal), "settings.json stores the change by its key");
        File.WriteAllText(path, "{ \"periodMinutes\": 5 }");
        Check(ScreenPrefsFile.Read(path).Rules.Resolve() == RuleSet.Shl, "a settings.json from before W44 plays SHL");
    }

    [Test]
    public void Overtime()
    {
        var season = Setup with { Overtime = OvertimeRule.Shl };
        Check(OvertimeFormat.Of(season.WithRules(RuleSet.Shl)) == OvertimeFormat.Shl, "SHL: a tie goes to 3 on 3, then a shootout");
        var shootout = RuleSet.Shl with { Overtime = OvertimeStyle.ShootoutOnly };
        Check(OvertimeFormat.Of(season.WithRules(shootout)) == OvertimeFormat.ShootoutOnly, "overtime switched to shootout only");
        Check(OvertimeFormat.Of(season.WithRules(RuleSet.Shl with { Overtime = OvertimeStyle.Tie })) == OvertimeFormat.None, "overtime switched off: the tie stands");
        Check(OvertimeFormat.Of(season.WithRules(RuleSet.Shl with { Overtime = OvertimeStyle.SuddenDeath })).UntilGoal, "overtime switched to 5 on 5 sudden death");
        var playoff = season with { OvertimeFormat = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = 90 } };
        Check(OvertimeFormat.Of(playoff.WithRules(RuleSet.Shl)) == playoff.OvertimeFormat, "a playoff game keeps its mode's sudden death format");
        Check(OvertimeFormat.Of(playoff.WithRules(RuleSet.Shl with { PlayoffOvertime = OvertimeStyle.ThreeOnThree })) == OvertimeFormat.Shl, "playoff overtime switched to 3 on 3 and a shootout");
        var test = Setup with { Overtime = OvertimeRule.None };
        Check(test.WithRules(RuleSet.Nhl) == test with { Rules = RuleSet.Nhl }, "a setup whose tie stands keeps it");
    }

    [Test]
    public void Icing()
    {
        Check(Iced(RuleSet.Shl, toucher: SideOrNone.None) == 1, "SHL: hybrid icing is called on the race");
        Check(Iced(RuleSet.Nhl, toucher: SideOrNone.None) == 1, "NHL: hybrid icing is called on the race");
        Check(Iced(RuleSet.Arcade, toucher: SideOrNone.None) == 0, "Arcade: no icing");
        var touch = RuleSet.Shl with { Icing = IcingCall.Touch };
        Check(Iced(touch, toucher: SideOrNone.None) == 0, "touch icing waits on the next touch");
        Check(Iced(touch, toucher: SideOrNone.Away) == 1, "touch icing: a defender touching first calls it");
        Check(Iced(touch, toucher: SideOrNone.Home) == 0, "touch icing: an attacker touching first waves it off");
    }

    /// <summary>A dump from the home half over the away goal line, the away defender nearer the dot; returns the icing calls.</summary>
    static int Iced(RuleSet rules, SideOrNone toucher)
    {
        var w = Make(new Settings(), rules);
        var d = Skater(w, Side.Home, PlayingPosition.D);
        Skater(w, Side.Away, PlayingPosition.D).Position = new Vector3(22, 0, 10);
        Send(w, d, new Vector3(-10, 0, 5), new Vector3(30, 0, 0));
        var ev = Run(w, 1.5f, [new PuckPhysics(), new MatchRules(), new IcingRule()]);
        if (toucher.TryGetSide(out var side)) ev.AddRange(Run(w, 0.5f, [new IcingRule()], Maybe.Some<Action<World>>(x => x.Puck.LastTouch = Skater(x, side, side == Side.Home ? PlayingPosition.RW : PlayingPosition.D).Id)));
        return ev.Count(e => e is IcingCalled);
    }

    [Test]
    public void Offside()
    {
        static int Called(RuleSet rules)
        {
            var w = Make(new Settings(), rules);
            var c = Skater(w, Side.Home, PlayingPosition.C);
            Skater(w, Side.Home, PlayingPosition.RW).Position = new Vector3(13, 0, 4);
            Send(w, c, new Vector3(2, 0, 4), new Vector3(14, 0, 0));
            return Run(w, 3f, [new PuckPhysics(), new MatchRules(), new OffsideRule()]).Count(e => e is OffsideCalled);
        }

        Check(Called(RuleSet.Shl) == 1, "SHL: the pass to a winger in the zone is offside");
        Check(Called(RuleSet.Arcade) == 0, "Arcade: no offside");
        Check(Called(RuleSet.Nhl with { Offside = false, Custom = true }) == 0, "offside switched off under NHL");
    }

    [Test]
    public void GoaliePulls()
    {
        Check(GoaliePull.Pull(Make(new Settings(), RuleSet.Shl), Side.Home), "SHL: a club may pull its goalie");
        Check(!GoaliePull.Pull(Make(new Settings(), RuleSet.Shl with { GoaliePull = false }), Side.Home), "goalie pull switched off: the goalie stays");
    }

    [Test]
    public void PenaltiesAndFights()
    {
        var settings = new Settings();
        var on = Make(settings, RuleSet.Shl);
        ScreenFlow.Apply(on, settings);
        Check(on.Settings is { Penalties: true, Fights: true }, "SHL: penalties and fights on");
        var off = Make(settings with { Penalties = true, Fights = true }, RuleSet.Arcade with { Penalties = false, Fights = false });
        ScreenFlow.Apply(off, off.Settings);
        Check(off.Settings is { Penalties: false, Fights: false }, "the match's rules switch penalties and fights off, whatever the old settings said");
        Check(!Penalties.Signal(off, Skater(off, Side.Home, PlayingPosition.C).Id, Infraction.Tripping), "with penalties off no call is made");
        Check(RuleSet.Arcade.StrictnessScale < 1 && Math.Abs(RuleSet.Shl.StrictnessScale - 1) < 1e-6 && (RuleSet.Shl with { Strictness = RefereeStrictness.Strict }).StrictnessScale > 1, "referee strictness scales the call chance");
    }

    [Test]
    public void ArcadeFixed()
    {
        var clubs = WorldBuilder.Clubs;
        ScreenFlow.Prefs = new ScreenPrefs { Rules = RuleChoice.Of(RulePreset.Nhl).With(RuleRows.All[0], "Off") };
        Check(RulePicks.For(new MatchPlan(new ArcadeLivesMode(), [clubs[0]])) == RuleSet.Arcade
            && RulePicks.For(new MatchPlan(new ArcadeSuddenDeathMode(), [clubs[0]])) == RuleSet.Arcade
            && RulePicks.For(new MatchPlan(new ArcadeContinueMode(), [])) == RuleSet.Arcade, "every arcade entry plays the Arcade preset, whatever the player picked");
        Check(RulePicks.For(new MatchPlan(new ExhibitionMode(), clubs.Take(2).ToList())) is { Preset: RulePreset.Nhl, Offside: false, Custom: true }, "an exhibition plays the player's choice");
        Check(!RulePicks.Fixed(new ExhibitionMode()).HasValue && RulePicks.Fixed(new ArcadeLivesMode()) == RuleSet.Arcade, "team select fixes the rules row for arcade only");
    }

    [Test]
    public void SeasonKeepsRules()
    {
        var clubs = WorldBuilder.Clubs;
        string path = Path.Combine(dir, "season.json");
        ScreenFlow.Prefs = new ScreenPrefs { Rules = RuleChoice.Of(RulePreset.Nhl) };
        SeasonSession.StartNew(clubs, clubs[0].Id, 7, path);
        var plan = MatchPlan.SeasonGame();
        Check(RulePicks.For(plan) == RuleSet.Nhl, "a new season plays the rules picked when it starts");
        ScreenFlow.Prefs = new ScreenPrefs { Rules = RuleChoice.Of(RulePreset.Arcade) };
        Check(RulePicks.For(plan) == RuleSet.Nhl, "a later change in the settings leaves the running season alone");
        Check(SeasonSave.Read(path).Is(s => s.Rules == RuleSet.Nhl), "the season save stores the resolved rules");
        SeasonSession.Continue(clubs, path);
        Check(RulePicks.For(plan) == RuleSet.Nhl, "a continued season plays the rules from its save");
    }

    static World Make(Settings settings, RuleSet rules)
    {
        var w = new WorldBuilder().Setup(Setup).Rules(rules).Settings(settings).Build();
        Run(w, 10f, [new PuckPhysics(), new MatchRules()], until: Maybe.Some<Func<World, bool>>(x => x.Match.Phase == MatchPhase.Play));
        Park(w);
        return w;
    }

    static List<MatchEvent> Run(World world, float seconds, ISystem[] systems, Maybe<Action<World>> each = default, Maybe<Func<World, bool>> until = default)
    {
        var events = new List<MatchEvent>();
        for (int i = 0; i < seconds / Game.Step && !until.Is(u => u(world)) && (until.HasValue || world.Match.Phase is MatchPhase.Play); i++)
        {
            world.Events.Clear();
            if (each.TryGet(out var act)) act(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            world.Time += Game.Step;
            world.Tick++;
        }
        return events;
    }

    /// <summary>Moves every skater out of the way along the boards, each club in its own half of the neutral zone.</summary>
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

    static Player Skater(World w, Side side, PlayingPosition position) => w.OnIce(side).First(p => p.Data.Position == position);

    static void Send(World w, Player by, Vector3 from, Vector3 velocity)
    {
        var pk = w.Puck;
        pk.Carrier = -1;
        pk.LastTouch = by.Id;
        pk.Position = from;
        pk.Velocity = velocity;
    }
}
