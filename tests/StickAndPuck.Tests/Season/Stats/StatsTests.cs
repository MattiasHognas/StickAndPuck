using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The goals, assists, shots and saves read from a match's event log, the player of the game, the
/// lines of a game settled on paper, that the season's stats survive the autosave and a restart (and
/// that an old save without them fills in), and the arcade run's stats and best-runs board across
/// runs, with saves in a temporary folder. Formerly SAP_SCENE=statstest.
/// </summary>
public sealed class StatsTests : CheckedTest
{
    /// <summary>The game state the test plays in, fresh for each test.</summary>
    readonly AppState app = new();

    static readonly string Folder = Path.Combine(Path.GetTempPath(), $"sap-statstest-{Environment.ProcessId}");
    static readonly string SeasonPath = Path.Combine(Folder, "season.json");
    static readonly string ArcadePath = Path.Combine(Folder, "arcade.json");

    [After(Test)]
    public void EndSessions()
    {
        app.Season.End();
        app.Arcade.End();
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");

    /// <summary>Gets the next match's setup, failing loudly when the run is over.</summary>
    static MatchSetup Must(NextMatch next) => next is NextMatch.Play play ? play.Setup : throw new InvalidOperationException("expected a match to play");

    /// <summary>A world with the starters on, and a way to play one step with some events.</summary>
    (World World, Action<Action<World>> Step) Rink(MatchSetup setup, Settings settings, params ISystem[] systems)
    {
        var world = new WorldBuilder().Setup(setup).Settings(settings).App(app).NotBegun().Build();
        void Step(Action<World> act)
        {
            world.Events.Clear();
            act(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step;
        }
        return (world, Step);
    }

    static Player Skater(World w, Side side, int n) => w.OnIce(side).Where(p => !p.IsGoalie).ElementAt(n);

    static Player Goalie(World w, Side side) => w.OnIce(side).First(p => p.IsGoalie);

    /// <summary>One rink with a tracker on it, and the players the checks use.</summary>
    sealed record Rig(World W, Action<Action<World>> Step, MatchStatTracker Tracker, Player A, Player B, Player C, Player D, Player G)
    {
        public MatchStatLine L(Player p) => Tracker.Sheet.Lines.First(l => l.Number == p.Data.Number && l.Side == p.Side);

        public void Touch(Player p) => Step(x => x.Puck.LastTouch = p.Id);

        public void Goal(Side side, Player p) => Step(x => { x.Match.Score[(int)side]++; x.Events.Publish(new GoalScored(x.Time, side, p.Id, default, 0)); });

        public void Faceoff() => Step(x => x.Events.Publish(new PuckDropped(x.Time, default)));
    }

    [Test]
    public void EventLog()
    {
        var settings = new Settings();
        var tracker = new MatchStatTracker();
        var (w, step) = Rink(WorldBuilder.DefaultSetup, settings, new Observer(tracker));
        var rig = new Rig(w, step, tracker, Skater(w, Side.Home, 0), Skater(w, Side.Home, 1), Skater(w, Side.Home, 2), Skater(w, Side.Away, 0), Goalie(w, Side.Away));
        step(_ => { });
        Assists(rig);
        Deflections(rig);
        HitsSavesAndStar(rig);
    }

    void Assists(Rig r)
    {
        // c to b to a, a scores: a goal, b and c assists
        r.Step(x => { x.Puck.LastTouch = r.C.Id; x.Events.Publish(new PassMade(x.Time, Side.Home, r.C.Id, default, 0)); });
        r.Step(x => { x.Puck.LastTouch = r.B.Id; x.Events.Publish(new PassMade(x.Time, Side.Home, r.B.Id, default, 0)); });
        r.Step(x => { x.Puck.LastTouch = r.A.Id; x.Events.Publish(new ShotTaken(x.Time, Side.Home, r.A.Id, default, 30)); });
        r.Goal(Side.Home, r.A);
        Check(r.L(r.A) is { Goals: 1, Shots: 1, Assists: 0 }, "the scorer gets the goal and the shot");
        Check(r.L(r.B).Assists == 1 && r.L(r.C).Assists == 1, "the two teammates who touched the puck before get the assists");
        Check(r.L(r.D).Assists == 0 && r.L(r.A).Assists == 0, "nobody else gets an assist");

        // d (other side) touches between: the chain breaks, a goal with one assist
        r.Faceoff();
        r.Touch(r.C);
        r.Touch(r.D);
        r.Touch(r.B);
        r.Touch(r.A);
        r.Goal(Side.Home, r.A);
        Check(r.L(r.A).Goals == 2 && r.L(r.B).Assists == 2 && r.L(r.C).Assists == 1, "an opponent's touch breaks the assist chain");
    }

    void Deflections(Rig r)
    {
        // a shot that deflects in off d: the last attacker to touch gets the goal, as an own goal would
        r.Faceoff();
        r.Touch(r.B);
        r.Step(x => { x.Puck.LastTouch = r.D.Id; x.Match.Score[0]++; x.Events.Publish(new GoalScored(x.Time, Side.Home, r.D.Id, default, 0)); });
        Check(r.L(r.B).Goals == 1 && r.L(r.D).Goals == 0, "a puck in off a defender is the last attacker's goal");

        // a touch too long ago is no assist
        r.Faceoff();
        r.Touch(r.C);
        r.W.Time += MatchStatTracker.AssistWindow + 1;
        r.Touch(r.A);
        r.Goal(Side.Home, r.A);
        Check(r.L(r.A).Goals == 3 && r.L(r.C).Assists == 1, $"a touch more than {MatchStatTracker.AssistWindow:0} s before the goal is no assist");
    }

    void HitsSavesAndStar(Rig r)
    {
        // hits, saves, and a shootout goal that counts for nobody
        r.Step(x => x.Events.Publish(new CheckLanded(x.Time, Side.Away, r.D.Id, default, 2)));
        for (int i = 0; i < 5; i++) r.Step(x => x.Events.Publish(new ShotSaved(x.Time, Side.Away, r.G.Id, default, 0)));
        r.Step(x => x.Match.Phase = MatchPhase.Shootout);
        r.Step(x => { x.Puck.LastTouch = r.D.Id; x.Events.Publish(new ShotTaken(x.Time, Side.Away, r.D.Id, default, 0)); x.Events.Publish(new GoalScored(x.Time, Side.Away, r.D.Id, default, 0)); });
        Check(r.L(r.D) is { Hits: 1, Goals: 0, Shots: 0 } && r.L(r.G).Saves == 5, "hits and saves count, shootout shots and goals do not");

        r.W.Match.Score[0] = 4;
        r.W.Match.Score[1] = 1;
        var done = r.Tracker.Finish();
        Check(done.PlayerOfGame.Is(star => star.Number == r.A.Data.Number && star.Side == Side.Home), $"player of the game is the hat-trick scorer ({done.PlayerOfGame.Select(x => x.Label).Or("none")})");
        Check(done.Lines.Count == r.W.Players.Count && done.Lines.Count(l => l.Played) == r.W.Players.Count(p => p.OnIce), "every dressed player has a line, the ones on the ice played");
    }

    [Test]
    public void Shutout()
    {
        // a shutout goalie beats a one-goal scorer
        var quiet = new MatchStatSheet { Home = "h", Away = "a", HomeGoals = 1, AwayGoals = 0 };
        quiet.Lines.Add(new MatchStatLine { Club = "h", Side = Side.Home, Number = 9, Name = "S", Position = PlayingPosition.C, Played = true, Goals = 1, Shots = 2 });
        quiet.Lines.Add(new MatchStatLine { Club = "h", Side = Side.Home, Number = 1, Name = "G", Position = PlayingPosition.G, Played = true, Saves = 30 });
        quiet.PickPlayerOfGame();
        Check(quiet.PlayerOfGame.Is(x => x.Number == 1), "a 30-save shutout is player of the game over a single goal");
    }

    [Test]
    public void Synthetic()
    {
        var clubs = WorldBuilder.Clubs;
        var result = new FixtureResult(4, 3, ResultKind.Shootout);
        var sheet = SyntheticStats.Sheet(clubs[0], clubs[1], result, 77);
        int homeGoals = sheet.Lines.Where(l => l.Side == Side.Home).Sum(l => l.Goals);
        int awayGoals = sheet.Lines.Where(l => l.Side == Side.Away).Sum(l => l.Goals);
        Check(homeGoals == 3 && awayGoals == 3, $"a 4-3 shootout game credits 3 and 3 goals, the deciding goal to nobody ({homeGoals}, {awayGoals})");
        int assists = sheet.Lines.Sum(l => l.Assists);
        Check(assists >= 3 && assists <= 12, $"assists come with the goals ({assists})");
        Check(sheet.Lines.All(l => !l.IsGoalie || l.Saves == 0 || l.Played), "only the goalie who played makes saves");
        Check(sheet.Lines.Count(l => l.IsGoalie && l.Played) == 2, "one goalie a side plays");
        Check(sheet.Lines.Where(l => l.IsGoalie).Sum(l => l.Saves) > 20, "the goalies make a night's saves");
        Check(sheet.PlayerOfGame.HasValue, $"a player of the game is picked ({sheet.PlayerOfGame.Select(x => x.Label).Or("none")})");
        var again = SyntheticStats.Sheet(clubs[0], clubs[1], result, 77);
        Check(again.Lines.Select(l => (l.Number, l.Goals, l.Assists, l.Shots)).SequenceEqual(sheet.Lines.Select(l => (l.Number, l.Goals, l.Assists, l.Shots))), "the same seed gives the same lines");

        // over many games the scorers lead by their traits and defenders score less
        var table = new StatTable();
        for (int i = 0; i < 60; i++) table.Add(SyntheticStats.Sheet(clubs[0], clubs[1], new FixtureResult(3, 2, ResultKind.Regulation), i));
        var leaders = table.Leaders(5);
        Check(leaders.Count == 5 && leaders[0].Points >= leaders[4].Points, $"leaders sort by points ({string.Join(", ", leaders.Select(l => $"{l.Label} {l.Points}"))})");
        double forwards = table.Players.Where(p => p.Position != PlayingPosition.D && !p.IsGoalie).Average(p => p.Goals);
        double defenders = table.Players.Where(p => p.Position == PlayingPosition.D).Average(p => p.Goals);
        Check(forwards > defenders, $"forwards score more than defenders ({forwards:0.0} vs {defenders:0.0} a man)");
        Check(table.Goalies(1)[0].Games > 0 && table.GamesCredited == 60, "the goalies and the game count fill in");
    }

    /// <summary>
    /// The season's stats through the autosave and a restart, then the same save stripped of its
    /// stats as a save from before they existed: one test, as the old save is the one this writes.
    /// </summary>
    [Test]
    public void SeasonSaveAndReload()
    {
        var clubs = WorldBuilder.Clubs;
        SaveAndReload(clubs, new Settings(), SeasonPath);
        OldSave(clubs, SeasonPath);
    }

    void SaveAndReload(IReadOnlyList<ClubData> clubs, Settings settings, string path)
    {
        string user = clubs[2].Id;
        var season = app.Season.StartNew(clubs, user, seed: 11, path: path);
        var setup = Must(app.Season.NextSetup());
        var game = Must(app.Season.Playing);
        var (w, step) = Rink(setup, settings, new StatsSystem(), new ScheduleSystem(), new AutosaveSystem());
        var side = Same(game.Home, user) ? Side.Home : Side.Away;
        var a = Skater(w, side, 0);
        var b = Skater(w, side, 1);
        step(_ => { });
        step(x => x.Puck.LastTouch = b.Id);
        step(x => { x.Puck.LastTouch = a.Id; x.Match.Score[(int)side]++; x.Events.Publish(new GoalScored(x.Time, side, a.Id, default, 0)); });
        step(x => { x.Puck.LastTouch = a.Id; x.Match.Score[(int)side]++; x.Events.Publish(new GoalScored(x.Time, side, a.Id, default, 0)); });
        Check(season.Stats.Regular.Players.Count == 0, "nothing is credited before MatchEnd");
        w.Match.Phase = MatchPhase.Final;
        step(x => x.Events.Publish(new MatchWon(x.Time, side)));
        var line = season.Stats.Regular.Find(user, a.Data.Number);
        Check(line.Is(x => x.Goals == 2 && x.Games == 1) && season.Stats.Regular.Find(user, b.Data.Number).Is(x => x.Assists == 1), "MatchEnd credits the played game from the event log");
        Check(season.Stats.Credited.Contains(SeasonStats.GameKey(game.Number)) && season.Stats.Awards.Count == 1 && season.Stats.Awards[0].Number == a.Data.Number, "the game is marked credited and its player of the game kept");
        Check(season.Games.Count(g => g.Played) == 7 && season.Stats.Regular.GamesCredited == 1, "the round's other games are settled but not yet credited in the same step");
        step(_ => { });    // the step after: catch-up credits the six paper games and asks for another save
        Check(season.Stats.Regular.GamesCredited == 7 && season.Stats.Awards.Count == 7, "the next step credits the six games settled on paper");
        Check(!app.Season.Unsaved, "the autosave ran again after the catch-up");
        Check(season.Stats.Regular.Players.Count(p => p.Games > 0) >= 7 * 19, "every club in the round has players with a game");

        // restart: the lines come back from the save
        app.Season.End();
        var reread = app.Season.Continue(clubs, path);
        Check(reread.Is(x => x.Stats.Regular.GamesCredited == 7 && x.Stats.Credited.Count == 7), "the stats read back after a restart");
        var resumed = Must(reread);
        var back = resumed.Stats.Regular.Find(user, a.Data.Number);
        Check(back.Is(x => x.Goals == 2 && x.Assists == 0 && x.Games == 1 && x.Stars == 1 && Same(x.Name, a.Data.LastName)), $"the scorer's line survives the reload ({back.Select(x => $"{x.Label} {x.Goals}G, {x.Stars} star").Or("none")})");
        Check(resumed.Stats.Awards.Count == 7 && Same(resumed.Stats.Awards[0].Game, SeasonStats.GameKey(game.Number)), "the awards survive the reload");
        Check(resumed.Stats.CatchUp(resumed, clubs) == 0, "nothing is credited twice");
        Check(resumed.Stats.Regular.Leaders(3).Count == 3, $"leaders: {string.Join(", ", resumed.Stats.Regular.Leaders(3).Select(l => $"{l.Label} {l.Points}"))}");
    }

    void OldSave(IReadOnlyList<ClubData> clubs, string path)
    {
        // a save from before stats existed: the played games fill in from their scores on the next step
        app.Season.End();
        string json = File.ReadAllText(path);
        int at = json.IndexOf("\"stats\"", StringComparison.Ordinal);
        Check(at > 0, "the save carries the stats");
        var season = Must(SeasonSave.Read(path));
        season.Stats = new SeasonStats();
        SeasonSave.Write(season, path);
        var resumed = Must(app.Season.Continue(clubs, path));
        Check(resumed.Stats.Regular.GamesCredited == 0, "an old save reads with empty stats");
        var w = new World(WorldBuilder.DefaultSetup, new Settings()) { App = app };
        new StatsSystem().Update(w, Game.Step);
        Check(resumed.Stats.Regular.GamesCredited == 7 && resumed.Stats.Awards.Count == 7 && app.Season.Unsaved, "the first step credits every played game from its score and asks for a save");
    }

    [Test]
    public void Arcade() => Arcade(WorldBuilder.Clubs, new Settings(), ArcadePath);

    void Arcade(IReadOnlyList<ClubData> clubs, Settings settings, string path)
    {
        string user = clubs[4].Id;
        var run = app.Arcade.StartNew(clubs, user, ArcadeRule.SuddenDeath, 3, path);
        var setup = Must(app.Arcade.NextSetup());
        var (w, step) = Rink(setup, settings, new StatsSystem(), new ArcadeSystem());
        var a = Skater(w, Side.Home, 0);
        step(x => { x.Puck.LastTouch = a.Id; x.Match.Score[0]++; x.Events.Publish(new GoalScored(x.Time, Side.Home, a.Id, default, 0)); });
        w.Match.Phase = MatchPhase.Final;
        step(x => x.Events.Publish(new MatchWon(x.Time, Side.Home)));
        Check(run.Stats.Find(user, a.Data.Number).Is(x => x.Goals == 1 && x.Games == 1) && run.Awards.Count == 1, "an arcade game credits the run's stats and its player of the game");
        Check(run.Games.Count == 1 && run.Records.Count == 0, "a run still going is not on the board");

        // lose the next game: sudden death ends the run, which goes on the board and is saved
        setup = Must(app.Arcade.NextSetup());
        (w, step) = Rink(setup, settings, new StatsSystem(), new ArcadeSystem());
        w.Match.Score[(int)setup.Inputs[0].Side == 0 ? 1 : 0] = 2;
        w.Match.Phase = MatchPhase.Final;
        step(x => x.Events.Publish(new MatchWon(x.Time, Side.Home)));
        Check(run.IsOver && run.Records.Count == 1 && run.Records[0] is { Beaten: 1, Losses: 1, Champion: false } && run.Place == 1, "the finished run is first on the board");
        Check(run.Records[0].TopPoints == 1 && run.Records[0].TopScorer.Contains(a.Data.LastName, StringComparison.Ordinal), $"the record names the run's top scorer ({run.Records[0].TopScorer})");

        app.Arcade.End();
        var reloaded = Must(app.Arcade.Continue(clubs, path));
        Check(reloaded.Records.Count == 1 && reloaded.Stats.Find(user, a.Data.Number).Is(x => x.Goals == 1) && reloaded.Awards.Count == 2, "the board, the lines and the two games' awards read back from arcade.json");

        // a new run keeps the board; a better run ranks above
        var next = app.Arcade.StartNew(clubs, user, ArcadeRule.ThreeLives, 4, path);
        Check(next.Records.Count == 1 && next.Stats.Players.Count == 0, "a new run starts with the board and empty lines");
        for (int i = 0; i < 3; i++)
        {
            app.Arcade.NextSetup();
            app.Arcade.Finish(next.NextAtHome ? new FixtureResult(2, 0, ResultKind.Regulation) : new FixtureResult(0, 2, ResultKind.Regulation));
        }
        for (int i = 0; i < 3; i++)
        {
            app.Arcade.NextSetup();
            app.Arcade.Finish(next.NextAtHome ? new FixtureResult(0, 1, ResultKind.Regulation) : new FixtureResult(1, 0, ResultKind.Regulation));
        }
        Check(next.IsOver && next.Records.Count == 2 && next.Records[0].Beaten == 3 && next.Place == 1, "three wins then three losses ranks above the one-win run");
        var champion = new ArcadeRecord(user, ArcadeRule.SuddenDeath, 13, true, 0, 40, 10, DateTime.UtcNow, "", 0);
        var board = ArcadeRecord.Add(next.Records, champion);
        Check(board[0] == champion && board.Count == 3, "a champion run tops the board");
        for (int i = 0; i < 12; i++) board = ArcadeRecord.Add(board, champion with { Beaten = i, Champion = false, Ended = DateTime.UtcNow.AddMinutes(i) });
        Check(board.Count == ArcadeRecord.Kept, $"the board keeps {ArcadeRecord.Kept} runs");
    }

    /// <summary>Feeds a tracker from a system list, so the checks step it like Game.cs would.</summary>
    sealed class Observer(MatchStatTracker tracker) : ISystem
    {
        public void Update(World world, float dt)
        {
            tracker.Observe(world);
        }
    }
}
