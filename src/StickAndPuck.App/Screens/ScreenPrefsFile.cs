using System.Text.Json;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>Reads and writes <see cref="ScreenPrefs"/> as settings.json next to the season save:
    /// ~/.config/StickAndPuck/settings.json on Linux, %APPDATA%\StickAndPuck\settings.json on Windows.
    /// SAP_SETTINGS=path overrides it.</summary>
    internal static class ScreenPrefsFile
    {
        /// <summary>Gets the settings file path.</summary>
        public static string DefaultPath =>
            Env.Get("SAP_SETTINGS") is { Length: > 0 } p
                ? Path.GetFullPath(p)
                : SavedFiles.DefaultPath("settings.json");

        /// <summary>Reads the saved settings from <see cref="DefaultPath"/>, or the defaults when there is no readable file.</summary>
        public static ScreenPrefs Read()
        {
            return Read(DefaultPath);
        }

        /// <summary>Reads the saved settings, or the defaults when there is no readable file.</summary>
        public static ScreenPrefs Read(string path)
        {
            try
            {
                if (SavedFiles.Exists(path) && FromJson(SavedFiles.ReadText(path)) is var prefs)
                {
                    return prefs with { PeriodMinutes = Math.Clamp(prefs.PeriodMinutes, 1, 20) };
                }
            }
            catch (Exception e)
                when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                Console.WriteLine(
                    $"SAP: settings: {path} unreadable ({e.Message}), using defaults"
                );
            }
            return new ScreenPrefs();
        }

        /// <summary>Gets the settings as the file stores them (a replay keeps them).</summary>
        public static string ToJson(ScreenPrefs prefs)
        {
            return JsonSerializer.Serialize(prefs, SaveJsonContext.Default.ScreenPrefs);
        }

        /// <summary>Reads settings <see cref="ToJson"/> wrote.</summary>
        public static ScreenPrefs FromJson(string json)
        {
            ScreenPrefs prefs =
                JsonSerializer.Deserialize(json, SaveJsonContext.Default.ScreenPrefs)
                ?? new ScreenPrefs();
            return OldVolume(json).TryGet(out float volume)
                ? prefs with
                {
                    Settings = prefs.Settings with { EffectsVolume = volume },
                }
                : prefs;
        }

        /// <summary>The single "volume" settings saved before the overall, effects and music volumes: it is the effects volume now.</summary>
        private static Maybe<float> OldVolume(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Maybe.None<float>();
            }

            List<JsonElement> settings =
            [
                .. doc
                    .RootElement.EnumerateObject()
                    .Where(p => Named(p, "settings") && p.Value.ValueKind == JsonValueKind.Object)
                    .Select(p => p.Value),
            ];
            if (
                settings.Count == 0
                || settings[0].EnumerateObject().Any(p => Named(p, nameof(Settings.EffectsVolume)))
            )
            {
                return Maybe.None<float>();
            }

            foreach (JsonProperty p in settings[0].EnumerateObject())
            {
                if (
                    Named(p, "volume")
                    && p.Value.ValueKind == JsonValueKind.Number
                    && p.Value.TryGetSingle(out float volume)
                )
                {
                    return Math.Clamp(volume, 0, 1);
                }
            }
            return Maybe.None<float>();
        }

        private static bool Named(JsonProperty p, string name)
        {
            return string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Writes the settings to <see cref="DefaultPath"/>; a failure is logged, never fatal.</summary>
        public static void Write(ScreenPrefs prefs)
        {
            if (DeviceInput.Playing)
            {
                return; // a replay's menus never touch the saved settings (nor create the default folder)
            }

            Write(prefs, DefaultPath);
        }

        /// <summary>Writes the settings; a failure is logged, never fatal.</summary>
        public static void Write(ScreenPrefs prefs, string path)
        {
            if (DeviceInput.Playing)
            {
                return; // a replay's menus never touch the saved settings
            }

            try
            {
                SavedFiles.WriteText(
                    path,
                    JsonSerializer.Serialize(prefs, SaveJsonContext.Default.ScreenPrefs)
                );
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"SAP: settings: could not write {path} ({e.Message})");
            }
        }
    }
}
