#if BROWSER
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using StickAndPuck.App.Replay;

namespace StickAndPuck.App.Core
{
    /// <summary>Browser entry points; JavaScript owns scheduling and the canvas.</summary>
    [SupportedOSPlatform("browser")]
    public static partial class BrowserHost
    {
        private const string VerificationFolder = "/replay-verification";
        private const string ChecksumsPath = VerificationFolder + "/checksums";

        private static Maybe<Game.Session> current;
        private static Maybe<double> previousTimestamp;
        private static string lastState = "",
            lastResult = "";

        /// <summary>Starts the game after browser resources and saved files are ready.</summary>
        [JSExport]
        public static void Start(string scene)
        {
            Begin(scene, "");
        }

        /// <summary>Starts an uploaded or previously recorded replay.</summary>
        [JSExport]
        public static void PlayReplay(string name, bool verify)
        {
            Begin("", ReplayPath(name), verify);
        }

        /// <summary>Scrubs a replay to one exact simulation step for cross-platform diagnosis.</summary>
        [JSExport]
        public static void ReplayAt(string name, int tick)
        {
            Begin("", ReplayPath(name), verify: true, tick);
        }

        private static void Begin(string scene, string replay, bool verify = false, int tick = -1)
        {
            Stop();
            DeviceInput.Reset();
            foreach ((string key, string _) in Env.Sap())
            {
                Env.Set(key, "");
            }

            AssetRoot.Enter();
            Env.Set("SAP_SCENE", scene);
            Env.Set("SAP_REPLAY", replay);
            Env.Set("SAP_AT", tick >= 0 ? "t" + tick.ToString(CultureInfo.InvariantCulture) : "");
            Env.Set("SAP_SCREENSHOT", verify ? "1" : "");
            if (verify)
            {
                Directory.CreateDirectory(VerificationFolder);
            }

            Env.Set("SAP_CHECKSUMS", verify ? ChecksumsPath : "");
            lastState = "";
            lastResult = "";
            if (scene.Length > 0 && !string.Equals(scene, Scenes.Default, StringComparison.Ordinal))
            {
                Env.Set("SAP_REPLAY_OUT", Path.Combine(ReplayRecorder.Folder, "browser.sapreplay"));
            }

            current = new Game.Session();
            previousTimestamp = Maybe.None<double>();
        }

        /// <summary>Lists completed replay files, newest first.</summary>
        [JSExport]
        public static string Replays()
        {
            return Directory.Exists(ReplayRecorder.Folder)
                ? string.Join(
                    '\n',
                    Directory
                        .GetFiles(ReplayRecorder.Folder, "*.sapreplay")
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .Select(Path.GetFileName)
                )
                : "";
        }

        /// <summary>Downloads a named replay, or a snapshot of the current recording when the name is empty.</summary>
        [JSExport]
        public static string DownloadReplay(string name)
        {
            string path =
                name.Length == 0 && current.TryGet(out Game.Session game)
                    ? game.ReplaySnapshot().Or("")
                    : ReplayPath(name);
            return path.Length > 0 && File.Exists(path)
                ? Convert.ToBase64String(File.ReadAllBytes(path))
                : "";
        }

        /// <summary>Validates and adds a replay uploaded by the player.</summary>
        [JSExport]
        public static string UploadReplay(string name, string base64)
        {
            string path = ReplayPath(name);
            byte[] bytes = Convert.FromBase64String(base64);
            Directory.CreateDirectory(ReplayRecorder.Folder);
            string temporary = path + ".upload";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                ReplayFile replay = ReplayFile.Read(temporary);
                if (replay.Header.Version != ReplayHeader.CurrentVersion || replay.LastTick < 0)
                {
                    throw new InvalidDataException(
                        "This replay has an unsupported version or no recorded steps."
                    );
                }

                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
            return Path.GetFileName(path);
        }

        private static string ReplayPath(string name)
        {
            return
                name.Length == 0
                || !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
                || !name.EndsWith(".sapreplay", StringComparison.OrdinalIgnoreCase)
                ? throw new ArgumentException("Choose a .sapreplay file.", nameof(name))
                : Path.Combine(ReplayRecorder.Folder, name);
        }

        /// <summary>Advances one rendered frame without blocking the browser's event loop.</summary>
        [JSExport]
        public static bool Frame(double timestamp)
        {
            float elapsed = previousTimestamp.TryGet(out double previous)
                ? (float)Math.Max(0, (timestamp - previous) / 1000)
                : 0;
            previousTimestamp = timestamp;
            return current.TryGet(out Game.Session game) && game.Frame(elapsed);
        }

        /// <summary>Reports the current tick, checksum and state for replay verification.</summary>
        [JSExport]
        public static string State()
        {
            return current.Select(game => game.State).Or(lastState);
        }

        /// <summary>Reports the completed replay's verification result.</summary>
        [JSExport]
        public static string ReplayResult()
        {
            return lastResult;
        }

        /// <summary>Returns every step's checksum after a verification run has stopped.</summary>
        [JSExport]
        public static string Checksums()
        {
            return File.Exists(ChecksumsPath) ? File.ReadAllText(ChecksumsPath) : "";
        }

        /// <summary>Reports the device frame for browser input diagnostics.</summary>
        [JSExport]
        public static string Input()
        {
            return DeviceInput.Current.Write();
        }

        /// <summary>Retains mouse clicks that begin and end between animation callbacks.</summary>
        [JSExport]
        public static void MousePressed(int button)
        {
            DeviceInput.NoteMousePress(button);
        }

        /// <summary>Finishes recording and closes the current game.</summary>
        [JSExport]
        public static void Stop()
        {
            if (current.TryGet(out Game.Session game))
            {
                lastState = game.State;
                game.Dispose();
                lastResult = game.ReplayResult;
            }
            current = Maybe.None<Game.Session>();
        }
    }
}
#endif
