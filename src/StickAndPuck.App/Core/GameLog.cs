using System.Runtime.InteropServices;
using System.Text;
using Raylib_cs;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Core
{
    /// <summary>Where the log goes. A published Windows game has no console of its own, so one started from a
    /// terminal attaches to that terminal's console. Every console line (the game's "SAP: ..." lines and raylib's
    /// warnings and errors) is also written to log.txt next to settings.json (~/.config/StickAndPuck/ on Linux,
    /// %APPDATA%\StickAndPuck\ on Windows); each launch starts it afresh and keeps the previous launch's as
    /// log.prev.txt. Release builds show raylib's warnings and errors only, Debug builds everything;
    /// SAP_RAYLIB_LOG=info (or trace, debug, warning, error, none) overrides it. The browser build logs to the
    /// browser console only.</summary>
    public static partial class GameLog
    {
        /// <summary>The log file's name.</summary>
        public const string FileName = "log.txt";

        /// <summary>The previous launch's log file's name.</summary>
        public const string PreviousFileName = "log.prev.txt";

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
            // A game GpuLaunch started on another card adds to the log of the game that started it.
            Maybe<TextWriter> file = Open(folder, append: Env.Get("SAP_GPU_LAUNCHED").Length > 0);
            console = Console.Out;
            if (file.TryGet(out TextWriter log))
            {
                Console.SetOut(TextWriter.Synchronized(new Tee(console, log)));
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
                && !int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _)
                ? level
                : fallback;
        }

        /// <summary>Opens log.txt in a folder: a fresh file whose predecessor becomes log.prev.txt, or, to append,
        /// the existing one. None when the folder cannot be written; the game then logs to the console only.</summary>
        public static Maybe<TextWriter> Open(string folder, bool append)
        {
            try
            {
                _ = Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, FileName);
                if (!append && File.Exists(path))
                {
                    File.Move(path, Path.Combine(folder, PreviousFileName), overwrite: true);
                }

                FileStream stream = new(
                    path,
                    append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete
                );
                return new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                )
                {
                    AutoFlush = true,
                };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"SAP: log: cannot write {folder} ({e.Message})");
                return Maybe.None<TextWriter>();
            }
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

        /// <summary>Writes everything to the console and to the log file.</summary>
        internal sealed class Tee(TextWriter console, TextWriter log) : TextWriter
        {
            /// <inheritdoc/>
            public override Encoding Encoding => console.Encoding;

            /// <inheritdoc/>
            public override void Write(char value)
            {
                Both(w => w.Write(value));
            }

            /// <inheritdoc/>
            public override void Write(string value)
            {
                Both(w => w.Write(value));
            }

            /// <inheritdoc/>
            public override void WriteLine(string value)
            {
                Both(w => w.WriteLine(value));
            }

            /// <inheritdoc/>
            public override void Flush()
            {
                Both(w => w.Flush());
            }

            private void Both(Action<TextWriter> write)
            {
                write(console);
                try
                {
                    write(log);
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException)
                {
                    // The console still gets the line when the log file stops taking them.
                }
            }
        }
    }
}
