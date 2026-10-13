using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>
/// The playoffs of the season in <see cref="SeasonSession"/>. Once the regular season is over,
/// <see cref="Open"/> seeds them from the final table (or picks up the saved ones), a scene (or
/// W11's start screen) builds the world from <see cref="NextSetup"/>, and <see cref="PlayoffSystem"/>
/// records the result when that match ends; <see cref="AutosaveSystem"/> then writes the save.
/// </summary>
public static class PlayoffSession
{
    /// <summary>Gets the playoffs of the season being played, or None before they start.</summary>
    public static Maybe<PlayoffState> Current => SeasonSession.Current.TryGet(out var season) ? season.Playoffs : Maybe<PlayoffState>.None;

    /// <summary>Gets the series whose next game the current match is, or None when the match is not a playoff game.</summary>
    public static Maybe<PlayoffSeries> Playing { get; private set; }

    /// <summary>Gets or sets the rules playoff games follow (W19): the SHL's unless a mode sets its own.</summary>
    public static PlayoffRules Rules { get; set; } = PlayoffRules.Shl;

    /// <summary>
    /// Starts the playoffs once the regular season is over and settles every game up to the human's
    /// next one; continues saved playoffs as they are. None while the season is still running.
    /// </summary>
    /// <returns>The playoffs, or None.</returns>
    public static Maybe<PlayoffState> Open()
    {
        Playing = Maybe<PlayoffSeries>.None;
        if (!SeasonSession.Current.TryGet(out var season) || !season.IsOver) return Maybe<PlayoffState>.None;
        if (season.Playoffs.HasValue) return season.Playoffs;
        var playoffs = PlayoffState.New(season);
        season.Playoffs = playoffs;
        playoffs.SimulateUntilUser(SeasonSession.Clubs);
        SeasonSession.Unsaved = true;
        return playoffs;
    }

    /// <summary>
    /// The setup for the human's next playoff game, with the overtime rules of <see cref="Rules"/>, and
    /// marks it as the game being played, so its result is recorded when the match ends. Over when the
    /// human's club is out or there is a champion.
    /// </summary>
    /// <param name="device">The human's input device.</param>
    /// <returns>The match to play, or <see cref="NextMatch.Over"/>.</returns>
    public static NextMatch NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        var playoffs = Open();
        Playing = playoffs.TryGet(out var open) ? open.UserSeries : Maybe<PlayoffSeries>.None;
        if (!Playing.TryGet(out var series) || !SeasonSession.Current.TryGet(out var season)) return NextMatch.Over;
        var game = new Fixture(-1, -1, series.NextHome, series.NextAway);
        return new NextMatch.Play(season.SetupFor(game, SeasonSession.Clubs, device) with { OvertimeFormat = Rules.Overtime });
    }

    /// <summary>
    /// Records the result of the game being played, settles the other games up to the human's next
    /// one (the rest of the playoffs when the human's club is out), and marks the season for saving.
    /// </summary>
    /// <param name="result">The result; a tie is refused.</param>
    /// <returns>True when a result was recorded.</returns>
    public static bool Finish(FixtureResult result)
    {
        if (!Current.TryGet(out var playoffs) || !Playing.TryGet(out var series) || !playoffs.Record(series, result)) return false;
        playoffs.SimulateUntilUser(SeasonSession.Clubs);
        Playing = Maybe<PlayoffSeries>.None;
        SeasonSession.Unsaved = true;
        return true;
    }

    /// <summary>Forgets the game being played, for leaving season mode; the SHL rules come back.</summary>
    public static void End()
    {
        Playing = Maybe<PlayoffSeries>.None;
        Rules = PlayoffRules.Shl;
    }
}
