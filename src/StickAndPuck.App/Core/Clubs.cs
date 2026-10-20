using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace StickAndPuck.App.Core
{
    /// <summary>Loads the club files in data/clubs/{league}/, one JSON file per club.</summary>
    public static class Clubs
    {
        private static readonly JsonTypeInfo<ClubData> PrettyJson = new JsonSerializerOptions(
            ClubJsonContext.Default.Options
        )
        {
            WriteIndented = true,
            TypeInfoResolver = ClubJsonContext.Default.WithAddedModifier(OmitNoSpares),
        }.GetTypeInfo<ClubData>();

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

        /// <summary>Reads every club in data/clubs/{league}/, sorted by id.</summary>
        public static IReadOnlyList<ClubData> Load(League league)
        {
            return LoadAll(Assets.ClubFolder(league));
        }

        /// <summary>Reads every club file directly in a league folder, sorted by id. Defaults to SHL.</summary>
        public static IReadOnlyList<ClubData> LoadAll(string folder = Assets.ShlClubFolder)
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
            return Load(League.Nhl);
        }

        /// <summary>Writes a club back to JSON, in the same shape <see cref="Load(string)"/> reads.</summary>
        public static string ToJson(ClubData club)
        {
            return JsonSerializer.Serialize(club, PrettyJson);
        }

        /// <summary>Leaves a club's spares (W86) out of the written JSON when it has none.</summary>
        private static void OmitNoSpares(JsonTypeInfo info)
        {
            if (info.Type != typeof(ClubData))
            {
                return;
            }

            foreach (
                JsonPropertyInfo property in info.Properties.Where(p =>
                    string.Equals(p.Name, "spares", StringComparison.Ordinal)
                )
            )
            {
                property.ShouldSerialize = static (_, value) =>
                    value is IReadOnlyList<PlayerData> { Count: > 0 };
            }
        }
    }
}
