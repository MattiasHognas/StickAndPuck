using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replays;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>Generated contracts preserve existing files, including nested optional values.</summary>
public sealed class JsonContractsTests : CheckedTest
{
    [Test]
    public void ReflectionFallbackIsDisabled()
    {
        Check(!JsonSerializer.IsReflectionEnabledByDefault, "the test host exercises the same reflection-free JSON policy as the game");
    }

    [Test]
    public void ClubOutputAndPermissiveReaderStayCompatible()
    {
        var legacy = Legacy(indented: true, enums: false);
        foreach (var club in WorldBuilder.Clubs)
        {
            Check(string.Equals(Clubs.ToJson(club), JsonSerializer.Serialize(club, legacy), StringComparison.Ordinal),
                $"{club.Id} keeps byte-identical JSON, including the replay's club fingerprint");
        }
        const string json = """{/* edited club */ "ID":"custom", "name":"Örebro", "roster":[],} """;
        var read = JsonSerializer.Deserialize(json, ClubJsonContext.Default.ClubData);
        Check(string.Equals(read.Id, "custom", StringComparison.Ordinal), "club input still accepts comments, trailing commas and case-insensitive names");
    }

    [Test]
    public void SavesAndSettingsKeepLegacyShape()
    {
        var season = new SeasonState
        {
            UserClub = "home", Clubs = ["home", "away"], Rules = RuleSet.Shl,
            Games = [new(0, 0, "home", "away", new FixtureResult(3, 2, ResultKind.Overtime)), new(1, 1, "away", "home")],
            Playoffs = new PlayoffState { UserClub = "home", Seeds = ["home", "away"] },
        };
        season.Stats.Awards.Add(new("g0", "home", "away", 3, 2, "home", 7, "Åke", 2, 1, 0));
        Compatible(season, SaveJsonContext.Default.SeasonState, indented: true);
        season.Rules = Maybe<RuleSet>.None;
        season.Playoffs = Maybe<PlayoffState>.None;
        season.Stats.Awards.Clear();
        Compatible(season, SaveJsonContext.Default.SeasonState, indented: true);
        var arcade = new ArcadeRun { UserClub = "home", Opponents = ["away"] };
        Compatible(arcade, SaveJsonContext.Default.ArcadeRun, indented: true);
        arcade.Games.Add(new(0, "away", true, new(3, 0, ResultKind.Regulation)));
        Compatible(arcade, SaveJsonContext.Default.ArcadeRun, indented: true);
        Compatible(new ScreenPrefs(), SaveJsonContext.Default.ScreenPrefs, indented: true);
    }

    [Test]
    public void ReplayHeadersKeepNestedOptionalValues()
    {
        var header = new ReplayHeader
        {
            Seed = 13,
            Setup = new ReplaySetup("home", "away", [new(InputDevice.KeyboardMouse, Side.Home)],
                1200, 3, OvertimeRule.Shl, OvertimeFormat.ShlPlayoffs, RuleSet.Shl),
        };
        Compatible(header, ReplayJsonContext.Default.ReplayHeader, indented: false);
        string json = JsonSerializer.Serialize(header, ReplayJsonContext.Default.ReplayHeader);
        var read = JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplayHeader);
        Check(read.Setup.TryGet(out var setup) && setup.OvertimeFormat.TryGet(out var format)
            && format.PeriodSeconds.TryGet(out float seconds) && Math.Abs(seconds - 1200) < 0.001f,
            "the custom overtime duration survives both optional wrappers");
        Compatible(header with { Setup = Maybe<ReplaySetup>.None }, ReplayJsonContext.Default.ReplayHeader, indented: false);
    }

    [Test]
    public void EveryReachableOptionalHasAnExplicitConverter()
    {
        JsonSerializerContext[] contexts = [ClubJsonContext.Default, SaveJsonContext.Default, ReplayJsonContext.Default, TextJsonContext.Default];
        foreach (var context in contexts)
        {
            var visited = new HashSet<Type>();
            void Visit(Type type)
            {
                if (!visited.Add(type)) return;
                var info = context.GetTypeInfo(type) ?? throw new InvalidOperationException($"Missing generated contract for {type}");
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Maybe<>))
                    Check(info.Kind == JsonTypeInfoKind.None, $"{context.GetType().Name}: {type} uses its value/null converter");
                foreach (var property in info.Properties) Visit(property.PropertyType);
                if (type.IsGenericType)
                {
                    foreach (var argument in type.GetGenericArguments()) Visit(argument);
                }
                if (type.IsArray) Visit(type.GetElementType());
            }
            foreach (var root in context.GetType().GetCustomAttributesData().Where(a => a.AttributeType == typeof(JsonSerializableAttribute)))
                Visit((Type)root.ConstructorArguments[0].Value);
        }
    }

    void Compatible<T>(T value, JsonTypeInfo<T> contract, bool indented)
    {
        var legacy = Legacy(indented, enums: true);
        string before = JsonSerializer.Serialize(value, legacy);
        string after = JsonSerializer.Serialize(value, contract);
        Check(string.Equals(before, after, StringComparison.Ordinal), $"{typeof(T).Name} keeps its exact JSON format");
        var read = JsonSerializer.Deserialize(before, contract);
        Check(string.Equals(JsonSerializer.Serialize(read, contract), after, StringComparison.Ordinal), $"{typeof(T).Name} reads the legacy format");
    }

    // Explicit reflection is confined to this compatibility oracle, reproducing the previous writer.
    static JsonSerializerOptions Legacy(bool indented, bool enums)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new LegacyMaybeFactory() },
        };
        if (enums) options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    sealed class LegacyMaybeFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Maybe<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(MaybeJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
    }
}
