using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Raylib_cs;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Core
{
    /// <summary>Where the log goes. A published Windows game has no console of its own, so one started from a
    /// terminal attaches to that terminal's console. Every console line (the game's "SAP: ..." lines and raylib's
    /// warnings and errors) also goes to a log file of its own per launch, named by its local start time, in
    /// logs/ next to settings.json (~/.config/StickAndPuck/logs/ on Linux, %APPDATA%\StickAndPuck\logs\ on
    /// Windows), written off the game thread (<see cref="LogFile"/>); logs older than <see cref="KeptDays"/>
    /// days are deleted in the background at startup. Release builds show raylib's warnings and errors only,
    /// Debug builds everything; SAP_RAYLIB_LOG=info (or trace, debug, warning, error, none) overrides it. The
    /// browser build logs to the browser console only.</summary>
    public static partial class GameLog
    {
        /// <summary>The folder next to settings.json that holds the logs.</summary>
        public const string FolderName = "logs";

        /// <summary>How many days of logs are kept.</summary>
        public const int KeptDays = 30;

        private const string Stamp = "yyyy-MM-dd_HH-mm-ss";
        private const int AttachParentProcess = -1;

        private static TextWriter console = TextWriter.Null;

        /// <summary>Gets raylib's log level when SAP_RAYLIB_LOG does not set one: warnings and up in Release, all in Debug.</summary>
        public static TraceLogLevel DefaultRaylibLevel =>
#if DEBUG
            TraceLogLevel.All;
#else
            TraceLogLevel.Warning;
#endif

        /// <summary>Starts logging for this run: attaches to a parent console on Windows, opens the log file and
        /// sets raylib's log level. Call it first thing.</summary>
        public static void Start()
        {
            if (OperatingSystem.IsWindows())
            {
                // Before the first Console write, so .NET picks up the attached console's handles.
                _ = AttachConsole(AttachParentProcess);
            }

            Raylib.SetTraceLogLevel(RaylibLevel(Env.Get("SAP_RAYLIB_LOG"), DefaultRaylibLevel));
            if (OperatingSystem.IsBrowser())
            {
                return;
            }

            string folder = Path.GetDirectoryName(ScreenPrefsFile.DefaultPath);
            DateTime now = DateTime.Now;
            console = Console.Out;
            if (Open(folder, now).TryGet(out LogFile log))
            {
                Console.SetOut(TextWriter.Synchronized(new Tee(console, log)));
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    log.WriteLine($"SAP: crash: {e.ExceptionObject}");
                    log.Dispose();
                };
                AppDomain.CurrentDomain.ProcessExit += (_, _) => log.Dispose();
                _ = Task.Run(() => Prune(Path.Combine(folder, FolderName), now));
            }
#if !BROWSER
#pragma warning disable S6640 // Raylib interop requires a function pointer here.
            unsafe
            {
                Raylib.SetTraceLogCallback(&RaylibLine);
            }
#pragma warning restore S6640
#endif
        }

        /// <summary>Reads SAP_RAYLIB_LOG ("info", "warning", ...); anything else keeps the default.</summary>
        public static TraceLogLevel RaylibLevel(string value, TraceLogLevel fallback)
        {
            return
                Enum.TryParse(value, ignoreCase: true, out TraceLogLevel level)
                && Enum.IsDefined(level)
                && !int.TryParse(value, CultureInfo.InvariantCulture, out _)
                ? level
                : fallback;
        }

        /// <summary>Opens a new log file for a launch that started at this local time:
        /// logs/2026-10-20_22-55-03.txt in the folder (with _2, _3, ... when that name is taken). None when the
        /// folder cannot be written; the game then logs to the console only.</summary>
        public static Maybe<LogFile> Open(string folder, DateTime started)
        {
            string logs = Path.Combine(folder, FolderName);
            try
            {
                _ = Directory.CreateDirectory(logs);
                string name = started.ToString(Stamp, CultureInfo.InvariantCulture);
                string path = Path.Combine(logs, name + ".txt");
                int n = 2;
                while (File.Exists(path))
                {
                    path = Path.Combine(
                        logs,
                        string.Create(CultureInfo.InvariantCulture, $"{name}_{n}.txt")
                    );
                    n++;
                }

                FileStream stream = new(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete
                );
                return new LogFile(
                    new StreamWriter(
                        stream,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                    )
                );
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"SAP: log: cannot write {logs} ({e.Message})");
                return Maybe.None<LogFile>();
            }
        }

        /// <summary>Deletes the logs in the folder that started more than <see cref="KeptDays"/> days before now
        /// (by the time in the name, else the file's last write). Other files stay.</summary>
        /// <returns>How many logs were deleted.</returns>
        public static int Prune(string logs, DateTime now)
        {
            int deleted = 0;
            try
            {
                foreach (string file in Directory.GetFiles(logs, "????-??-??_??-??-??*.txt"))
                {
                    DateTime started = DateTime.TryParseExact(
                        Path.GetFileName(file)[..Stamp.Length],
                        Stamp,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime named
                    )
                        ? named
                        : File.GetLastWriteTime(file);
                    if (now - started > TimeSpan.FromDays(KeptDays))
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"SAP: log: cleaning {logs} stopped ({e.Message})");
            }
            return deleted;
        }

        /// <summary>Gets the line raylib's log shows for a message at a level.</summary>
        public static string RaylibText(TraceLogLevel level, string message)
        {
            return level switch
                {
                    TraceLogLevel.Trace => "TRACE: ",
                    TraceLogLevel.Debug => "DEBUG: ",
                    TraceLogLevel.Info => "INFO: ",
                    TraceLogLevel.Warning => "WARNING: ",
                    TraceLogLevel.Error => "ERROR: ",
                    TraceLogLevel.Fatal => "FATAL: ",
                    TraceLogLevel.All or TraceLogLevel.None => "",
                    _ => "",
                } + message;
        }

        /// <summary>Gets a value indicating whether a raylib message at this level also goes to the log file.</summary>
        public static bool RaylibToFile(TraceLogLevel level)
        {
            return level >= TraceLogLevel.Warning;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AttachConsole(int processId);

#if !BROWSER
#pragma warning disable S6640 // raylib hands its log over as a C format string and va_list.
        [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static unsafe void RaylibLine(int level, sbyte* text, sbyte* args)
        {
            try
            {
                const int Size = 1024;
                byte* buffer = stackalloc byte[Size];
                int length = OperatingSystem.IsWindows()
                    ? WindowsFormat(buffer, Size, text, args)
                    : UnixFormat(buffer, Size, text, args);
                string message = Encoding.UTF8.GetString(buffer, Math.Clamp(length, 0, Size - 1));
                TraceLogLevel at = (TraceLogLevel)level;
                string line = RaylibText(at, message);
                if (RaylibToFile(at))
                {
                    Console.WriteLine(line);
                }
                else
                {
                    console.WriteLine(line);
                }
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // A log line that cannot be written is dropped.
            }
        }

        [LibraryImport("msvcrt.dll", EntryPoint = "vsnprintf")]
        private static unsafe partial int WindowsFormat(
            byte* buffer,
            nuint size,
            sbyte* format,
            sbyte* args
        );

        [LibraryImport("libc", EntryPoint = "vsnprintf")]
        private static unsafe partial int UnixFormat(
            byte* buffer,
            nuint size,
            sbyte* format,
            sbyte* args
        );
#pragma warning restore S6640
#endif

        /// <summary>Writes to the console as before and queues the same text for the log file.</summary>
        internal sealed class Tee(TextWriter console, LogFile log) : TextWriter
        {
            /// <inheritdoc/>
            public override Encoding Encoding => console.Encoding;

            /// <inheritdoc/>
            public override void Write(char value)
            {
                console.Write(value);
                log.Write(value.ToString());
            }

            /// <inheritdoc/>
            public override void Write(string value)
            {
                console.Write(value);
                log.Write(value);
            }

            /// <inheritdoc/>
            public override void WriteLine(string value)
            {
                console.WriteLine(value);
                log.WriteLine(value);
            }

            /// <inheritdoc/>
            public override void WriteLine()
            {
                console.WriteLine();
                log.WriteLine("");
            }

            /// <inheritdoc/>
            public override void Flush()
            {
                console.Flush();
            }
        }
    }
}
