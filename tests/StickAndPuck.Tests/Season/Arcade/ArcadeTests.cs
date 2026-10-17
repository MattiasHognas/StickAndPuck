using System.Globalization;
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
    /// <summary>The game state the test plays in, fresh for each test.</summary>
    readonly AppState app = new();

    static readonly FixtureResult HomeWin = new(3, 1, ResultKind.Regulation);
    static readonly FixtureResult AwayWin = new(1, 3, ResultKind.Regulation);

    readonly string folder = Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"sap-arcadetest-{Environment.ProcessId}-{Guid.NewGuid():N}"));
    string SavePath => Path.Combine(folder, "arcade.json");

    [Before(Test)]
    public void SaveFolder() => Directory.CreateDirectory(folder);

    [After(Test)]
    public void EndRun()
    {
        app.Arcade.End();
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");

    /// <summary>Gets the next match's setup, failing loudly when the run is over.</summary>
    static MatchSetup Must(NextMatch next) => next is NextMatch.Play play ? play.Setup : throw new InvalidOperationException("expected a match to play");

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
        Check(run.Opponents.Count == clubs.Count - 1 && run.Opponents.Distinct(StringComparer.Ordinal).Count() == run.Opponents.Count, string.Create(CultureInfo.InvariantCulture, $"the run meets each of the other {clubs.Count - 1} clubs once"));
        Check(!run.Opponents.Contains(user), "the human's club is not an opponent");
        var strength = run.Opponents.Select(id => ArcadeRun.Strength(clubs.First(c => Same(c.Id, id)))).ToList();
        Check(strength.Zip(strength.Skip(1)).All(p => p.Second >= p.First - 2), "opponents come weakest first");
        Check(run.Opponents.SequenceEqual(ArcadeRun.New(clubs, user, ArcadeRule.ThreeLives, 5).Opponents, StringComparer.Ordinal), "the same seed gives the same order");
        var setup = Must(run.NextSetup(clubs));
        Check(Same(setup.Home.Id, user) && Same(setup.Away.Id, run.Opponents[0]) && setup.Inputs[0] is { Device: InputDevice.KeyboardMouse, Side: Side.Home } && setup.Overtime == OvertimeRule.Shl,
            "game 1: the human at home against the first club, SHL overtime");
        run.Record(Result(run, win: true));
        setup = Must(run.NextSetup(clubs));
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
        var beaten = run.NextOpponent;
        run.Record(Result(run, win: false, ResultKind.Shootout));
        Check(run.IsOver && run.Lost && !run.Champion, "sudden death: a shootout loss ends the run");
        Check(run.Stage == 2 && run.Games.Count == 3 && beaten.Is(b => Same(run.Games[^1].Opponent, b)), "the summary has two wins and the loss to the third club");
        Check(!run.NextOpponent.HasValue && run.NextSetup(clubs) is not NextMatch.Play, "a finished run has no next game");
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
        var first = run.NextOpponent;
        run.Record(Result(run, win: false));
        Check(!run.IsOver && run.LivesLeft == 2 && run.NextOpponent == first, "first loss: two lives left, the same club again");
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
        var run = app.Arcade.StartNew(clubs, user, ArcadeRule.ThreeLives, seed: 9, path: path);
        Check(ArcadeSave.Exists(path) && app.Arcade.CanContinue(path), "a new run is saved at once and can be continued");
        var setup = Must(app.Arcade.NextSetup());

        // the match is lost 2-3 in a shootout and ends through the system, as in a real match
        var world = new World(setup, settings) { App = app };
        var system = new ArcadeSystem();
        void Step(Action<World> publish)
        {
            world.Events.Clear();
            publish(world);
            system.Update(world, Game.Step);
            world.Time += Game.Step;
        }
        world.Match.Period = 4;
        world.Match.Phase = MatchPhase.Shootout;
        Step(_ => { });
        Check(run.Games.Count == 0, "nothing is recorded before MatchEnd");
        world.Match.Score[(int)setup.Inputs[0].Side] = 2;
        world.Match.Score[1 - (int)setup.Inputs[0].Side] = 3;
        world.Match.Phase = MatchPhase.Final;
        Step(w => w.Events.Publish(new MatchWon(w.Time, setup.Inputs[0].Side == Side.Home ? Side.Away : Side.Home)));
        Check(run.Games.Count == 1 && run.Games[0] is { Won: false, GoalsFor: 2, GoalsAgainst: 3 } && run.Games[0].Result.Kind == ResultKind.Shootout, "MatchEnd records a 2-3 shootout loss");
        Check(run.LivesLeft == 2, "the loss costs a life");
        Step(w => w.Events.Publish(new MatchWon(w.Time, Side.Home)));
        Check(run.Games.Count == 1, "a second MatchEnd records nothing");

        // restart: nothing in memory, read the save back
        string before = File.ReadAllText(path);
        app.Arcade.End();
        var reread = app.Arcade.Continue(clubs, path);
        Check(reread.Is(x => x.Games.SequenceEqual(run.Games) && x.Opponents.SequenceEqual(run.Opponents, StringComparer.Ordinal)), "continuing after a restart reads back the game and the order");
        var resumed = Must(reread);
        Check(Same(resumed.UserClub, user) && resumed.Rule == ArcadeRule.ThreeLives && resumed.LivesLeft == 2, "the save keeps the club, the rule and the lives");
        Check(app.Arcade.NextSetup() is NextMatch.Play n && Same(n.Setup.Home.Id, setup.Home.Id) && Same(n.Setup.Away.Id, setup.Away.Id), "after the restart the same club is next");
        Check(Same(File.ReadAllText(path), before), "continuing does not rewrite the save");

        // two more losses end the run; the finished run still loads, for its summary
        app.Arcade.Finish(Result(resumed, win: false));
        app.Arcade.NextSetup();
        app.Arcade.Finish(Result(resumed, win: false));
        Check(resumed.IsOver && !app.Arcade.CanContinue(path), "the third loss ends the run and Continue is no longer offered");
        app.Arcade.End();
        Check(app.Arcade.Continue(clubs, path).Is(x => x.Lost && x.Stage == 0) && app.Arcade.NextSetup() is not NextMatch.Play, "a finished run reads back over, with no next game");

        // a match that is not the arcade game records nothing
        var other = new World(WorldBuilder.DefaultSetup, settings) { App = app };
        other.Events.Publish(new MatchWon(other.Time, Side.Home));
        new ArcadeSystem().Update(other, Game.Step);
        Check(Must(app.Arcade.Current).Games.Count == 3, "a match that is not an arcade game records nothing");
    }

    [Test]
    public void BadSave()
    {
        string path = SavePath;
        app.Arcade.End();
        File.WriteAllText(path, "{ not json");
        Check(!ArcadeSave.Read(path).HasValue && !app.Arcade.CanContinue(path), "a broken save reads as no save");
        ArcadeSave.Delete(path);
        Check(!ArcadeSave.Exists(path), "a deleted save is gone");
    }
}
