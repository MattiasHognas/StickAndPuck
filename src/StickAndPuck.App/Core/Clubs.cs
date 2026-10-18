using System.Text.Json;

namespace StickAndPuck.App.Core
{
    /// <summary>Loads the club files in data/clubs/, one JSON file per club.</summary>
    public static class Clubs
    {
        private static readonly ClubJsonContext PrettyJson = new(
            new JsonSerializerOptions(ClubJsonContext.Default.Options) { WriteIndented = true }
        );

        /// <summary>Reads one club file.</summary>
        public static ClubData Load(string path)
        {
            ClubData club =
                JsonSerializer.Deserialize(File.ReadAllText(path), ClubJsonContext.Default.ClubData)
                ?? throw new InvalidDataException($"{path} is empty");
            if (string.IsNullOrWhiteSpace(club.Id))
            {
                club = club with { Id = Path.GetFileNameWithoutExtension(path) };
            }

            return club;
        }

        /// <summary>Reads every club in one league, sorted by id: the 14 SHL clubs in data/clubs/ or the 32 NHL
        /// clubs in data/clubs/nhl/. The single entry point for league choice (W117).</summary>
        public static IReadOnlyList<ClubData> Load(League league)
        {
            return league == League.Nhl ? LoadNhl() : LoadAll();
        }

        /// <summary>Reads every club file directly in the folder (not its subfolders, so the SHL list leaves
        /// out data/clubs/nhl/), sorted by id.</summary>
        public static IReadOnlyList<ClubData> LoadAll(string folder = Assets.ClubFolder)
        {
            List<ClubData> clubs = [];
            foreach (
                string resourceName in ResourceLoader
                    .List(folder, ".json")
                    .Where(name =>
                        !name[(folder.Length + 1)..^".json".Length]
                            .Contains('.', StringComparison.Ordinal)
                    )
            )
            {
                ClubData club =
                    JsonSerializer.Deserialize(
                        ResourceLoader.ReadText(resourceName),
                        ClubJsonContext.Default.ClubData
                    ) ?? throw new InvalidDataException($"{resourceName} is empty");
                if (string.IsNullOrWhiteSpace(club.Id))
                {
                    club = club with { Id = resourceName.Split('.')[^2] };
                }

                clubs.Add(club);
            }
            return [.. clubs.OrderBy(c => c.Id, StringComparer.Ordinal)];
        }

        /// <summary>Reads the 32 NHL club files in data/clubs/nhl/ (W115), sorted by id. Not part of the SHL
        /// club list the menus, seasons and arcade use; league choice (W117) picks between the two.</summary>
        public static IReadOnlyList<ClubData> LoadNhl()
        {
            return LoadAll(Assets.NhlClubFolder);
        }

        /// <summary>Writes a club back to JSON, in the same shape <see cref="Load(string)"/> reads.</summary>
        public static string ToJson(ClubData club)
        {
            return JsonSerializer.Serialize(club, PrettyJson.ClubData);
        }
    }
}
