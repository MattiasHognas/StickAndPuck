using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Season.News
{
    /// <summary>The news deck reads like the club files: camel case, comments and trailing commas allowed, enums as strings.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UseStringEnumConverter = true,
        GenerationMode = JsonSourceGenerationMode.Metadata
    )]
    [JsonSerializable(typeof(NewsDeck))]
    internal sealed partial class NewsJsonContext : JsonSerializerContext;
}
