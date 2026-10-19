using System.Text.Json.Serialization;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.App.Core
{
    /// <summary>Replay headers stay compact; optional payloads have explicit generated contracts.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UseStringEnumConverter = true,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        Converters = [
            typeof(MaybeJsonConverter<ReplaySetup>),
            typeof(MaybeJsonConverter<OvertimeFormat>),
            typeof(MaybeJsonConverter<float>),
            typeof(MaybeJsonConverter<MatchMorale>),
            typeof(MaybeJsonConverter<CoachTalk>),
        ]
    )]
    [JsonSerializable(typeof(ReplayHeader))]
    [JsonSerializable(typeof(ReplaySetup))]
    [JsonSerializable(typeof(OvertimeFormat))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(MatchMorale))]
    [JsonSerializable(typeof(CoachTalk))]
    internal sealed partial class ReplayJsonContext : JsonSerializerContext;
}
