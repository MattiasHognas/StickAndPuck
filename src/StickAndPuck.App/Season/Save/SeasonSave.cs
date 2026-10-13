using System.Text.Json;
using StickAndPuck.App.Core;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Season;

/// <summary>
/// The season save file: one indented JSON file holding a <see cref="SeasonState"/>. It lives in
/// the user's settings folder, so it survives restarts, rebuilds and a fresh single-file unpack:
/// ~/.config/StickAndPuck/season.json on Linux, %APPDATA%\StickAndPuck\season.json on Windows,
/// ~/Library/Application Support/StickAndPuck/season.json on macOS. SAP_SAVE=path overrides it.
/// Writes go to a temporary file first and then replace the save, so a crash never leaves half a file.
/// </summary>
public static class SeasonSave
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the save file path: SAP_SAVE, or season.json in the user's settings folder.</summary>
    public static string DefaultPath =>
        Environment.GetEnvironmentVariable("SAP_SAVE") is { Length: > 0 } p
            ? Path.GetFullPath(p)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "StickAndPuck", "season.json");

    /// <summary>Gets a value indicating whether a season save exists, for the start screen's Continue.</summary>
    /// <param name="path">The file, by default <see cref="DefaultPath"/>.</param>
    /// <returns>True when there is a save to continue.</returns>
    public static bool Exists(string? path = null)
    {
        return File.Exists(path ?? DefaultPath);
    }

    /// <summary>Writes the season, replacing any earlier save.</summary>
    /// <param name="season">The season.</param>
    /// <param name="path">The file, by default <see cref="DefaultPath"/>.</param>
    public static void Write(SeasonState season, string? path = null)
    {
        if (DeviceInput.Playing) return;    // W67: a replay never touches the saves
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        season.Saved = DateTime.UtcNow;
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(season, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads the saved season.</summary>
    /// <param name="path">The file, by default <see cref="DefaultPath"/>.</param>
    /// <returns>The season, or null when there is no save, it cannot be read, or it is from a newer version.</returns>
    public static SeasonState? Read(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return null;
        try
        {
            var season = JsonSerializer.Deserialize<SeasonState>(File.ReadAllText(path), Json);
            if (season == null || season.Version > SeasonState.CurrentVersion || season.Games.Count == 0) return null;
            return season;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"SAP: season save {path} could not be read ({e.Message})");
            return null;
        }
    }

    /// <summary>Removes the save, for New season on the start screen.</summary>
    /// <param name="path">The file, by default <see cref="DefaultPath"/>.</param>
    public static void Delete(string? path = null)
    {
        path ??= DefaultPath;
        if (File.Exists(path)) File.Delete(path);
    }
}
