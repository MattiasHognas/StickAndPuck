using System.Text.Json;

namespace StickAndPuck.App.Core;

/// <summary>Loads the club files in data/clubs/, one JSON file per club.</summary>
public static class Clubs
{
    static readonly ClubJsonContext PrettyJson = new(new JsonSerializerOptions(ClubJsonContext.Default.Options) { WriteIndented = true });

    /// <summary>Reads one club file.</summary>
    public static ClubData Load(string path)
    {
        var club = JsonSerializer.Deserialize(File.ReadAllText(path), ClubJsonContext.Default.ClubData)
            ?? throw new InvalidDataException($"{path} is empty");
        if (string.IsNullOrWhiteSpace(club.Id))
            club = club with { Id = Path.GetFileNameWithoutExtension(path) };
        return club;
    }

    /// <summary>Reads every club file in the folder, sorted by id.</summary>
    public static IReadOnlyList<ClubData> LoadAll(string folder = Assets.ClubFolder)
    {
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder, "*.json")
            .Select(Load)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Writes a club back to JSON, in the same shape <see cref="Load"/> reads.</summary>
    public static string ToJson(ClubData club)
    {
        return JsonSerializer.Serialize(club, PrettyJson.ClubData);
    }
}
