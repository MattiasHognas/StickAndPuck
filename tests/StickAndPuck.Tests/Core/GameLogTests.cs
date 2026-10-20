using System.Diagnostics;
using System.Globalization;
using System.Text;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>
    /// The logs next to the settings: each launch logs to a file of its own named by its start time, logs
    /// older than 30 days are deleted, writing never waits on the disk (a flood drops lines instead), lines
    /// arrive in order and are all written on dispose, console lines reach the file, and Release builds show
    /// raylib's warnings and errors only.
    /// </summary>
    public sealed class GameLogTests : CheckedTest
    {
        private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Local);

        private readonly string dir = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap_gamelogtest_{Environment.ProcessId}_{Guid.NewGuid():N}"
            )
        );

        private string Logs => Path.Combine(dir, GameLog.FolderName);

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
            return File.ReadAllText(Path.Combine(Logs, name));
        }

        private void Launch(DateTime started, string line)
        {
            Check(GameLog.Open(dir, started).TryGet(out LogFile log), "the log file opens");
            using (log)
            {
                log.WriteLine(line);
            }
        }

        [Test]
        public void EachLaunchGetsItsOwnLog()
        {
            DateTime started = new(2026, 10, 20, 22, 55, 3, DateTimeKind.Local);
            Launch(started, "first launch");
            Check(
                Same(LogText("2026-10-20_22-55-03.txt"), "first launch" + Environment.NewLine),
                "the logs folder is made and the launch logs to a file named by its start time"
            );

            Launch(started, "same second");
            Check(
                Same(LogText("2026-10-20_22-55-03_2.txt"), "same second" + Environment.NewLine)
                    && Same(
                        LogText("2026-10-20_22-55-03.txt"),
                        "first launch" + Environment.NewLine
                    ),
                "a launch in the same second (a game started again on another card) gets a file of its own"
            );
        }

        [Test]
        public void LogsOlderThanThirtyDaysAreDeleted()
        {
            _ = Directory.CreateDirectory(Logs);
            string[] names =
            [
                "2026-09-01_08-00-00.txt",
                "2026-09-20_11-59-59.txt",
                "2026-09-20_12-00-01.txt",
                "2026-10-19_23-00-00_2.txt",
                "notes.txt",
            ];
            foreach (string name in names)
            {
                File.WriteAllText(Path.Combine(Logs, name), "x");
            }

            int deleted = GameLog.Prune(Logs, Noon);
            List<string> left =
            [
                .. Directory.GetFiles(Logs).Select(Path.GetFileName).Order(StringComparer.Ordinal),
            ];
            Check(
                deleted == 2
                    && left.SequenceEqual(
                        ["2026-09-20_12-00-01.txt", "2026-10-19_23-00-00_2.txt", "notes.txt"],
                        StringComparer.Ordinal
                    ),
                $"logs from more than 30 days ago go, newer logs and other files stay ({string.Join(", ", left)})"
            );
        }

        [Test]
        public void LinesArriveInOrder()
        {
            StringWriter target = new();
            StringBuilder expected = new();
            using (LogFile log = new(target))
            {
                for (int i = 0; i < 5000; i++)
                {
                    string line = string.Create(CultureInfo.InvariantCulture, $"line {i}");
                    log.WriteLine(line);
                    _ = expected.Append(line).Append(Environment.NewLine);
                }
                Check(log.Dropped == 0, "a queue with room drops nothing");
            }
            Check(
                Same(target.ToString(), expected.ToString()),
                "every line arrives, in order, by the time the log is disposed"
            );
        }

        [Test]
        public void DisposeWritesTheLastLinesToTheFile()
        {
            Check(GameLog.Open(dir, Noon).TryGet(out LogFile log), "the log file opens");
            log.Write("partial ");
            log.WriteLine("line");
            log.WriteLine("last line before exit");
            log.Dispose();
            log.Dispose();
            Check(
                Same(
                    LogText("2026-10-20_12-00-00.txt"),
                    "partial line"
                        + Environment.NewLine
                        + "last line before exit"
                        + Environment.NewLine
                ),
                "disposing writes and flushes the queued lines (twice is harmless)"
            );
        }

        [Test]
        public void AFloodNeverBlocks()
        {
            using ManualResetEventSlim gate = new(initialState: false);
            StuckWriter stuck = new(gate);
            LogFile log = new(stuck, capacity: 64);
            Stopwatch clock = Stopwatch.StartNew();
            for (int i = 0; i < 200_000; i++)
            {
                log.WriteLine("flood");
            }
            clock.Stop();
            Check(
                clock.Elapsed < TimeSpan.FromSeconds(5),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"200000 lines queue without waiting on a stuck disk ({clock.ElapsedMilliseconds} ms)"
                )
            );
            Check(log.Dropped > 0, "lines past the queue's capacity are dropped and counted");
            gate.Set();
            log.Dispose();
            Check(
                stuck.Text.Contains("dropped", StringComparison.Ordinal),
                "the log notes that lines were dropped"
            );
        }

        [Test]
        public void ConsoleLinesReachTheLog()
        {
            StringWriter target = new();
            using StringWriter console = new();
            using (LogFile log = new(target))
            {
                using GameLog.Tee tee = new(console, log);
                tee.WriteLine("SAP: a line");
                tee.Write('x');
                tee.Write("y");
                tee.WriteLine();
            }
            string expected = "SAP: a line" + Environment.NewLine + "xy" + Environment.NewLine;
            Check(Same(console.ToString(), expected), "the console gets every line");
            Check(Same(target.ToString(), expected), "the log file gets the same lines");
        }

        [Test]
        public void UnwritableFolderLogsToTheConsoleOnly()
        {
            _ = Directory.CreateDirectory(dir);
            string blocked = Path.Combine(dir, "a file");
            File.WriteAllText(blocked, "");
            Check(
                !GameLog.Open(blocked, Noon).TryGet(out _),
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

        /// <summary>A disk that does not take writes until the gate opens.</summary>
        private sealed class StuckWriter(ManualResetEventSlim gate) : TextWriter
        {
            private readonly StringBuilder text = new();

            public override Encoding Encoding => Encoding.UTF8;

            public string Text => text.ToString();

            public override void Write(char value)
            {
                gate.Wait();
                _ = text.Append(value);
            }

            public override void Write(string value)
            {
                gate.Wait();
                _ = text.Append(value);
            }
        }
    }
}
