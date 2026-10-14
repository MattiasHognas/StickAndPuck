namespace StickAndPuck.App.Core;

/// <summary>What lives as long as the game runs rather than one match: the screens, the season, arcade
/// and playoff sessions, the last match's sheet, the pads and the render scale. <see cref="Game"/> makes
/// one and every world it runs points at it (<see cref="World.App"/>); a world built on its own (a test)
/// gets a fresh one, so nothing carries over between them.</summary>
public sealed class AppState
{
    /// <summary>Initializes a new instance of the <see cref="AppState"/> class, with nothing started.</summary>
    public AppState()
    {
        Screens = new Screens.ScreenFlow(this);
        Rumble = new Players.Rumble(Pads);
    }

    /// <summary>Gets the screens and where the game is between them and matches.</summary>
    internal Screens.ScreenFlow Screens { get; }

    /// <summary>Gets the season being played, if any.</summary>
    public Season.SeasonSession Season { get; } = new();

    /// <summary>Gets the playoffs of <see cref="Season"/>.</summary>
    public Season.PlayoffSession Playoffs => Season.Playoffs;

    /// <summary>Gets the arcade run being played, if any.</summary>
    public Season.ArcadeSession Arcade { get; } = new();

    /// <summary>Gets or sets the sheet of the last match that ended in this run, or None before one has, for the results screen of any mode.</summary>
    public Maybe<Season.MatchStatSheet> LastMatchStats { get; set; }

    /// <summary>Gets the SAP_SCREEN_KEYS presses that drive the menus, when set.</summary>
    internal Screens.MenuScript MenuScript { get; } = new();

    /// <summary>Gets the connected gamepads.</summary>
    public Players.Gamepads Pads { get; } = new();

    /// <summary>Gets the gamepad rumble.</summary>
    internal Players.Rumble Rumble { get; }

    /// <summary>Gets how coarse the scene is drawn.</summary>
    public Render.RenderScale Scale { get; } = new();

    /// <summary>Gets where the dither pattern starts on the scene's pixel grid.</summary>
    public Render.DitherGrid Dither { get; } = new();
}
