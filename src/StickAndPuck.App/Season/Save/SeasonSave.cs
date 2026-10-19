using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Save
{
    /// <summary>The season save file: one indented JSON file holding a <see cref="SeasonState"/>. It lives in
    /// the user's settings folder, so it survives restarts, rebuilds and a fresh single-file unpack:
    /// ~/.config/StickAndPuck/season.json on Linux, %APPDATA%\StickAndPuck\season.json on Windows,
    /// ~/Library/Application Support/StickAndPuck/season.json on macOS. SAP_SAVE=path overrides it.
    /// Writes go to a temporary file first and then replace the save, so a crash never leaves half a file.</summary>
    public static class SeasonSave
    {
        /// <summary>Gets the save file path: SAP_SAVE, or season.json in the user's settings folder.</summary>
        public static string DefaultPath =>
            Env.Get("SAP_SAVE") is { Length: > 0 } p
                ? Path.GetFullPath(p)
                : SavedFiles.DefaultPath("season.json");

        /// <summary>Gets a league's season save: <see cref="DefaultPath"/> for the SHL (so a save from before
        /// league choice is the SHL save), season_nhl.json beside it for the NHL.</summary>
        public static string PathFor(League league)
        {
            return Leagues.SavePath(league, DefaultPath);
        }

        /// <summary>Gets a value indicating whether a season save exists, for the start screen's Continue.</summary>
        public static bool Exists()
        {
            return Exists(DefaultPath);
        }

        /// <summary>Gets a value indicating whether a season save exists, for the start screen's Continue.</summary>
        public static bool Exists(string path)
        {
            return SavedFiles.Exists(path);
        }

        /// <summary>Writes the season, replacing any earlier save.</summary>
        public static void Write(SeasonState season)
        {
            Write(season, DefaultPath);
        }

        /// <summary>Writes the season, replacing any earlier save.</summary>
        public static void Write(SeasonState season, string path)
        {
            if (DeviceInput.Playing)
            {
                return; // a replay never touches the saves
            }

            season.Saved = DateTime.UtcNow;
            SavedFiles.WriteText(
                path,
                JsonSerializer.Serialize(season, SaveJsonContext.Default.SeasonState)
            );
        }

        /// <summary>Reads the saved season.</summary>
        /// <returns>The season, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
        public static Maybe<SeasonState> Read()
        {
            return Read(DefaultPath);
        }

        /// <summary>Reads the saved season.</summary>
        /// <returns>The season, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
        public static Maybe<SeasonState> Read(string path)
        {
            if (!SavedFiles.Exists(path))
            {
                return Maybe.None<SeasonState>();
            }

            try
            {
                if (
                    JsonSerializer.Deserialize(
                        SavedFiles.ReadText(path),
                        SaveJsonContext.Default.SeasonState
                    )
                        is not { } season
                    || season.Version > SeasonState.CurrentVersion
                    || season.Games.Count == 0
                )
                {
                    return Maybe.None<SeasonState>();
                }

                if (season.Morale.Count == 0 && season.Games.Any(g => g.Played))
                {
                    TeamMorale.Rebuild(season); // a save from before team morale: work it out from the results
                }

                return season;
            }
            catch (JsonException e)
            {
                Console.WriteLine($"SAP: season save {path} could not be read ({e.Message})");
                return Maybe.None<SeasonState>();
            }
        }

        /// <summary>Removes the save, for New season on the start screen.</summary>
        public static void Delete()
        {
            Delete(DefaultPath);
        }

        /// <summary>Removes the save, for New season on the start screen.</summary>
        public static void Delete(string path)
        {
            if (!DeviceInput.Playing)
            {
                SavedFiles.Delete(path);
            }
        }
    }
}
