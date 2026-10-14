using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// Season mode: the schedule, the points, the table, the autosave after a match and continuing after
/// a restart, with a save in a temporary folder. Formerly SAP_SCENE=seasontest.
/// </summary>
public sealed class SeasonTests : CheckedTest
{
    /// <summary>The game state the test plays in, fresh for each test.</summary>
    readonly AppState app = new();

    readonly string folder = Path.Combine(Path.GetTempPath(), $"sap-seasontest-{Environment.ProcessId}-{Guid.NewGuid():N}");

    string SavePath => Path.Combine(folder, "season.json");

    [After(Test)]
    public void EndSession()
    {
        app.Season.End();
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");

    /// <summary>Gets the next match's setup, failing loudly when the run is over.</summary>
    static MatchSetup Must(NextMatch next) => next is NextMatch.Play play ? play.Setup : throw new InvalidOperationException("expected a match to play");

    [Test]
    public void Schedule()
    {
        var ids = WorldBuilder.Clubs.Select(c => c.Id).ToList();
        var games = SeasonSchedule.Build(ids, 42);
        int n = ids.Count, rounds = games.Max(g => g.Round) + 1;
        Check(n == 14, $"14 SHL clubs load ({n})");
        Check(rounds == 52 && games.Count == 52 * 7, $"52 rounds of 7 games ({rounds} rounds, {games.Count} games)");
        Check(ids.TrueForAll(id => games.Count(g => g.Involves(id)) == 52), "every club plays 52 games");
        Check(ids.TrueForAll(id => games.Count(g => Same(g.Home, id)) == 26), "every club has 26 home games");
        Check(Enumerable.Range(0, rounds).All(r => games.Where(g => g.Round == r).SelectMany(g => new[] { g.Home, g.Away }).Distinct().Count() == n),
            "every club plays once in every round");
        bool pairs = ids.TrueForAll(a => ids.TrueForAll(b => Same(a, b) || (games.Count(g => Same(g.Home, a) && Same(g.Away, b)) == 2)));
        Check(pairs, "every pair meets four times, twice at each rink");
        bool backToBack = games.Any(g => games.Any(o => o.Round == g.Round + 1 && o.Involves(g.Home) && o.Involves(g.Away)));
        Check(!backToBack, "no pair meets in two rounds running");
        Check(games.Select((g, i) => g.Number == i).All(x => x), "games are numbered in order");
        var again = SeasonSchedule.Build(ids, 42);
        Check(again.SequenceEqual(games), "the same seed gives the same schedule");
        Check(!SeasonSchedule.Build(ids, 43).SequenceEqual(games), "another seed gives another schedule");
    }

    [Test]
    public void Points()
    {
        Check(new FixtureResult(3, 1, ResultKind.Regulation) is { HomePoints: 3, AwayPoints: 0 }, "regulation win 3, loss 0");
        Check(new FixtureResult(2, 3, ResultKind.Overtime) is { HomePoints: 1, AwayPoints: 2 }, "overtime win 2, loss 1");
        Check(new FixtureResult(4, 3, ResultKind.Shootout) is { HomePoints: 2, AwayPoints: 1 }, "shootout win 2, loss 1");
        var games = new List<Fixture>
        {
            new(0, 0, "a", "b", new FixtureResult(3, 1, ResultKind.Regulation)),
            new(1, 0, "c", "d", new FixtureResult(2, 1, ResultKind.Shootout)),
            new(2, 1, "b", "c", new FixtureResult(5, 0, ResultKind.Regulation)),
            new(3, 1, "d", "a"),
        };
        var table = Standings.Compute(["a", "b", "c", "d"], games);
        string order = string.Join(",", table.Select(r => $"{r.ClubId}{r.Points}"));
        Check(Same(order, "b3,a3,c2,d1"), $"table by points then goal difference ({order})");
        var c = table.Single(r => Same(r.ClubId, "c"));
        Check(c is { Played: 2, OvertimeWins: 1, Losses: 1, GoalsFor: 2, GoalsAgainst: 6 }, "a club's line counts games, wins, losses and goals");
    }

    [Test]
    public void AutosaveAndContinue()
    {
        var clubs = WorldBuilder.Clubs;
        string path = SavePath;
        string user = clubs[^1].Id;
        var season = app.Season.StartNew(clubs, user, seed: 7, path: path);
        Check(SeasonSave.Exists(path), "a new season is saved at once");
        var firstSetup = app.Season.NextSetup();
        var firstGame = app.Season.Playing;
        Check(firstSetup is NextMatch.Play && firstGame.Is(g => g.Round == 0 && g.Involves(user)), "the first game is the human's round 1 game");
        var game = Must(firstGame);
        var side = Same(game.Home, user) ? Side.Home : Side.Away;
        var setup = Must(firstSetup);
        Check(setup.Inputs[0] is { Device: InputDevice.KeyboardMouse } && setup.Inputs[0].Side == side && setup.Overtime == OvertimeRule.Shl,
            "keyboard and mouse play the human's club, SHL overtime");

        // the match ends 2-2 after regulation and goes to a shootout the home side wins 3-2
        var world = new World(setup, new Settings()) { App = app };
        var schedule = new ScheduleSystem();
        var autosave = new AutosaveSystem();
        void Step(Action<World> publish)
        {
            world.Events.Clear();
            publish(world);
            schedule.Update(world, Game.Step);
            autosave.Update(world, Game.Step);
            world.Time += Game.Step;
        }
        world.Match.Period = 4;
        world.Match.Phase = MatchPhase.Shootout;
        Step(_ => { });
        Check(!season.Games[game.Number].Played, "nothing is recorded before MatchEnd");
        world.Match.Score[0] = 3;
        world.Match.Score[1] = 2;
        world.Match.Phase = MatchPhase.Final;
        Step(w => w.Events.Publish(new MatchWon(w.Time, Side.Home)));
        Check(season.Games[game.Number].Result == new FixtureResult(3, 2, ResultKind.Shootout), "MatchEnd records 3-2 after a shootout");
        Check(season.Round(0).All(g => g.Played) && !season.Round(1).Any(g => g.Played), "the round's other six games are settled, the next round is not");
        Check(!app.Season.Unsaved, "the autosave ran after the match");

        Restart(clubs, path, season, user);
    }

    void Restart(IReadOnlyList<ClubData> clubs, string path, SeasonState season, string user)
    {
        // restart: nothing in memory, read the save back
        string before = File.ReadAllText(path);
        app.Season.End();
        var reread = app.Season.Continue(clubs, path);
        Check(reread.Is(x => x.Games.SequenceEqual(season.Games)), "continuing after a restart reads back every result");
        var resumed = Must(reread);
        Check(Same(resumed.UserClub, user) && resumed.Seed == 7, "the save keeps the human's club and the seed");
        Check(app.Season.NextSetup() is NextMatch.Play && app.Season.Playing.Is(x => x.Round == 1), "after the restart the next game is in round 2");
        Check(Same(File.ReadAllText(path), before), "continuing does not rewrite the save");

        // a game that is not a season game records nothing
        app.Season.End();
        app.Season.Continue(clubs, path);
        var other = new World(WorldBuilder.DefaultSetup, new Settings()) { App = app };
        other.Events.Publish(new MatchWon(other.Time, Side.Home));
        new ScheduleSystem().Update(other, Game.Step);
        Check(Must(app.Season.Current).Games.Count(g => g.Played) == 7 && !app.Season.Unsaved, "a match that is not a season game records nothing");
    }

    [Test]
    public void WholeSeason()
    {
        var clubs = WorldBuilder.Clubs;
        string path = SavePath;
        var season = app.Season.StartNew(clubs, clubs[0].Id, seed: 2026, path: path);
        int games = 0;
        while (app.Season.NextSetup() is NextMatch.Play)
        {
            // home goals 0 to 4 against 2; the 2-2 games go to a shootout
            int home = games % 5;
            app.Season.Finish(home == 2 ? new FixtureResult(3, 2, ResultKind.Shootout) : new FixtureResult(home, 2, ResultKind.Regulation));
            SeasonSave.Write(Must(app.Season.Current), path);
            games++;
            if (games > 60) break;
        }
        Check(games == 52 && season.IsOver, $"the human plays 52 games and the season ends ({games})");
        var table = season.Table;
        Check(table.Count == 14 && table.All(r => r.Played == 52), "every club has played 52 games");
        Check(table.Sum(r => r.Wins + r.OvertimeWins) == season.Games.Count && table.Sum(r => r.Losses + r.OvertimeLosses) == season.Games.Count, "every game has one winner and one loser");
        Check(table.Sum(r => r.GoalsFor) == table.Sum(r => r.GoalsAgainst), "goals for and against balance");
        Check(table.Zip(table.Skip(1)).All(p => p.First.Points >= p.Second.Points), "the table is sorted by points");
        double perSide = season.Games.Where(g => !g.Involves(season.UserClub)).Average(g => (Must(g.Result).HomeGoals + Must(g.Result).AwayGoals) / 2.0);
        Check(perSide is > 2 and < 4.5, $"simulated games score about three a side ({perSide:0.00})");
        var top = clubs.FirstOrNone(c => Same(c.Id, table[0].ClubId));
        Check(top.HasValue, $"leader after 52 rounds: {top.Select(c => c.Name).Or("none")} {table[0].Points} pts");
        var again = QuickSim.Play(clubs[1], clubs[2], 99);
        Check(again == QuickSim.Play(clubs[1], clubs[2], 99), "a simulated game is the same every time");

        app.Season.End();
        Check(app.Season.Continue(clubs, path).Is(x => x.IsOver && x.Table.SequenceEqual(table)), "a finished season reads back with the same table");
        Check(app.Season.NextSetup() is not NextMatch.Play, "a finished season has no next game");
    }

    /// <summary>
    /// Sim this game, then a broken save. The broken save is written into the folder the simmed
    /// season's save created, so the two stay in one test, in this order.
    /// </summary>
    [Test]
    public void SimGameThenBadSave()
    {
        SimGame(WorldBuilder.Clubs, SavePath);
        BadSave(SavePath);
    }

    void SimGame(IReadOnlyList<ClubData> clubs, string path)
    {
        // "Sim this game" settles the human's game on paper, records it like a played game and saves
        app.Season.End();
        var season = app.Season.StartNew(clubs, clubs[3].Id, seed: 11, path: path);
        string before = File.ReadAllText(path);
        var game = Must(season.NextGame);
        var simmed = app.Season.SimCurrentGame();
        Check(simmed.Is(x => !x.Playoff && Same(x.Home, game.Home) && Same(x.Away, game.Away)), "sim this game settles the human's next game");
        var recorded = season.Games[game.Number];
        Check(recorded.Played && recorded.Result == Must(simmed).Result && Must(recorded.Result).HomeGoals != Must(recorded.Result).AwayGoals, "the result is recorded in the fixture with a winner");
        Check(recorded.Result == QuickSim.Play(clubs.First(c => Same(c.Id, game.Home)), clubs.First(c => Same(c.Id, game.Away)), season.GameSeed(game.Number)),
            "the simmed game uses the same simulation as the round's other games");
        Check(season.Round(0).All(g => g.Played) && !season.Round(1).Any(g => g.Played), "the rest of the round is settled too");
        var table = season.Table;
        Check(table.All(r => r.Played == 1) && table.Sum(r => r.Points) == 7 * 3, "the table updates: every club has played once, 21 points handed out");
        Check(!app.Season.Playing.HasValue && !app.Season.Unsaved && !Same(File.ReadAllText(path), before), "the sim is saved at once and no game is left marked as playing");
        Check(season.NextGame.Is(x => x.Round == 1), "the next game is in round 2");

        // the whole regular season can be simmed, then the playoffs start and sim too
        int count = 1;
        while (!season.IsOver && app.Season.SimCurrentGame().HasValue && ++count < 60) { }
        Check(count == 52 && season.IsOver, $"simming every game ends the regular season ({count} sims)");
        var playoffSim = app.Season.SimCurrentGame();
        var playoffs = season.Playoffs;
        if (!playoffs.TryGet(out var bracket) || !bracket.UserQualified)
        {
            Check(playoffs.HasValue && !playoffSim.HasValue, "a club out of the playoffs has nothing to sim");
        }
        else
        {
            Check(playoffSim.Is(x => x.Playoff && x.Result.Kind != ResultKind.Shootout) && bracket.Rounds.Any(r => r.Series.Any(s => s.Games.Count > 0 && s.Involves(season.UserClub))),
                $"the first playoff game is simmed and recorded in the series ({playoffSim.Select(x => x.Stage).Or("")})");
            int more = 0;
            while (app.Season.SimCurrentGame().HasValue && ++more < 40) { }
            Check(bracket.IsOver || !bracket.UserAlive, $"simming on plays the human's playoffs out ({more + 1} playoff sims, champion {bracket.Champion.Or("none yet")})");
        }
        app.Season.End();
        var champion = playoffs.Select(x => x.Champion);
        Check(app.Season.Continue(clubs, path).Is(x => x.Games.SequenceEqual(season.Games) && x.Playoffs.Select(y => y.Champion) == champion), "the simmed season reads back from the save");
    }

    void BadSave(string path)
    {
        app.Season.End();
        File.WriteAllText(path, "{ not json");
        Check(!SeasonSave.Read(path).HasValue, "a broken save reads as no save");
        SeasonSave.Delete(path);
        Check(!SeasonSave.Exists(path), "a deleted save is gone");
    }
}
