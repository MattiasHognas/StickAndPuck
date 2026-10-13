using System.Globalization;
using System.Runtime.InteropServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Replays;

/// <summary>
/// W67: writes the replay of the match being played. Every match started from the screens is
/// recorded to the replays folder beside the settings (the newest <see cref="Keep"/> stay);
/// SAP_REPLAY_OUT=file records to that file instead, and also records a SAP_SCENE match;
/// SAP_REPLAY_OUT=0 records nothing. SAP_CHECKSUMS=file writes "tick checksum" for every step,
/// played or replayed, to compare two runs line by line.
/// </summary>
public sealed class ReplayRecorder
{
    /// <summary>The replays kept in the replays folder.</summary>
    public const int Keep = 50;

    /// <summary>The SAP_* variables that steer recording and replaying rather than the match, never stored or set again.</summary>
    internal static readonly string[] ControlVariables =
        ["SAP_REPLAY", "SAP_REPLAY_OUT", "SAP_STEPS", "SAP_AT", "SAP_TAKEOVER", "SAP_CHECKSUMS", "SAP_SCREENSHOT", "SAP_RECORD", "SAP_FRAMES", "SAP_SCREEN_KEYS", "SAP_SETTINGS", "SAP_SAVE"];

    readonly ReplayFile file;
    readonly string path;
    string last;

    ReplayRecorder(ReplayFile file, string path)
    {
        this.file = file;
        this.path = path;
        last = file.Start.Write();
    }

    /// <summary>Gets the folder screens matches are recorded to.</summary>
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "StickAndPuck", "replays");

    /// <summary>Gets the SAP_CHECKSUMS log, when it is open.</summary>
    internal static Maybe<StreamWriter> Sums { get; private set; }

    /// <summary>Opens the SAP_CHECKSUMS log once, when it is set.</summary>
    public static void OpenChecksums()
    {
        if (Sums.HasValue || Env.Get("SAP_CHECKSUMS") is not { Length: > 0 } f) return;
        Sums = new StreamWriter(AssetRoot.Output(f)) { AutoFlush = false };
        Console.WriteLine($"SAP: replay: checksum of every step to {AssetRoot.Output(f)}");
    }

    /// <summary>Writes one step's checksum to the SAP_CHECKSUMS log, when it is open.</summary>
    /// <param name="tick">The step.</param>
    /// <param name="sum">Its checksum.</param>
    public static void LogChecksum(long tick, ulong sum)
    {
        if (Sums.TryGet(out var sums)) sums.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{tick} {WorldChecksum.Text(sum)}"));
    }

    /// <summary>Closes the SAP_CHECKSUMS log.</summary>
    public static void CloseChecksums()
    {
        if (Sums.TryGet(out var sums)) sums.Dispose();
        Sums = Maybe<StreamWriter>.None;
    }

    /// <summary>
    /// Starts recording a world that is about to start, if it is a match worth recording: a match
    /// the screens started, or any SAP_SCENE world when SAP_REPLAY_OUT names a file.
    /// </summary>
    /// <param name="world">The new world; its systems have not started yet.</param>
    /// <param name="scene">The SAP_SCENE it came from, or "" for a world the screens built.</param>
    /// <returns>The recorder, or None.</returns>
    public static Maybe<ReplayRecorder> Begin(World world, string scene)
    {
        string target = Env.Get("SAP_REPLAY_OUT");
        if (string.Equals(target, "0", StringComparison.Ordinal)) return Maybe<ReplayRecorder>.None;
        bool built = scene.Length == 0;
        var playing = built ? ScreenFlow.Playing : Maybe<MatchPlan>.None;
        var started = built ? ScreenFlow.Started : Maybe<MatchSetup>.None;
        bool screensMatch = playing.HasValue && started.HasValue;
        if (!screensMatch && (built || target.Length == 0)) return Maybe<ReplayRecorder>.None;

        var header = new ReplayHeader
        {
            Seed = world.Seed,
            Kind = screensMatch ? "match" : "scene",
            Scene = scene,
            Env = Variables(),
            Setup = screensMatch ? started.Select(ReplaySetup.Of) : Maybe<ReplaySetup>.None,
            Mode = screensMatch ? playing.Select(ModeName).Or("") : "",
            Prefs = screensMatch ? ScreenPrefsFile.ToJson(ScreenFlow.Prefs) : "",
            Clubs = ClubsHash(world.Setup.Home, world.Setup.Away),
            Recorded = string.Create(CultureInfo.InvariantCulture, $"{Environment.MachineName}, {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, .NET {Environment.Version}, {DateTime.Now:yyyy-MM-dd HH:mm}"),
        };
        return Begin(header, DeviceInput.Current, target.Length == 0 ? NewPath(world) : AssetRoot.Output(target));
    }

    /// <summary>Starts recording with a given header and start frame (a take-over keeps the replay's own).</summary>
    /// <param name="header">The header.</param>
    /// <param name="start">The devices before the first step.</param>
    /// <param name="path">Where to write.</param>
    /// <returns>The recorder.</returns>
    public static ReplayRecorder Begin(ReplayHeader header, InputFrame start, string path)
    {
        Console.WriteLine($"SAP: replay: recording seed {header.Seed} to {path}");
        return new ReplayRecorder(new ReplayFile { Header = header, Start = start }, path);
    }

    /// <summary>Records the step that just ran: its device frame when it changed, its events, and every <see cref="ReplayFile.CheckEvery"/> steps its checksum.</summary>
    /// <param name="world">The world after the step.</param>
    /// <param name="sum">The world's checksum after the step.</param>
    public void AfterStep(World world, ulong sum)
    {
        long tick = world.Tick - 1;
        string frame = DeviceInput.Current.Write();
        if (!string.Equals(frame, last, StringComparison.Ordinal))
        {
            file.Inputs[tick] = DeviceInput.Current;
            last = frame;
        }
        foreach (var e in world.Events.Current) file.Events.Add(ReplayFile.EventLine(tick, e));
        if (tick % ReplayFile.CheckEvery == 0) file.Checks[tick] = sum;
        file.LastTick = tick;
        if (file.End.Length == 0 && ReplayFile.Ends(world)) file.End = ReplayFile.EndLine(world);
    }

    /// <summary>Writes the replay: the match so far, ending at the last recorded step.</summary>
    /// <param name="world">The world.</param>
    public void Finish(World world)
    {
        if (file.LastTick < 0) return;
        file.Checks[file.LastTick] = WorldChecksum.Of(world);
        if (file.End.Length == 0) file.End = ReplayFile.EndLine(world);
        file.Write(path);
        Console.WriteLine($"SAP: replay: {file.LastTick + 1} steps, {file.Events.Count} events, {file.Inputs.Count} input changes, final {file.End.Split(' ')[1]} written to {path}");
        if (string.Equals(Path.GetDirectoryName(path) ?? "", Folder, StringComparison.Ordinal)) Prune();
    }

    /// <summary>Gets the SAP_* variables now, without the ones that steer recording and replaying.</summary>
    static Dictionary<string, string> Variables()
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in Env.Sap)
            if (!ControlVariables.Contains(k, StringComparer.Ordinal)) vars[k] = v;
        return vars;
    }

    /// <summary>Gets the game mode's type name of a screens match, "" for a season game.</summary>
    static string ModeName(MatchPlan plan) => plan.Mode.Select(m => m.GetType().Name).Or("");

    /// <summary>Gets a short hash of two clubs' data.</summary>
    /// <param name="home">The home club.</param>
    /// <param name="away">The visiting club.</param>
    /// <returns>16 hex digits.</returns>
    internal static string ClubsHash(ClubData home, ClubData away)
    {
        ulong h = 14695981039346656037UL;
        foreach (char ch in Clubs.ToJson(home) + Clubs.ToJson(away))
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return WorldChecksum.Text(h);
    }

    static string NewPath(World world)
    {
        string name = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{world.Setup.Home.Id}-{world.Setup.Away.Id}.sapreplay");
        return Path.Combine(Folder, name);
    }

    static void Prune()
    {
        try
        {
            foreach (var old in new DirectoryInfo(Folder).GetFiles("*.sapreplay").OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(Keep)) old.Delete();
        }
        catch (IOException e)
        {
            Console.WriteLine($"SAP: replay: could not prune {Folder} ({e.Message})");
        }
    }
}
