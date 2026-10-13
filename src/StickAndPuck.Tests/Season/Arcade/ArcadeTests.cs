using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The arcade run: the opponent order, that sudden death ends on the first loss and three lives on
/// the third, that a loss replays the same club, a champion run, the save after a match and
/// continuing after a restart, with a save in a temporary folder. Formerly SAP_SCENE=arcadetest.
/// </summary>
public sealed class ArcadeTests : CheckedTest
{
    static readonly FixtureResult HomeWin = new(3, 1, ResultKind.Regulation);
    static readonly FixtureResult AwayWin = new(1, 3, ResultKind.Regulation);

    static readonly string Folder = Path.Combine(Path.GetTempPath(), $"sap-arcadetest-{Environment.ProcessId}");
    static readonly string SavePath = Path.Combine(Folder, "arcade.json");

    [Before(Test)]
    public void SaveFolder() => Directory.CreateDirectory(Folder);

    [After(Test)]
    public void EndRun()
    {
        ArcadeSession.End();
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>A result the human's club wins (or loses) in the run's next game.</summary>
    static FixtureResult Result(ArcadeRun run, bool win, ResultKind kind = ResultKind.Regulation)
    {
        var r = run.NextAtHome == win ? HomeWin : AwayWin;
        return r with { HomeGoals = kind == ResultKind.Regulation ? r.HomeGoals : Math.Min(r.HomeGoals, 2), AwayGoals = kind == ResultKind.Regulation ? r.AwayGoals : Math.Min(r.AwayGoals, 2), Kind = kind };
    }

    [Test]
    public void Order()
    {
        var clubs = WorldBuilder.Clubs;
        string user = clubs[0].Id;
        var run = ArcadeRun.New(clubs, user, ArcadeRule.ThreeLives, 5);
        Check(run.Opponents.Count == clubs.Count - 1 && run.Opponents.Distinct().Count() == run.Opponents.Count, $"the run meets each of the other {clubs.Count - 1} clubs once");
        Check(!run.Opponents.Contains(user), "the human's club is not an opponent");
        var strength = run.Opponents.Select(id => ArcadeRun.Strength(clubs.First(c => Same(c.Id, id)))).ToList();
        Check(strength.Zip(strength.Skip(1)).All(p => p.Second >= p.First - 2), "opponents come weakest first");
        Check(run.Opponents.SequenceEqual(ArcadeRun.New(clubs, user, ArcadeRule.ThreeLives, 5).Opponents), "the same seed gives the same order");
        var setup = run.NextSetup(clubs)!;
        Check(Same(setup.Home.Id, user) && Same(setup.Away.Id, run.Opponents[0]) && setup.Inputs[0] is { Device: InputDevice.KeyboardMouse, Side: Side.Home } && setup.Overtime == OvertimeRule.Shl,
            "game 1: the human at home against the first club, SHL overtime");
        run.Record(Result(run, win: true));
        setup = run.NextSetup(clubs)!;
        Check(Same(setup.Away.Id, user) && Same(setup.Home.Id, run.Opponents[1]) && setup.Inputs[0].Side == Side.Away, "game 2: away to the second club");
    }

    [Test]
    public void SuddenDeath()
    {
        var clubs = WorldBuilder.Clubs;
        var run = ArcadeRun.New(clubs, clubs[3].Id, ArcadeRule.SuddenDeath, 1);
        Check(run.StartLives == 1 && run.LivesLeft == 1 && !run.IsOver, "sudden death starts with one life");
        run.Record(Result(run, win: true));
        run.Record(Result(run, win: true));
        Check(!run.IsOver && run.Stage == 2, "two wins: still going, stage 3 next");
        string? beaten = run.NextOpponent;
        run.Record(Result(run, win: false, ResultKind.Shootout));
        Check(run.IsOver && run.Lost && !run.Champion, "sudden death: a shootout loss ends the run");
        Check(run.Stage == 2 && run.Games.Count == 3 && Same(run.Games[^1].Opponent, beaten!), "the summary has two wins and the loss to the third club");
        Check(run.NextOpponent == null && run.NextSetup(clubs) == null, "a finished run has no next game");
        bool threw = false;
        try { run.Record(HomeWin); } catch (InvalidOperationException) { threw = true; }
        Check(threw && run.Games.Count == 3, "nothing is recorded after the run ends");
    }

    [Test]
    public void ThreeLives()
    {
        var clubs = WorldBuilder.Clubs;
        var run = ArcadeRun.New(clubs, clubs[5].Id, ArcadeRule.ThreeLives, 2);
        Check(run.StartLives == 3, "three lives starts with three");
        string first = run.NextOpponent!;
        run.Record(Result(run, win: false));
        Check(!run.IsOver && run.LivesLeft == 2 && Same(run.NextOpponent!, first), "first loss: two lives left, the same club again");
        run.Record(Result(run, win: true));
        run.Record(Result(run, win: false, ResultKind.Overtime));
        Check(!run.IsOver && run.LivesLeft == 1 && run.Stage == 1, "second loss (overtime): one life left, the run goes on");
        run.Record(Result(run, win: true));
        run.Record(Result(run, win: true));
        Check(!run.IsOver && run.Stage == 3, "wins after losses keep moving up");
        run.Record(Result(run, win: false));
        Check(run.IsOver && run.Lost && run.LivesLeft == 0 && run.Stage == 3 && run.Games.Count == 6, "third loss ends the run after three clubs beaten");
        var tie = ArcadeRun.New(clubs, clubs[5].Id, ArcadeRule.SuddenDeath, 2);
        tie.Record(new FixtureResult(2, 2, ResultKind.Regulation));
        Check(tie.IsOver, "a tie is not a win, so it costs a life");
    }

    [Test]
    public void Champion()
    {
        var clubs = WorldBuilder.Clubs;
        var run = ArcadeRun.New(clubs, clubs[^1].Id, ArcadeRule.SuddenDeath, 3);
        int games = 0;
        while (!run.IsOver && games++ < 40) run.Record(Result(run, win: true));
        Check(run.Champion && !run.Lost && run.Stage == clubs.Count - 1 && run.Games.Count == clubs.Count - 1, $"beating all {clubs.Count - 1} clubs ends the run as champions");
    }

    [Test]
    public void SaveAndContinue() => SaveAndContinue(WorldBuilder.Clubs, new Settings(), SavePath);

    void SaveAndContinue(IReadOnlyList<ClubData> clubs, Settings settings, string path)
    {
        string user = clubs[2].Id;
        var run = ArcadeSession.StartNew(clubs, user, ArcadeRule.ThreeLives, seed: 9, path: path);
        Check(ArcadeSave.Exists(path) && ArcadeSession.CanContinue(path), "a new run is saved at once and can be continued");
        var setup = ArcadeSession.NextSetup()!;

        // the match is lost 2-3 in a shootout and ends through the system, as in a real match
        var world = new World(setup, settings);
        var system = new ArcadeSystem();
        void Step(Action<World>? publish = null)
        {
            world.Events.Clear();
            publish?.Invoke(world);
            system.Update(world, Game.Step);
            world.Time += Game.Step;
        }
        world.Match.Period = 4;
        world.Match.Phase = MatchPhase.Shootout;
        Step();
        Check(run.Games.Count == 0, "nothing is recorded before MatchEnd");
        world.Match.Score[(int)setup.Inputs[0].Side] = 2;
        world.Match.Score[1 - (int)setup.Inputs[0].Side] = 3;
        world.Match.Phase = MatchPhase.Final;
        Step(w => w.Events.Publish(new MatchEvent(MatchEventKind.MatchEnd, w.Time, setup.Inputs[0].Side == Side.Home ? Side.Away : Side.Home)));
        Check(run.Games.Count == 1 && run.Games[0] is { Won: false, GoalsFor: 2, GoalsAgainst: 3 } && run.Games[0].Result.Kind == ResultKind.Shootout, "MatchEnd records a 2-3 shootout loss");
        Check(run.LivesLeft == 2, "the loss costs a life");
        Step(w => w.Events.Publish(new MatchEvent(MatchEventKind.MatchEnd, w.Time, Side.Home)));
        Check(run.Games.Count == 1, "a second MatchEnd records nothing");

        // restart: nothing in memory, read the save back
        string before = File.ReadAllText(path);
        ArcadeSession.End();
        var resumed = ArcadeSession.Continue(clubs, path);
        Check(resumed != null && resumed.Games.SequenceEqual(run.Games) && resumed.Opponents.SequenceEqual(run.Opponents), "continuing after a restart reads back the game and the order");
        Check(Same(resumed!.UserClub, user) && resumed.Rule == ArcadeRule.ThreeLives && resumed.LivesLeft == 2, "the save keeps the club, the rule and the lives");
        var next = ArcadeSession.NextSetup();
        Check(next != null && Same(next.Home.Id, setup.Home.Id) && Same(next.Away.Id, setup.Away.Id), "after the restart the same club is next");
        Check(Same(File.ReadAllText(path), before), "continuing does not rewrite the save");

        // two more losses end the run; the finished run still loads, for its summary
        ArcadeSession.Finish(Result(resumed, win: false));
        ArcadeSession.NextSetup();
        ArcadeSession.Finish(Result(resumed, win: false));
        Check(resumed.IsOver && !ArcadeSession.CanContinue(path), "the third loss ends the run and Continue is no longer offered");
        ArcadeSession.End();
        Check(ArcadeSession.Continue(clubs, path) is { Lost: true, Stage: 0 } && ArcadeSession.NextSetup() == null, "a finished run reads back over, with no next game");

        // a match that is not the arcade game records nothing
        var other = new World(WorldBuilder.DefaultSetup, settings);
        other.Events.Publish(new MatchEvent(MatchEventKind.MatchEnd, other.Time, Side.Home));
        new ArcadeSystem().Update(other, Game.Step);
        Check(ArcadeSession.Current!.Games.Count == 3, "a match that is not an arcade game records nothing");
    }

    [Test]
    public void BadSave()
    {
        string path = SavePath;
        ArcadeSession.End();
        File.WriteAllText(path, "{ not json");
        Check(ArcadeSave.Read(path) == null && !ArcadeSession.CanContinue(path), "a broken save reads as no save");
        ArcadeSave.Delete(path);
        Check(!ArcadeSave.Exists(path), "a deleted save is gone");
    }
}
