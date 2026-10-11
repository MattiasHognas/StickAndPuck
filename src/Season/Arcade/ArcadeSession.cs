using StickAndPuck.Core;

namespace StickAndPuck.Season;

/// <summary>
/// The arcade run being played, and the entry point for W11's start screen: show Continue when
/// <see cref="CanContinue"/>, call <see cref="Continue"/> or <see cref="StartNew"/>, then build the
/// world from <see cref="NextSetup"/>. <see cref="ArcadeSystem"/> records the result when that match
/// ends and saves the run at once; when <see cref="ArcadeRun.IsOver"/> the run's summary shows.
/// </summary>
public static class ArcadeSession
{
    /// <summary>Gets the run being played, or null outside arcade mode.</summary>
    public static ArcadeRun? Current { get; private set; }

    /// <summary>Gets the setup of the arcade game being played, or null when the current match is not one.</summary>
    public static MatchSetup? Playing { get; private set; }

    /// <summary>Gets the clubs the run was loaded with.</summary>
    public static IReadOnlyList<ClubData> Clubs { get; private set; } = [];

    /// <summary>Gets the save file this session writes.</summary>
    public static string SavePath { get; private set; } = "";

    /// <summary>Gets a value indicating whether there is an unfinished run to continue, for the start screen.</summary>
    /// <param name="path">The save file, by default <see cref="ArcadeSave.DefaultPath"/>.</param>
    /// <returns>True when a saved run is still going.</returns>
    public static bool CanContinue(string? path = null)
    {
        return ArcadeSave.Read(path) is { IsOver: false };
    }

    /// <summary>Continues the saved run; a finished run is loaded too, so its summary can show.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="path">The save file, by default <see cref="ArcadeSave.DefaultPath"/>.</param>
    /// <returns>The run, or null when there is no save.</returns>
    public static ArcadeRun? Continue(IReadOnlyList<ClubData> clubs, string? path = null)
    {
        path ??= ArcadeSave.DefaultPath;
        var run = ArcadeSave.Read(path);
        if (run == null) return null;
        Use(run, clubs, path);
        return run;
    }

    /// <summary>Starts a new run, replacing any arcade save, and saves it at once.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <param name="rule">Sudden death or three lives.</param>
    /// <param name="seed">The opponent order seed, or null for a random one.</param>
    /// <param name="path">The save file, by default <see cref="ArcadeSave.DefaultPath"/>.</param>
    /// <returns>The new run.</returns>
    public static ArcadeRun StartNew(IReadOnlyList<ClubData> clubs, string userClub, ArcadeRule rule, int? seed = null, string? path = null)
    {
        path ??= ArcadeSave.DefaultPath;
        var run = ArcadeRun.New(clubs, userClub, rule, seed ?? Random.Shared.Next());
        // the best-runs board carries over; a run given up with games played goes on it as it stands
        if (ArcadeSave.Read(path) is { } old)
        {
            run.Records = old.Records;
            if (!old.IsOver && old.Games.Count > 0) run.Records = ArcadeRecord.Add(old.Records, ArcadeRecord.Of(old));
        }
        Use(run, clubs, path);
        ArcadeSave.Write(run, path);
        return run;
    }

    /// <summary>The setup for the human's next game, marked as the game being played. Null when the run is over.</summary>
    /// <param name="device">The human's input device.</param>
    /// <returns>The setup, or null.</returns>
    public static MatchSetup? NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        Playing = Current?.NextSetup(Clubs, device);
        return Playing;
    }

    /// <summary>Records the result of the game being played and saves the run. Ignored outside arcade mode.</summary>
    /// <param name="result">The final score, home goals first.</param>
    /// <returns>The recorded game, or null when nothing was being played.</returns>
    public static ArcadeGame? Finish(FixtureResult result)
    {
        if (Current == null || Playing == null) return null;
        var game = Current.Record(result);
        Playing = null;
        if (Current.IsOver)
        {
            var record = ArcadeRecord.Of(Current);
            Current.Records = ArcadeRecord.Add(Current.Records, record);
            Current.Place = ArcadeRecord.Place(Current.Records, record);
        }
        ArcadeSave.Write(Current, SavePath);
        return game;
    }

    /// <summary>Leaves arcade mode: nothing is recorded or saved after this.</summary>
    public static void End()
    {
        Current = null;
        Playing = null;
    }

    static void Use(ArcadeRun run, IReadOnlyList<ClubData> clubs, string path)
    {
        Current = run;
        Clubs = clubs;
        SavePath = path;
        Playing = null;
    }
}
