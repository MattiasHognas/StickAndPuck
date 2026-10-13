namespace StickAndPuck.App.Core;

/// <summary>How control moves between players.</summary>
public enum ControlSwitching
{
    /// <summary>Default: you control the puck carrier, control follows passes, on defence it goes to the player nearest the puck.</summary>
    Auto,

    /// <summary>Control follows passes, but on defence only the switch button changes player.</summary>
    PassesOnly,

    /// <summary>Only the switch button changes player.</summary>
    Manual,
}

/// <summary>How lines change.</summary>
public enum LineChanges
{
    /// <summary>Default: tired lines change on their own; a button changes early.</summary>
    Automatic,

    /// <summary>Lines change only on the button.</summary>
    Manual,
}

/// <summary>The arena lighting mood a match starts with (W18's time of day).</summary>
public enum LightingMood
{
    /// <summary>Default: a random mood each match.</summary>
    Random,

    /// <summary>Early afternoon: cool daylight, bright ice.</summary>
    Early,

    /// <summary>Late afternoon: the dusk look.</summary>
    Afternoon,

    /// <summary>Evening: floodlit, dark stands.</summary>
    Late,
}

/// <summary>How hard the AI plays (W29 scales the AI by it).</summary>
public enum Difficulty
{
    /// <summary>The AI makes more mistakes.</summary>
    Easy,

    /// <summary>Default: the AI as the traits say.</summary>
    Normal,

    /// <summary>The AI plays sharper.</summary>
    Hard,
}

/// <summary>
/// Player options from the settings screen, with the defaults from the plan's Decisions. Every
/// switch lives here (W20); systems only read them from <see cref="World.Settings"/>.
/// <see cref="FromEnvironment"/> lays the SAP_* variables over them for headless runs.
/// </summary>
public sealed record Settings
{
    /// <summary>Gets how control moves between players.</summary>
    public ControlSwitching Switching { get; init; } = ControlSwitching.Auto;

    /// <summary>Gets how lines change.</summary>
    public LineChanges Lines { get; init; } = LineChanges.Automatic;

    /// <summary>Gets the AI difficulty (SAP_DIFFICULTY=easy|normal|hard).</summary>
    public Difficulty Difficulty { get; init; } = Difficulty.Normal;

    /// <summary>Gets a value indicating whether the gameplay camera zooms with speed (SAP_CAMERA_ZOOM=0|1). Off by default: zooming softens the ad board text. F6 toggles it in play.</summary>
    public bool CameraZoom { get; init; }

    /// <summary>Gets the lighting mood matches start with (SAP_LIGHTING=early|afternoon|late|random).</summary>
    public LightingMood Lighting { get; init; } = LightingMood.Random;

    /// <summary>Gets a value indicating whether fights can start.</summary>
    public bool Fights { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls penalties.</summary>
    public bool Penalties { get; init; } = true;

    /// <summary>Gets a value indicating whether a marker shows under each controlled skater (SAP_CONTROL_MARKER=0|1; W30 draws it).</summary>
    public bool ControlMarker { get; init; } = true;

    /// <summary>Gets a value indicating whether a season game can be simmed instead of played (SAP_SIM_GAME=0|1; W26 offers it).</summary>
    public bool SimGame { get; init; } = true;

    /// <summary>Gets a value indicating whether dekes happen on their own; off means the deke button only (SAP_AUTO_DEKE=0|1; W32 reads it).</summary>
    public bool AutoDeke { get; init; }

    /// <summary>Gets a value indicating whether a shot straight off a pass fires as a one-timer (SAP_ONE_TIMERS=0|1; W32 reads it).</summary>
    public bool OneTimers { get; init; } = true;

    /// <summary>Gets a value indicating whether the home and away kits are swapped (SAP_SWAP_KITS=0|1). Tab toggles it in play.</summary>
    public bool SwapKits { get; init; }

    /// <summary>Gets a value indicating whether the fps and key help line shows top left (SAP_FPS=0|1).</summary>
    public bool FpsLine { get; init; }

    /// <summary>Gets a value indicating whether the plain score and clock debug line shows (SAP_MATCH_DEBUG=0|1).</summary>
    public bool MatchDebug { get; init; }

    /// <summary>Gets a value indicating whether the energy panel showing each unit on the ice shows (SAP_LINES_DEBUG=0|1).</summary>
    public bool LinesDebug { get; init; }

    /// <summary>Gets the sound effect volume, 0 to 1.</summary>
    public float Volume { get; init; } = 0.8f;

    /// <summary>Gets the rebound keys and gamepad buttons (W39); an action missing here keeps its default. Read them with <see cref="KeyBindings.Of"/>.</summary>
    public IReadOnlyDictionary<GameAction, KeyBinding> Keys { get; init; } = new Dictionary<GameAction, KeyBinding>();

    /// <summary>
    /// Lays the SAP_* overrides over these settings: SAP_DIFFICULTY, SAP_CAMERA_ZOOM, SAP_LIGHTING,
    /// SAP_CONTROL_MARKER, SAP_SIM_GAME, SAP_AUTO_DEKE, SAP_ONE_TIMERS, SAP_SWAP_KITS, SAP_FPS,
    /// SAP_MATCH_DEBUG and SAP_LINES_DEBUG. A variable that is unset or empty changes nothing, so
    /// the saved settings stay in charge; a bad value is an error, like SAP_LIGHTING always was.
    /// </summary>
    /// <param name="saved">The saved settings, or null for the defaults.</param>
    /// <returns>The settings to run with.</returns>
    public static Settings FromEnvironment(Settings? saved = null)
    {
        var s = saved ?? new Settings();
        if (Flag("SAP_CAMERA_ZOOM") is { } zoom) s = s with { CameraZoom = zoom };
        if (Flag("SAP_CONTROL_MARKER") is { } marker) s = s with { ControlMarker = marker };
        if (Flag("SAP_SIM_GAME") is { } sim) s = s with { SimGame = sim };
        if (Flag("SAP_AUTO_DEKE") is { } deke) s = s with { AutoDeke = deke };
        if (Flag("SAP_ONE_TIMERS") is { } oneTimers) s = s with { OneTimers = oneTimers };
        if (Flag("SAP_SWAP_KITS") is { } swap) s = s with { SwapKits = swap };
        if (Flag("SAP_FPS") is { } fps) s = s with { FpsLine = fps };
        if (Flag("SAP_MATCH_DEBUG") is { } match) s = s with { MatchDebug = match };
        if (Flag("SAP_LINES_DEBUG") is { } lines) s = s with { LinesDebug = lines };
        if (Choice<LightingMood>("SAP_LIGHTING") is { } mood) s = s with { Lighting = mood };
        if (Choice<Difficulty>("SAP_DIFFICULTY") is { } level) s = s with { Difficulty = level };
        return s;
    }

    /// <summary>Reads a 0/1 variable; "on", "off", "true" and "false" work too.</summary>
    static bool? Flag(string name)
    {
        return Environment.GetEnvironmentVariable(name)?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "1" or "on" or "true" or "yes" => true,
            "0" or "off" or "false" or "no" => false,
            var other => throw new InvalidOperationException($"{name}={other}: use 0 or 1"),
        };
    }

    static T? Choice<T>(string name)
        where T : struct, Enum
    {
        if (Environment.GetEnvironmentVariable(name) is not { Length: > 0 } value) return null;
        if (Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed)) return parsed;
        throw new InvalidOperationException($"{name}={value}: use {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}");
    }
}
