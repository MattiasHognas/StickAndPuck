using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>
    /// The log file next to the settings: each launch starts log.txt afresh and keeps the previous one as
    /// log.prev.txt, a game started again on another card adds to it, console lines reach it, and Release
    /// builds show raylib's warnings and errors only.
    /// </summary>
    public sealed class GameLogTests : CheckedTest
    {
        private readonly string dir = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap_gamelogtest_{Environment.ProcessId}_{Guid.NewGuid():N}"
            )
        );

        [After(Test)]
        public void CleanUp()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private string LogText(string name)
        {
            return File.ReadAllText(Path.Combine(dir, name));
        }

        private void Launch(string line, bool append = false)
        {
            Check(GameLog.Open(dir, append).TryGet(out TextWriter log), "the log file opens");
            using (log)
            {
                log.WriteLine(line);
            }
        }

        [Test]
        public void EachLaunchKeepsThePreviousLog()
        {
            Launch("first launch");
            Check(
                Same(LogText(GameLog.FileName), "first launch" + Environment.NewLine),
                "the folder is made and the first launch writes log.txt"
            );
            Check(
                !File.Exists(Path.Combine(dir, GameLog.PreviousFileName)),
                "there is no previous log yet"
            );

            Launch("second launch");
            Launch("third launch");
            Check(
                Same(LogText(GameLog.FileName), "third launch" + Environment.NewLine),
                "a launch starts log.txt afresh"
            );
            Check(
                Same(LogText(GameLog.PreviousFileName), "second launch" + Environment.NewLine),
                "the launch before is kept as log.prev.txt, older ones are dropped"
            );

            Launch("on another card", append: true);
            Check(
                Same(
                    LogText(GameLog.FileName),
                    "third launch" + Environment.NewLine + "on another card" + Environment.NewLine
                ),
                "a game started again on another card adds to the log"
            );
        }

        [Test]
        public void ConsoleLinesReachTheLog()
        {
            Check(GameLog.Open(dir, append: false).TryGet(out TextWriter log), "the log opens");
            using StringWriter console = new();
            using (log)
            {
                using GameLog.Tee tee = new(console, log);
                tee.WriteLine("SAP: a line");
                tee.Write('x');
                tee.Write("y");
                tee.WriteLine();
            }
            string expected = "SAP: a line" + Environment.NewLine + "xy" + Environment.NewLine;
            Check(Same(console.ToString(), expected), "the console gets every line");
            Check(Same(LogText(GameLog.FileName), expected), "the log file gets the same lines");
        }

        [Test]
        public void UnwritableFolderLogsToTheConsoleOnly()
        {
            _ = Directory.CreateDirectory(dir);
            string blocked = Path.Combine(dir, "a file");
            File.WriteAllText(blocked, "");
            Check(
                !GameLog.Open(blocked, append: false).TryGet(out _),
                "a folder that cannot be made gives no log file, and no crash"
            );
        }

        [Test]
        public void ReleaseShowsRaylibWarningsAndUp()
        {
#if DEBUG
            Check(
                GameLog.DefaultRaylibLevel == TraceLogLevel.All,
                "Debug builds show raylib's log"
            );
#else
            Check(
                GameLog.DefaultRaylibLevel == TraceLogLevel.Warning,
                "Release builds show raylib's warnings and errors only"
            );
#endif
            Check(
                GameLog.RaylibLevel("", TraceLogLevel.Warning) == TraceLogLevel.Warning,
                "without SAP_RAYLIB_LOG the build's level holds"
            );
            Check(
                GameLog.RaylibLevel("info", TraceLogLevel.Warning) == TraceLogLevel.Info,
                "SAP_RAYLIB_LOG=info brings raylib's info lines back"
            );
            Check(
                GameLog.RaylibLevel("loud", TraceLogLevel.Warning) == TraceLogLevel.Warning
                    && GameLog.RaylibLevel("3", TraceLogLevel.Warning) == TraceLogLevel.Warning,
                "an unknown SAP_RAYLIB_LOG keeps the build's level"
            );
            Check(
                GameLog.RaylibToFile(TraceLogLevel.Warning)
                    && GameLog.RaylibToFile(TraceLogLevel.Error)
                    && !GameLog.RaylibToFile(TraceLogLevel.Info),
                "raylib's warnings and errors go to the log file, its info lines do not"
            );
            Check(
                Same(
                    GameLog.RaylibText(TraceLogLevel.Warning, "TEXTURE: failed"),
                    "WARNING: TEXTURE: failed"
                ),
                "a raylib line reads as raylib prints it"
            );
        }
    }
}
