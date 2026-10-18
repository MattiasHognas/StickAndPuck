using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>Editable club data keeps its permissive reader and existing enum attributes.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        Converters = [typeof(MaybeJsonConverter<string>)]
    )]
    [JsonSerializable(typeof(ClubData))]
    internal sealed partial class ClubJsonContext : JsonSerializerContext;
}
