using System.Text.Json.Serialization;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Core
{
    /// <summary>Settings and saves share indented camel-case JSON and string enums.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UseStringEnumConverter = true,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        Converters = [
            typeof(MaybeJsonConverter<PlayoffState>),
            typeof(MaybeJsonConverter<RuleSet>),
            typeof(MaybeJsonConverter<Fixture>),
            typeof(MaybeJsonConverter<FixtureResult>),
            typeof(MaybeJsonConverter<string>),
            typeof(MaybeJsonConverter<PlayerOfGameAward>),
        ]
    )]
    [JsonSerializable(typeof(ScreenPrefs))]
    [JsonSerializable(typeof(SeasonState))]
    [JsonSerializable(typeof(ArcadeRun))]
    [JsonSerializable(typeof(PlayoffState))]
    [JsonSerializable(typeof(RuleSet))]
    [JsonSerializable(typeof(Fixture))]
    [JsonSerializable(typeof(FixtureResult))]
    [JsonSerializable(typeof(PlayerOfGameAward))]
    [JsonSerializable(typeof(string))]
    internal sealed partial class SaveJsonContext : JsonSerializerContext;
}
