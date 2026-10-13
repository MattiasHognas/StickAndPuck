using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// The season being played in this run, if any, and which of its games the current match is.
/// A scene (or W11's start screen) calls <see cref="Continue(IReadOnlyList{ClubData})"/> or <see cref="StartNew(IReadOnlyList{ClubData}, string)"/>, builds
/// the world from <see cref="NextSetup"/>, and <see cref="ScheduleSystem"/> records the result
/// when that match ends; <see cref="AutosaveSystem"/> then writes the save.
/// </summary>
public static class SeasonSession
{
    /// <summary>Gets the season being played, or None outside season mode.</summary>
    public static Maybe<SeasonState> Current { get; private set; }

    /// <summary>Gets the game the current match is, or None when the match is not a season game.</summary>
    public static Maybe<Fixture> Playing { get; private set; }

    /// <summary>Gets the clubs the season was loaded with.</summary>
    public static IReadOnlyList<ClubData> Clubs { get; private set; } = [];

    /// <summary>Gets the save file this session writes.</summary>
    public static string SavePath { get; private set; } = "";

    /// <summary>Gets a value indicating whether the season changed since it was last saved.</summary>
    public static bool Unsaved { get; internal set; }

    /// <summary>Continues the season saved at <see cref="SeasonSave.DefaultPath"/>.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <returns>The season, or None when there is no save to continue.</returns>
    public static Maybe<SeasonState> Continue(IReadOnlyList<ClubData> clubs) => Continue(clubs, SeasonSave.DefaultPath);

    /// <summary>Continues the saved season.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="path">The save file.</param>
    /// <returns>The season, or None when there is no save to continue.</returns>
    public static Maybe<SeasonState> Continue(IReadOnlyList<ClubData> clubs, string path)
    {
        if (!SeasonSave.Read(path).TryGet(out var season)) return Maybe<SeasonState>.None;
        Use(season, clubs, path);
        Unsaved = false;
        return season;
    }

    /// <summary>Starts a new season with a random seed at <see cref="SeasonSave.DefaultPath"/>, replacing any save, and saves it at once.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <returns>The new season.</returns>
    public static SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub) => StartNew(clubs, userClub, Maybe<int>.None, SeasonSave.DefaultPath);

    /// <summary>Starts a new season at <see cref="SeasonSave.DefaultPath"/>, replacing any save, and saves it at once.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <param name="seed">The schedule seed, or None for a random one.</param>
    /// <returns>The new season.</returns>
    public static SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub, Maybe<int> seed) => StartNew(clubs, userClub, seed, SeasonSave.DefaultPath);

    /// <summary>Starts a new season, replacing any save, and saves it at once.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <param name="seed">The schedule seed, or None for a random one.</param>
    /// <param name="path">The save file.</param>
    /// <returns>The new season.</returns>
    public static SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub, Maybe<int> seed, string path)
    {
        var season = SeasonState.New(clubs, userClub, seed.TryGet(out int s) ? s : Random.Shared.Next());
        Use(season, clubs, path);
        SeasonSave.Write(season, path);
        Unsaved = false;
        return season;
    }

    /// <summary>
    /// The setup for the human's next game, and marks it as the game being played, so its result is
    /// recorded when the match ends. Over when the human's club has no games left.
    /// </summary>
    /// <param name="device">The human's input device.</param>
    /// <returns>The match to play, or <see cref="NextMatch.Over"/>.</returns>
    public static NextMatch NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        if (!Current.TryGet(out var season))
        {
            Playing = Maybe<Fixture>.None;
            return NextMatch.Over;
        }
        Playing = season.NextGame;
        return Playing.TryGet(out var game) ? new NextMatch.Play(season.SetupFor(game, Clubs, device)) : NextMatch.Over;
    }

    /// <summary>Leaves season mode: nothing is recorded or saved after this.</summary>
    public static void End()
    {
        Current = Maybe<SeasonState>.None;
        Playing = Maybe<Fixture>.None;
        Unsaved = false;
    }

    /// <summary>
    /// Records the result of the game being played, settles the rest of its round (and any earlier
    /// games still open), and marks the season for saving. Ignored outside season mode.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>True when a result was recorded.</returns>
    public static bool Finish(FixtureResult result)
    {
        if (!Current.TryGet(out var season) || !Playing.TryGet(out var game)) return false;
        season.Record(game.Number, result);
        season.SimulateUpTo(game.Round, Clubs);
        Playing = Maybe<Fixture>.None;
        Unsaved = true;
        return true;
    }

    /// <summary>
    /// W26: plays the human's current game off screen instead of on the ice, with the same
    /// <see cref="QuickSim"/> the other games of the round use (and the playoffs' other series),
    /// then records and saves it exactly as a played game: the result goes into the fixture or the
    /// series, the rest of the round is settled and the save is written. None when there is nothing
    /// to play; once the regular season is over it starts the playoffs and sims the next playoff game.
    /// </summary>
    /// <returns>The game that was simmed, with its result, or None.</returns>
    public static Maybe<SimmedGame> SimCurrentGame()
    {
        if (!Current.TryGet(out var season)) return Maybe<SimmedGame>.None;
        Maybe<ClubData> Find(string id) => Clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal));

        if (!season.IsOver)
        {
            if (!season.NextGame.TryGet(out var game) || !Find(game.Home).TryGet(out var home) || !Find(game.Away).TryGet(out var away)) return Maybe<SimmedGame>.None;
            Playing = game;
            var result = QuickSim.Play(home, away, season.GameSeed(game.Number));
            if (!Finish(result)) return Maybe<SimmedGame>.None;
            Save();
            Console.WriteLine($"SAP: simmed season game {game.Number + 1}, round {game.Round + 1}: {game.Home} {result.HomeGoals}-{result.AwayGoals} {game.Away} ({result.Kind})");
            return new SimmedGame(game.Home, game.Away, result, Playoff: false, Text.Format(K.SimRound, game.Round + 1));
        }

        if (!PlayoffSession.Open().TryGet(out var playoffs) || PlayoffSession.NextSetup(InputDevice.None) is not NextMatch.Play { Setup: var setup } || !PlayoffSession.Playing.TryGet(out var series)) return Maybe<SimmedGame>.None;
        int round = playoffs.Rounds.Count - 1, index = playoffs.Current.Series.IndexOf(series), gameNo = series.Games.Count;
        string stage = playoffs.Current.Name, title = playoffs.CurrentTitle;    // read before Finish, which may draw the next round
        var r = QuickSim.Play(setup.Home, setup.Away, playoffs.GameSeed(round, index, gameNo));
        if (r.Kind == ResultKind.Shootout && !PlayoffSession.Rules.Overtime.HasShootout) r = r with { Kind = ResultKind.Overtime };    // W19: the mode's overtime rules, SHL playoffs have no shootout
        if (!PlayoffSession.Finish(r)) return Maybe<SimmedGame>.None;
        Save();
        Console.WriteLine($"SAP: simmed playoff game {gameNo + 1} of {stage}: {setup.Home.Id} {r.HomeGoals}-{r.AwayGoals} {setup.Away.Id} ({r.Kind})");
        return new SimmedGame(setup.Home.Id, setup.Away.Id, r, Playoff: true, Text.Format(K.SimStage, title, gameNo + 1));
    }

    /// <summary>Writes the save when the season changed, as <see cref="AutosaveSystem"/> does after a played match.</summary>
    static void Save()
    {
        if (!Unsaved || !Current.TryGet(out var season)) return;
        SeasonSave.Write(season, SavePath);
        Unsaved = false;
    }

    static void Use(SeasonState season, IReadOnlyList<ClubData> clubs, string path)
    {
        Current = season;
        Clubs = clubs;
        SavePath = path;
        Playing = Maybe<Fixture>.None;
    }
}

/// <summary>A game settled by <see cref="SeasonSession.SimCurrentGame"/>.</summary>
/// <param name="Home">The home club's id.</param>
/// <param name="Away">The away club's id.</param>
/// <param name="Result">The result.</param>
/// <param name="Playoff">True for a playoff game, false for a regular season game.</param>
/// <param name="Stage">The round or series stage, for the screen ("ROUND 12", "FINAL GAME 3").</param>
public sealed record SimmedGame(string Home, string Away, FixtureResult Result, bool Playoff, string Stage);
