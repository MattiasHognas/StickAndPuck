using System.Text.Json;
using System.Text.Json.Serialization;
using StickAndPuck.App.Replays;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Core;

/// <summary>Editable club data keeps its permissive reader and existing enum attributes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ClubData))]
internal partial class ClubJsonContext : JsonSerializerContext;

/// <summary>Settings and saves share indented camel-case JSON and string enums.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, WriteIndented = true, UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = [typeof(MaybeJsonConverter<PlayoffState>), typeof(MaybeJsonConverter<RuleSet>),
        typeof(MaybeJsonConverter<Fixture>), typeof(MaybeJsonConverter<FixtureResult>),
        typeof(MaybeJsonConverter<string>), typeof(MaybeJsonConverter<PlayerOfGameAward>)])]
[JsonSerializable(typeof(ScreenPrefs))]
[JsonSerializable(typeof(SeasonState))]
[JsonSerializable(typeof(ArcadeRun))]
[JsonSerializable(typeof(PlayoffState))]
[JsonSerializable(typeof(RuleSet))]
[JsonSerializable(typeof(Fixture))]
[JsonSerializable(typeof(FixtureResult))]
[JsonSerializable(typeof(PlayerOfGameAward))]
[JsonSerializable(typeof(string))]
internal partial class SaveJsonContext : JsonSerializerContext;

/// <summary>Replay headers stay compact; optional payloads have explicit generated contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = [typeof(MaybeJsonConverter<ReplaySetup>), typeof(MaybeJsonConverter<OvertimeFormat>), typeof(MaybeJsonConverter<float>)])]
[JsonSerializable(typeof(ReplayHeader))]
[JsonSerializable(typeof(ReplaySetup))]
[JsonSerializable(typeof(OvertimeFormat))]
[JsonSerializable(typeof(float))]
internal partial class ReplayJsonContext : JsonSerializerContext;

/// <summary>Language dictionaries preserve their literal keys and values.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class TextJsonContext : JsonSerializerContext;
