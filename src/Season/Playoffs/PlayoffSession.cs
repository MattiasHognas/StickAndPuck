using StickAndPuck.Core;

namespace StickAndPuck.Season;

/// <summary>
/// The playoffs of the season in <see cref="SeasonSession"/>. Once the regular season is over,
/// <see cref="Open"/> seeds them from the final table (or picks up the saved ones), a scene (or
/// W11's start screen) builds the world from <see cref="NextSetup"/>, and <see cref="PlayoffSystem"/>
/// records the result when that match ends; <see cref="AutosaveSystem"/> then writes the save.
/// </summary>
public static class PlayoffSession
{
    /// <summary>Gets the playoffs of the season being played, or null before they start.</summary>
    public static PlayoffState? Current => SeasonSession.Current?.Playoffs;

    /// <summary>Gets the series whose next game the current match is, or null when the match is not a playoff game.</summary>
    public static PlayoffSeries? Playing { get; private set; }

    /// <summary>
    /// Starts the playoffs once the regular season is over and settles every game up to the human's
    /// next one; continues saved playoffs as they are. Null while the season is still running.
    /// </summary>
    /// <returns>The playoffs, or null.</returns>
    public static PlayoffState? Open()
    {
        Playing = null;
        if (SeasonSession.Current is not { IsOver: true } season) return null;
        if (season.Playoffs != null) return season.Playoffs;
        season.Playoffs = PlayoffState.New(season);
        season.Playoffs.SimulateUntilUser(SeasonSession.Clubs);
        SeasonSession.Unsaved = true;
        return season.Playoffs;
    }

    /// <summary>
    /// The setup for the human's next playoff game, and marks it as the game being played, so its
    /// result is recorded when the match ends. Null when the human's club is out or there is a champion.
    /// </summary>
    /// <param name="device">The human's input device.</param>
    /// <returns>The setup, or null.</returns>
    public static MatchSetup? NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        var playoffs = Open();
        Playing = playoffs?.UserSeries;
        if (Playing == null) return null;
        var game = new Fixture(-1, -1, Playing.NextHome, Playing.NextAway);
        return SeasonSession.Current!.SetupFor(game, SeasonSession.Clubs, device);
    }

    /// <summary>
    /// Records the result of the game being played, settles the other games up to the human's next
    /// one (the rest of the playoffs when the human's club is out), and marks the season for saving.
    /// </summary>
    /// <param name="result">The result; a tie is refused.</param>
    /// <returns>True when a result was recorded.</returns>
    public static bool Finish(FixtureResult result)
    {
        if (Current is not { } playoffs || Playing == null || !playoffs.Record(Playing, result)) return false;
        playoffs.SimulateUntilUser(SeasonSession.Clubs);
        Playing = null;
        SeasonSession.Unsaved = true;
        return true;
    }

    /// <summary>Forgets the game being played, for leaving season mode.</summary>
    public static void End()
    {
        Playing = null;
    }
}
