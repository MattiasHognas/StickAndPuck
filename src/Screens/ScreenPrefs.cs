using System.Text.Json;
using System.Text.Json.Serialization;
using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>
/// What the settings screen changes: the <see cref="Core.Settings"/> every match runs with, plus
/// the period length and the human's input device, which go into each <see cref="MatchSetup"/>.
/// </summary>
internal sealed record ScreenPrefs
{
    /// <summary>Period lengths the settings screen offers, in minutes.</summary>
    public static readonly int[] PeriodChoices = [2, 3, 5, 10, 20];

    /// <summary>Gets the player options.</summary>
    public Settings Settings { get; init; } = new();

    /// <summary>Gets the period length in minutes of game clock.</summary>
    public int PeriodMinutes { get; init; } = 5;

    /// <summary>Gets the device the human plays with.</summary>
    public InputDevice Controls { get; init; } = InputDevice.KeyboardMouse;
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

    /// <summary>Writes the settings; a failure is logged, never fatal.</summary>
    public static void Write(ScreenPrefs prefs, string? path = null)
    {
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
