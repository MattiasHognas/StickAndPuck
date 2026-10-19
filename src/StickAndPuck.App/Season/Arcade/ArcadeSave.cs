using System.Text.Json;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Arcade
{
    /// <summary>The arcade save: one indented JSON file holding an <see cref="ArcadeRun"/>, next to the season
    /// save (arcade.json in the user's settings folder), so a run and a season never overwrite each
    /// other. SAP_ARCADE_SAVE=path overrides it. Writes replace the file in one move, as the season save does.</summary>
    public static class ArcadeSave
    {
        /// <summary>Gets the save file path: SAP_ARCADE_SAVE, or arcade.json in the user's settings folder.</summary>
        public static string DefaultPath =>
            Env.Get("SAP_ARCADE_SAVE") is { Length: > 0 } p
                ? Path.GetFullPath(p)
                : SavedFiles.DefaultPath("arcade.json");

        /// <summary>Gets a league's arcade save: <see cref="DefaultPath"/> for the SHL (so a run from before
        /// league choice is the SHL run), arcade_nhl.json beside it for the NHL.</summary>
        public static string PathFor(League league)
        {
            return Leagues.SavePath(league, DefaultPath);
        }

        /// <summary>Gets a value indicating whether an arcade save exists (finished or not).</summary>
        public static bool Exists()
        {
            return Exists(DefaultPath);
        }

        /// <summary>Gets a value indicating whether an arcade save exists (finished or not).</summary>
        public static bool Exists(string path)
        {
            return SavedFiles.Exists(path);
        }

        /// <summary>Writes the run, replacing any earlier save.</summary>
        public static void Write(ArcadeRun run)
        {
            Write(run, DefaultPath);
        }

        /// <summary>Writes the run, replacing any earlier save.</summary>
        public static void Write(ArcadeRun run, string path)
        {
            if (DeviceInput.Playing)
            {
                return; // a replay never touches the saves
            }

            run.Saved = DateTime.UtcNow;
            SavedFiles.WriteText(
                path,
                JsonSerializer.Serialize(run, SaveJsonContext.Default.ArcadeRun)
            );
        }

        /// <summary>Reads the saved run.</summary>
        /// <returns>The run, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
        public static Maybe<ArcadeRun> Read()
        {
            return Read(DefaultPath);
        }

        /// <summary>Reads the saved run.</summary>
        /// <returns>The run, or None when there is no save, it cannot be read, or it is from a newer version.</returns>
        public static Maybe<ArcadeRun> Read(string path)
        {
            if (!SavedFiles.Exists(path))
            {
                return Maybe.None<ArcadeRun>();
            }

            try
            {
                return
                    JsonSerializer.Deserialize(
                        SavedFiles.ReadText(path),
                        SaveJsonContext.Default.ArcadeRun
                    )
                        is not { } run
                    || run.Version > ArcadeRun.CurrentVersion
                    || run.Opponents.Count == 0
                    ? Maybe.None<ArcadeRun>()
                    : (Maybe<ArcadeRun>)run;
            }
            catch (JsonException e)
            {
                Console.WriteLine($"SAP: arcade save {path} could not be read ({e.Message})");
                return Maybe.None<ArcadeRun>();
            }
        }

        /// <summary>Removes the save.</summary>
        public static void Delete()
        {
            Delete(DefaultPath);
        }

        /// <summary>Removes the save.</summary>
        public static void Delete(string path)
        {
            if (!DeviceInput.Playing)
            {
                SavedFiles.Delete(path);
            }
        }
    }
}
