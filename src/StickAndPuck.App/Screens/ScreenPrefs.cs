using System.Text.Json;
using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens;

/// <summary>
/// What the settings screen changes: the <see cref="Core.Settings"/> every match runs with, plus
/// the period length and the humans' input devices, which go into each <see cref="MatchSetup"/>.
/// </summary>
internal sealed record ScreenPrefs
{
    /// <summary>Period lengths the settings screen offers, in minutes.</summary>
    public static readonly int[] PeriodChoices = [2, 3, 5, 10, 20];

    /// <summary>Gets the player options.</summary>
    public Settings Settings { get; init; } = new();

    /// <summary>Gets the rule preset and the rules changed from it (W44); <see cref="RuleChoice.Resolve"/> gives the rules a match plays.</summary>
    public RuleChoice Rules { get; init; } = new();

    /// <summary>Gets the period length in minutes of game clock.</summary>
    public int PeriodMinutes { get; init; } = 5;

    /// <summary>Gets the device the human plays with.</summary>
    public InputDevice Controls { get; init; } = InputDevice.KeyboardMouse;

    /// <summary>
    /// Gets player two's device in versus: <see cref="InputDevice.None"/> turns player two off
    /// (the AI plays the visitors). When it is the same as <see cref="Controls"/>, versus falls
    /// back to the other device as before. SAP_VERSUS_P2 overrides it.
    /// </summary>
    public InputDevice Player2 { get; init; } = InputDevice.Gamepad1;
}

/// <summary>
/// Reads and writes <see cref="ScreenPrefs"/> as settings.json next to the season save:
/// ~/.config/StickAndPuck/settings.json on Linux, %APPDATA%\StickAndPuck\settings.json on Windows.
/// SAP_SETTINGS=path overrides it.
/// </summary>
internal static class ScreenPrefsFile
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the settings file path.</summary>
    public static string DefaultPath =>
        Environment.GetEnvironmentVariable("SAP_SETTINGS") is { Length: > 0 } p
            ? Path.GetFullPath(p)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "StickAndPuck", "settings.json");

    /// <summary>Reads the saved settings, or the defaults when there is no readable file.</summary>
    public static ScreenPrefs Read(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<ScreenPrefs>(File.ReadAllText(path), Json) is { } prefs)
                return prefs with { PeriodMinutes = Math.Clamp(prefs.PeriodMinutes, 1, 20) };
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"SAP: settings: {path} unreadable ({e.Message}), using defaults");
        }
        return new ScreenPrefs();
    }

    /// <summary>Gets the settings as the file stores them (W67: a replay keeps them).</summary>
    /// <param name="prefs">The settings.</param>
    /// <returns>JSON.</returns>
    public static string ToJson(ScreenPrefs prefs) => JsonSerializer.Serialize(prefs, Json);

    /// <summary>Reads settings <see cref="ToJson"/> wrote.</summary>
    /// <param name="json">JSON.</param>
    /// <returns>The settings.</returns>
    public static ScreenPrefs FromJson(string json) => JsonSerializer.Deserialize<ScreenPrefs>(json, Json) ?? new ScreenPrefs();

    /// <summary>Writes the settings; a failure is logged, never fatal.</summary>
    /// <param name="prefs">The settings.</param>
    /// <param name="path">The file, or null for the default.</param>
    public static void Write(ScreenPrefs prefs, string? path = null)
    {
        if (DeviceInput.Playing) return;    // W67: a replay's menus never touch the saved settings
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(prefs, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"SAP: settings: could not write {path} ({e.Message})");
        }
    }
}
