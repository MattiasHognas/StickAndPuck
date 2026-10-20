namespace StickAndPuck.App.Core
{
    /// <summary>Player options from the settings screen, with the defaults from the plan's Decisions. Every
    /// switch lives here; systems only read them from <see cref="World.Settings"/>.
    /// <see cref="FromEnvironment()"/> lays the SAP_* variables over them for headless runs.</summary>
    public sealed record Settings
    {
        /// <summary>Gets how control moves between players.</summary>
        public ControlSwitching Switching { get; init; } = ControlSwitching.PassesOnly;

        /// <summary>Gets how lines change.</summary>
        public LineChanges Lines { get; init; } = LineChanges.Automatic;

        /// <summary>Gets the AI difficulty (SAP_DIFFICULTY=easy|normal|hard).</summary>
        public Difficulty Difficulty { get; init; } = Difficulty.Normal;

        /// <summary>Gets a value indicating whether the gameplay camera zooms with speed (SAP_CAMERA_ZOOM=0|1). Off by default: zooming softens the ad board text. F6 toggles it in play.</summary>
        public bool CameraZoom { get; init; }

        /// <summary>Gets the lighting mood matches start with (SAP_LIGHTING=early|afternoon|late|random).</summary>
        public LightingMood Lighting { get; init; } = LightingMood.Random;

        /// <summary>Gets a value indicating whether fights can start.</summary>
        public bool Fights { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls penalties.</summary>
        public bool Penalties { get; init; } = true;

        /// <summary>Gets a value indicating whether a marker shows under each controlled skater (SAP_CONTROL_MARKER=0|1).</summary>
        public bool ControlMarker { get; init; } = true;

        /// <summary>Gets a value indicating whether a season game can be simmed instead of played (SAP_SIM_GAME=0|1).</summary>
        public bool SimGame { get; init; } = true;

        /// <summary>Gets a value indicating whether dekes happen on their own; off means the deke button only (SAP_AUTO_DEKE=0|1).</summary>
        public bool AutoDeke { get; init; }

        /// <summary>Gets a value indicating whether a shot straight off a pass fires as a one-timer (SAP_ONE_TIMERS=0|1).</summary>
        public bool OneTimers { get; init; } = true;

        /// <summary>Gets a value indicating whether the home and away kits are swapped (SAP_SWAP_KITS=0|1). Tab toggles it in play.</summary>
        public bool SwapKits { get; init; }

        /// <summary>Gets a value indicating whether both clubs' benches show their players (SAP_BENCHES=0|1). Off leaves the benches empty; the match plays the same either way.</summary>
        public bool Benches { get; init; } = true;

        /// <summary>Gets a value indicating whether the fans hold up their club's scarves, flags and banners and the home tifo goes up (SAP_SUPPORTERS=0|1). Presentation only.</summary>
        public bool Supporters { get; init; } = true;

        /// <summary>Gets a value indicating whether the fps and key help line shows top left (SAP_FPS=0|1).</summary>
        public bool FpsLine { get; init; }

        /// <summary>Gets a value indicating whether the plain score and clock debug line shows (SAP_MATCH_DEBUG=0|1).</summary>
        public bool MatchDebug { get; init; }

        /// <summary>Gets a value indicating whether the energy panel showing each unit on the ice shows (SAP_LINES_DEBUG=0|1).</summary>
        public bool LinesDebug { get; init; }

        /// <summary>Gets the language the game shows (SAP_LANGUAGE=english|swedish).</summary>
        public Language Language { get; init; } = Language.English;

        /// <summary>Gets the overall volume, 0 to 1: Raylib's master volume, over the effects and the music.</summary>
        public float OverallVolume { get; init; } = 1f;

        /// <summary>Gets the sound effect volume, 0 to 1 (the crowd too). A settings.json from before there were three
        /// volumes kept only "volume"; <see cref="Screens.ScreenPrefsFile"/> carries it over to this.</summary>
        public float EffectsVolume { get; init; } = 0.8f;

        /// <summary>Gets the music volume, 0 to 1 (the menu, intermission and end-of-game tracks).</summary>
        public float MusicVolume { get; init; } = 0.6f;

        /// <summary>Gets the graphics card the game draws on, by <see cref="GpuCard.Id"/> (SAP_GPU=id or part of the
        /// card's name). "" is the machine's default card. <see cref="GpuLaunch"/> starts the game on it.</summary>
        public string Gpu { get; init; } = "";

        /// <summary>Gets the rebound keys and gamepad buttons; an action missing here keeps its default. Read them with <see cref="KeyBindings.Of"/>.</summary>
        public IReadOnlyDictionary<GameAction, KeyBinding> Keys { get; init; } =
            new Dictionary<GameAction, KeyBinding>();

        /// <summary>Lays the SAP_* overrides over these settings: SAP_DIFFICULTY, SAP_CAMERA_ZOOM, SAP_LIGHTING,
        /// SAP_CONTROL_MARKER, SAP_SIM_GAME, SAP_AUTO_DEKE, SAP_ONE_TIMERS, SAP_SWAP_KITS, SAP_BENCHES, SAP_SUPPORTERS, SAP_FPS,
        /// SAP_MATCH_DEBUG, SAP_LINES_DEBUG, SAP_LANGUAGE and SAP_GPU. A variable that is unset or empty changes nothing, so
        /// the saved settings stay in charge; a bad value is an error, like SAP_LIGHTING always was.</summary>
        public static Settings FromEnvironment()
        {
            return FromEnvironment(new Settings());
        }

        /// <summary>Lays the SAP_* overrides over saved settings, as <see cref="FromEnvironment()"/> does over the defaults.</summary>
        public static Settings FromEnvironment(Settings saved)
        {
            Settings s = saved;
            s = ApplyFlags(
                s,
                ("SAP_CAMERA_ZOOM", (settings, zoom) => settings with { CameraZoom = zoom }),
                (
                    "SAP_CONTROL_MARKER",
                    (settings, marker) => settings with { ControlMarker = marker }
                ),
                ("SAP_SIM_GAME", (settings, sim) => settings with { SimGame = sim }),
                ("SAP_AUTO_DEKE", (settings, deke) => settings with { AutoDeke = deke }),
                (
                    "SAP_ONE_TIMERS",
                    (settings, oneTimers) => settings with { OneTimers = oneTimers }
                ),
                ("SAP_SWAP_KITS", (settings, swap) => settings with { SwapKits = swap }),
                ("SAP_BENCHES", (settings, benches) => settings with { Benches = benches }),
                (
                    "SAP_SUPPORTERS",
                    (settings, supporters) => settings with { Supporters = supporters }
                ),
                ("SAP_FPS", (settings, fps) => settings with { FpsLine = fps }),
                ("SAP_MATCH_DEBUG", (settings, match) => settings with { MatchDebug = match }),
                ("SAP_LINES_DEBUG", (settings, lines) => settings with { LinesDebug = lines })
            );
            s = ApplyChoices<LightingMood>(
                s,
                ("SAP_LIGHTING", (settings, mood) => settings with { Lighting = mood })
            );
            s = ApplyChoices<Difficulty>(
                s,
                ("SAP_DIFFICULTY", (settings, level) => settings with { Difficulty = level })
            );
            s = ApplyChoices<Language>(
                s,
                ("SAP_LANGUAGE", (settings, language) => settings with { Language = language })
            );
            if (Env.Get("SAP_GPU") is { Length: > 0 } gpu)
            {
                s = s with { Gpu = gpu.Trim() };
            }

            return s;
        }

        private static Settings ApplyFlags(
            Settings settings,
            params (string Name, Func<Settings, bool, Settings> Apply)[] overrides
        )
        {
            foreach ((string name, Func<Settings, bool, Settings> apply) in overrides)
            {
                settings = ApplyFlag(settings, name, apply);
            }

            return settings;
        }

        private static Settings ApplyChoices<T>(
            Settings settings,
            params (string Name, Func<Settings, T, Settings> Apply)[] overrides
        )
            where T : struct, Enum
        {
            foreach ((string name, Func<Settings, T, Settings> apply) in overrides)
            {
                settings = ApplyChoice(settings, name, apply);
            }

            return settings;
        }

        private static Settings ApplyFlag(
            Settings settings,
            string name,
            Func<Settings, bool, Settings> apply
        )
        {
            return Flag(name).TryGet(out bool value) ? apply(settings, value) : settings;
        }

        private static Settings ApplyChoice<T>(
            Settings settings,
            string name,
            Func<Settings, T, Settings> apply
        )
            where T : struct, Enum
        {
            return Choice<T>(name).TryGet(out T value) ? apply(settings, value) : settings;
        }

        /// <summary>Reads a 0/1 variable; "on", "off", "true" and "false" work too.</summary>
        private static Maybe<bool> Flag(string name)
        {
            return Env.Get(name).Trim().ToLowerInvariant() switch
            {
                "" => Maybe.None<bool>(),
                "1" or "on" or "true" or "yes" => true,
                "0" or "off" or "false" or "no" => false,
                var other => throw new InvalidOperationException($"{name}={other}: use 0 or 1"),
            };
        }

        private static Maybe<T> Choice<T>(string name)
            where T : struct, Enum
        {
            string value = Env.Get(name);
            return string.IsNullOrWhiteSpace(value) ? Maybe.None<T>()
                : Enum.TryParse(value.Trim(), ignoreCase: true, out T parsed) ? (Maybe<T>)parsed
                : throw new InvalidOperationException(
                    $"{name}={value}: use {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}"
                );
        }
    }
}
