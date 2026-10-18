using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>Generated contracts preserve existing files, including nested optional values.</summary>
    public sealed class JsonContractsTests : CheckedTest
    {
        [Test]
        public void ReflectionFallbackIsDisabled()
        {
            Check(
                !JsonSerializer.IsReflectionEnabledByDefault,
                "the test host exercises the same reflection-free JSON policy as the game"
            );
        }

        [Test]
        public void ClubOutputAndPermissiveReaderStayCompatible()
        {
            JsonSerializerOptions legacy = Legacy(indented: true, enums: false);
            foreach (ClubData club in WorldBuilder.Clubs)
            {
                Check(
                    string.Equals(
                        Clubs.ToJson(club),
                        JsonSerializer.Serialize(club, legacy),
                        StringComparison.Ordinal
                    ),
                    $"{club.Id} keeps byte-identical JSON, including the replay's club fingerprint"
                );
            }
            const string json =
                """{/* edited club */ "ID":"custom", "name":"Örebro", "roster":[],} """;
            ClubData read = JsonSerializer.Deserialize(json, ClubJsonContext.Default.ClubData);
            Check(
                string.Equals(read.Id, "custom", StringComparison.Ordinal),
                "club input still accepts comments, trailing commas and case-insensitive names"
            );
        }

        [Test]
        public void SavesAndSettingsKeepLegacyShape()
        {
            SeasonState season = new()
            {
                UserClub = "home",
                Clubs = ["home", "away"],
                Rules = RuleSet.Shl,
                Games =
                [
                    new(0, 0, "home", "away", new FixtureResult(3, 2, ResultKind.Overtime)),
                    new(1, 1, "away", "home"),
                ],
                Playoffs = new PlayoffState { UserClub = "home", Seeds = ["home", "away"] },
            };
            season.Stats.Awards.Add(new("g0", "home", "away", 3, 2, "home", 7, "Åke", 2, 1, 0));
            Compatible(season, SaveJsonContext.Default.SeasonState, indented: true);
            season.Rules = Maybe.None<RuleSet>();
            season.Playoffs = Maybe.None<PlayoffState>();
            season.Stats.Awards.Clear();
            Compatible(season, SaveJsonContext.Default.SeasonState, indented: true);
            ArcadeRun arcade = new() { UserClub = "home", Opponents = ["away"] };
            Compatible(arcade, SaveJsonContext.Default.ArcadeRun, indented: true);
            arcade.Games.Add(new(0, "away", Home: true, new(3, 0, ResultKind.Regulation)));
            Compatible(arcade, SaveJsonContext.Default.ArcadeRun, indented: true);
            Compatible(new ScreenPrefs(), SaveJsonContext.Default.ScreenPrefs, indented: true);
        }

        [Test]
        public void ReplayHeadersKeepNestedOptionalValues()
        {
            ReplayHeader header = new()
            {
                Seed = 13,
                Setup = new ReplaySetup(
                    "home",
                    "away",
                    [new(InputDevice.KeyboardMouse, Side.Home)],
                    1200,
                    3,
                    OvertimeRule.Shl,
                    OvertimeFormat.ShlPlayoffs,
                    RuleSet.Shl
                ),
            };
            Compatible(header, ReplayJsonContext.Default.ReplayHeader, indented: false);
            string json = JsonSerializer.Serialize(header, ReplayJsonContext.Default.ReplayHeader);
            ReplayHeader read = JsonSerializer.Deserialize(
                json,
                ReplayJsonContext.Default.ReplayHeader
            );
            Check(
                read.Setup.TryGet(out ReplaySetup setup)
                    && setup.OvertimeFormat.TryGet(out OvertimeFormat format)
                    && format.PeriodSeconds.TryGet(out float seconds)
                    && Math.Abs(seconds - 1200) < 0.001f,
                "the custom overtime duration survives both optional wrappers"
            );
            Compatible(
                header with
                {
                    Setup = Maybe.None<ReplaySetup>(),
                },
                ReplayJsonContext.Default.ReplayHeader,
                indented: false
            );
        }

        [Test]
        public void EveryReachableOptionalHasAnExplicitConverter()
        {
            JsonSerializerContext[] contexts =
            [
                ClubJsonContext.Default,
                SaveJsonContext.Default,
                ReplayJsonContext.Default,
                TextJsonContext.Default,
            ];
            foreach (JsonSerializerContext context in contexts)
            {
                HashSet<Type> visited = [];
                void Visit(Type type)
                {
                    if (!visited.Add(type))
                    {
                        return;
                    }

                    JsonTypeInfo info =
                        context.GetTypeInfo(type)
                        ?? throw new InvalidOperationException(
                            $"Missing generated contract for {type}"
                        );
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Maybe<>))
                    {
                        Check(
                            info.Kind == JsonTypeInfoKind.None,
                            $"{context.GetType().Name}: {type} uses its value/null converter"
                        );
                    }

                    foreach (JsonPropertyInfo property in info.Properties)
                    {
                        Visit(property.PropertyType);
                    }

                    if (type.IsGenericType)
                    {
                        foreach (Type argument in type.GetGenericArguments())
                        {
                            Visit(argument);
                        }
                    }
                    if (type.IsArray)
                    {
                        Visit(type.GetElementType());
                    }
                }
                foreach (
                    CustomAttributeData root in context
                        .GetType()
                        .GetCustomAttributesData()
                        .Where(a => a.AttributeType == typeof(JsonSerializableAttribute))
                )
                {
                    Visit((Type)root.ConstructorArguments[0].Value);
                }
            }
        }

        private void Compatible<T>(T value, JsonTypeInfo<T> contract, bool indented)
        {
            JsonSerializerOptions legacy = Legacy(indented, enums: true);
            string before = JsonSerializer.Serialize(value, legacy);
            string after = JsonSerializer.Serialize(value, contract);
            Check(
                string.Equals(before, after, StringComparison.Ordinal),
                $"{typeof(T).Name} keeps its exact JSON format"
            );
            T read = JsonSerializer.Deserialize(before, contract);
            Check(
                string.Equals(
                    JsonSerializer.Serialize(read, contract),
                    after,
                    StringComparison.Ordinal
                ),
                $"{typeof(T).Name} reads the legacy format"
            );
        }

        // Explicit reflection is confined to this compatibility oracle, reproducing the previous writer.
        private static JsonSerializerOptions Legacy(bool indented, bool enums)
        {
            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = indented,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                Converters = { new LegacyMaybeFactory() },
            };
            if (enums)
            {
                options.Converters.Add(new JsonStringEnumConverter());
            }

            return options;
        }

        private sealed class LegacyMaybeFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert.IsGenericType
                    && typeToConvert.GetGenericTypeDefinition() == typeof(Maybe<>);
            }

            public override JsonConverter CreateConverter(
                Type typeToConvert,
                JsonSerializerOptions options
            )
            {
                return (JsonConverter)
                    Activator.CreateInstance(
                        typeof(MaybeJsonConverter<>).MakeGenericType(
                            typeToConvert.GetGenericArguments()[0]
                        )
                    );
            }
        }
    }
}
