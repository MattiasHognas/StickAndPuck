using System.Text.Json;
using StickAndPuck.App.Core;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Season;

/// <summary>
/// The arcade save: one indented JSON file holding an <see cref="ArcadeRun"/>, next to the season
/// save (arcade.json in the user's settings folder), so a run and a season never overwrite each
/// other. SAP_ARCADE_SAVE=path overrides it. Writes replace the file in one move, as the season save does.
/// </summary>
public static class ArcadeSave
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets the save file path: SAP_ARCADE_SAVE, or arcade.json in the user's settings folder.</summary>
    public static string DefaultPath =>
        Env.Get("SAP_ARCADE_SAVE") is { Length: > 0 } p
            ? Path.GetFullPath(p)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "StickAndPuck", "arcade.json");

    /// <summary>Gets a value indicating whether an arcade save exists (finished or not).</summary>
    /// <returns>True when there is a save.</returns>
    public static bool Exists() => Exists(DefaultPath);

    /// <summary>Gets a value indicating whether an arcade save exists (finished or not).</summary>
    /// <param name="path">The file.</param>
    /// <returns>True when there is a save.</returns>
    public static bool Exists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>Writes the run, replacing any earlier save.</summary>
    /// <param name="run">The run.</param>
    public static void Write(ArcadeRun run) => Write(run, DefaultPath);

    /// <summary>Writes the run, replacing any earlier save.</summary>
    /// <param name="run">The run.</param>
    /// <param name="path">The file.</param>
    public static void Write(ArcadeRun run, string path)
    {
        if (DeviceInput.Playing) return;    // W67: a replay never touches the saves
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
        run.Saved = DateTime.UtcNow;
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(run, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Reads the saved run.</summary>
    /// <returns>The run, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
    public static Maybe<ArcadeRun> Read() => Read(DefaultPath);

    /// <summary>Reads the saved run.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The run, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
    public static Maybe<ArcadeRun> Read(string path)
    {
        if (!File.Exists(path)) return Maybe<ArcadeRun>.None;
        try
        {
            if (JsonSerializer.Deserialize<ArcadeRun>(File.ReadAllText(path), Json) is not { } run || run.Version > ArcadeRun.CurrentVersion || run.Opponents.Count == 0) return Maybe<ArcadeRun>.None;
            return run;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"SAP: arcade save {path} could not be read ({e.Message})");
            return Maybe<ArcadeRun>.None;
        }
    }

    /// <summary>Removes the save.</summary>
    public static void Delete() => Delete(DefaultPath);

    /// <summary>Removes the save.</summary>
    /// <param name="path">The file.</param>
    public static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
