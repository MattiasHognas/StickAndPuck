using StickAndPuck.Core;

namespace StickAndPuck.Season;

/// <summary>
/// The season being played in this run, if any, and which of its games the current match is.
/// A scene (or W11's start screen) calls <see cref="Continue"/> or <see cref="StartNew"/>, builds
/// the world from <see cref="NextSetup"/>, and <see cref="ScheduleSystem"/> records the result
/// when that match ends; <see cref="AutosaveSystem"/> then writes the save.
/// </summary>
public static class SeasonSession
{
    /// <summary>Gets the season being played, or null outside season mode.</summary>
    public static SeasonState? Current { get; private set; }

    /// <summary>Gets the game the current match is, or null when the match is not a season game.</summary>
    public static Fixture? Playing { get; private set; }

    /// <summary>Gets the clubs the season was loaded with.</summary>
    public static IReadOnlyList<ClubData> Clubs { get; private set; } = [];

    /// <summary>Gets the save file this session writes.</summary>
    public static string SavePath { get; private set; } = "";

    /// <summary>Gets a value indicating whether the season changed since it was last saved.</summary>
    public static bool Unsaved { get; internal set; }

    /// <summary>Continues the saved season.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="path">The save file, by default <see cref="SeasonSave.DefaultPath"/>.</param>
    /// <returns>The season, or null when there is no save to continue.</returns>
    public static SeasonState? Continue(IReadOnlyList<ClubData> clubs, string? path = null)
    {
        path ??= SeasonSave.DefaultPath;
        var season = SeasonSave.Read(path);
        if (season == null) return null;
        Use(season, clubs, path);
        Unsaved = false;
        return season;
    }

    /// <summary>Starts a new season, replacing any save, and saves it at once.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <param name="seed">The schedule seed, or null for a random one.</param>
    /// <param name="path">The save file, by default <see cref="SeasonSave.DefaultPath"/>.</param>
    /// <returns>The new season.</returns>
    public static SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub, int? seed = null, string? path = null)
    {
        path ??= SeasonSave.DefaultPath;
        var season = SeasonState.New(clubs, userClub, seed ?? Random.Shared.Next());
        Use(season, clubs, path);
        SeasonSave.Write(season, path);
        Unsaved = false;
        return season;
    }

    /// <summary>
    /// The setup for the human's next game, and marks it as the game being played, so its result is
    /// recorded when the match ends. Null when the human's club has no games left.
    /// </summary>
    /// <param name="device">The human's input device.</param>
    /// <returns>The setup, or null.</returns>
    public static MatchSetup? NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        Playing = Current?.NextGame;
        return Playing == null ? null : Current!.SetupFor(Playing, Clubs, device);
    }

    /// <summary>Leaves season mode: nothing is recorded or saved after this.</summary>
    public static void End()
    {
        Current = null;
        Playing = null;
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
        if (Current == null || Playing == null) return false;
        var game = Playing;
        Current.Record(game.Number, result);
        Current.SimulateUpTo(game.Round, Clubs);
        Playing = null;
        Unsaved = true;
        return true;
    }

    static void Use(SeasonState season, IReadOnlyList<ClubData> clubs, string path)
    {
        Current = season;
        Clubs = clubs;
        SavePath = path;
        Playing = null;
    }
}
