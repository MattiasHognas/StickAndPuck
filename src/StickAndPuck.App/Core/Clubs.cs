using System.Text.Json;

namespace StickAndPuck.App.Core;

/// <summary>Loads the club files in data/clubs/, one JSON file per club.</summary>
public static class Clubs
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads one club file.</summary>
    public static ClubData Load(string path)
    {
        var club = JsonSerializer.Deserialize<ClubData>(File.ReadAllText(path), Json)
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
        return JsonSerializer.Serialize(club, new JsonSerializerOptions(Json) { WriteIndented = true });
    }
}
