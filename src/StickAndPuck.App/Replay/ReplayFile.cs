using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using static System.FormattableString;

namespace StickAndPuck.App.Replays;

/// <summary>The setup a match started from, with the clubs by id, as a replay stores it.</summary>
/// <param name="Home">The home club's id.</param>
/// <param name="Away">The visiting club's id.</param>
/// <param name="Inputs">The human input slots.</param>
/// <param name="PeriodSeconds">The period length.</param>
/// <param name="Periods">The regulation periods.</param>
/// <param name="Overtime">The overtime rule.</param>
/// <param name="OvertimeFormat">The mode's own overtime format, or None.</param>
/// <param name="Rules">The rules.</param>
public sealed record ReplaySetup(string Home, string Away, IReadOnlyList<InputSlot> Inputs, float PeriodSeconds, int Periods, OvertimeRule Overtime, Maybe<OvertimeFormat> OvertimeFormat, RuleSet Rules)
{
    /// <summary>Gets the stored form of a setup.</summary>
    /// <param name="s">The setup.</param>
    /// <returns>The stored setup.</returns>
    public static ReplaySetup Of(MatchSetup s) => new(s.Home.Id, s.Away.Id, s.Inputs, s.PeriodSeconds, s.Periods, s.Overtime, s.OvertimeFormat, s.Rules);

    /// <summary>Builds the setup again from the clubs on this machine.</summary>
    /// <param name="clubs">Every club.</param>
    /// <returns>The setup.</returns>
    public MatchSetup ToSetup(IReadOnlyList<ClubData> clubs)
    {
        ClubData Club(string id) => clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"the replay's club {id} is not in data/clubs");
        return new MatchSetup
        {
            Home = Club(Home), Away = Club(Away), Inputs = Inputs, PeriodSeconds = PeriodSeconds, Periods = Periods,
            Overtime = Overtime, OvertimeFormat = OvertimeFormat, Rules = Rules,
        };
    }
}

/// <summary>What a replay needs to build its match again; the first line of the file after the magic.</summary>
public sealed record ReplayHeader
{
    /// <summary>The file format version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets the file format version.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Gets the match seed.</summary>
    public int Seed { get; init; }

    /// <summary>Gets how the match was built: "match" (started from the screens) or "scene" (SAP_SCENE).</summary>
    public string Kind { get; init; } = "match";

    /// <summary>Gets the SAP_SCENE name for a scene replay.</summary>
    public string Scene { get; init; } = "";

    /// <summary>Gets the SAP_* variables the match ran with; a replay sets them again.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the setup a screens match started from (None for a scene).</summary>
    public Maybe<ReplaySetup> Setup { get; init; }

    /// <summary>Gets the game mode a screens match was played in, by its type name ("" for a season game).</summary>
    public string Mode { get; init; } = "";

    /// <summary>Gets the screens' saved settings at the start of a screens match, as JSON.</summary>
    public string Prefs { get; init; } = "";

    /// <summary>Gets a hash of the two clubs' data, so a replay can warn when a club file changed since.</summary>
    public string Clubs { get; init; } = "";

    /// <summary>Gets where and when it was recorded (shown, never used to replay).</summary>
    public string Recorded { get; init; } = "";
}

/// <summary>
/// W67: a replay file. Gzipped text, one record per line: the magic "SAPREPLAY 1", "h" and the
/// header as JSON, "f" and the device frame when the match started, then per step "i tick frame"
/// whenever the devices changed, "c tick checksum" every <see cref="CheckEvery"/> steps, "e tick
/// event" for every match event, and last "x tick score shots period clock" when the match ended.
/// </summary>
public sealed class ReplayFile
{
    /// <summary>Steps between stored checksums.</summary>
    public const int CheckEvery = 30;

    const string Magic = "SAPREPLAY 1";

    /// <summary>The JSON options for headers.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Gets or sets the header.</summary>
    public ReplayHeader Header { get; set; } = new();

    /// <summary>Gets or sets the devices when the match started, before its first step.</summary>
    public InputFrame Start { get; set; } = InputFrame.Empty;

    /// <summary>Gets the device frames by step, only where they changed.</summary>
    public SortedDictionary<long, InputFrame> Inputs { get; } = [];

    /// <summary>Gets the stored checksums by step.</summary>
    public Dictionary<long, ulong> Checks { get; } = [];

    /// <summary>Gets the event lines, "tick event".</summary>
    public List<string> Events { get; } = [];

    /// <summary>Gets or sets the end line, "tick score shots period clock", or empty when the recording was cut short.</summary>
    public string End { get; set; } = "";

    /// <summary>Gets or sets the last step recorded.</summary>
    public long LastTick { get; set; } = -1;

    /// <summary>Writes an event as a replay line.</summary>
    /// <param name="tick">The step.</param>
    /// <param name="e">The event.</param>
    /// <returns>The line, without the "e".</returns>
    public static string EventLine(long tick, MatchEvent e)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{tick} {EventText(e)}");
    }

    /// <summary>
    /// Writes an event as "Type time fields...", every float round-trip exact. Replay lines, the
    /// world checksum and the state dump all use it, so a new event type fails here until it has a line.
    /// </summary>
    /// <param name="e">The event.</param>
    /// <returns>The text.</returns>
    public static string EventText(MatchEvent e)
    {
        string fields = e switch
        {
            GoalScored g => Invariant($"{g.Side} {g.ScorerId} {V(g.Position)} {g.Speed:R}"),
            PuckDropped f => V(f.Dot),
            OffsideCalled o => Invariant($"{o.Attacker} {V(o.Position)}"),
            IcingCalled i => Invariant($"{i.Offender} {i.PlayerId} {V(i.Position)}"),
            PenaltyCalled p => Invariant($"{p.Side} {p.PlayerId} {V(p.Position)} {p.Seconds:R}"),
            FightStarted f => Invariant($"{f.Side} {f.PlayerId} {V(f.Position)}"),
            ShotSaved s => Invariant($"{s.Side} {s.GoalieId} {V(s.Position)} {s.Speed:R}"),
            LineChanged l => Invariant($"{l.Side} {l.PlayerId} {V(l.Position)}"),
            PeriodEnded p => Invariant($"{p.Period}"),
            MatchWon w => Invariant($"{w.Winner}"),
            MatchTied => "",
            ShotTaken s => Invariant($"{s.Side} {s.ShooterId} {V(s.Position)} {s.Speed:R}"),
            PassMade p => Invariant($"{p.Side} {p.PasserId} {V(p.Position)} {p.Speed:R}"),
            CheckLanded c => Invariant($"{c.Side} {c.CheckerId} {V(c.Position)} {c.Strength:R}"),
            PuckHitBoards b => Invariant($"{V(b.Position)} {b.Speed:R}"),
            PuckHitPost p => Invariant($"{V(p.Position)} {p.Speed:R}"),
            WhistleBlown w => V(w.Position),
            _ => throw new UnreachableException($"no replay text for {e.GetType().Name}"),
        };
        return Invariant($"{e.GetType().Name} {e.Time:R} {fields}").TrimEnd();
    }

    static string V(Vector3 v) => Invariant($"{v.X:R},{v.Y:R},{v.Z:R}");

    /// <summary>Whether the match ended on the step that just ran: the final horn, or the final phase.</summary>
    /// <param name="world">The world after the step.</param>
    /// <returns>True when it ended.</returns>
    public static bool Ends(World world) => world.Match.Phase == MatchPhase.Final || world.Events.Current.Any(e => e is MatchEnded);

    /// <summary>Writes the end line for a world.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The line, without the "x".</returns>
    public static string EndLine(World world)
    {
        var m = world.Match;
        return string.Create(CultureInfo.InvariantCulture, $"{world.Tick - 1} {m.Score[0]}-{m.Score[1]} {m.Shots[0]}-{m.Shots[1]} {m.Period} {m.Clock:R} {m.Phase}");
    }

    /// <summary>Reads a replay file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The replay.</returns>
    public static ReplayFile Read(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        if (!string.Equals(reader.ReadLine(), Magic, StringComparison.Ordinal)) throw new InvalidDataException($"{path} is not a Stick & Puck replay");
        var r = new ReplayFile();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length < 2) continue;
            string rest = line[2..];
            switch (line[0])
            {
                case 'h':
                    r.Header = JsonSerializer.Deserialize<ReplayHeader>(rest, Json) ?? throw new InvalidDataException("empty replay header");
                    break;
                case 'f':
                    r.Start = InputFrame.Read(rest);
                    break;
                case 'i':
                    var (tick, frame) = Split(rest);
                    r.Inputs[tick] = InputFrame.Read(frame);
                    r.LastTick = Math.Max(r.LastTick, tick);
                    break;
                case 'c':
                    var (at, sum) = Split(rest);
                    r.Checks[at] = ulong.Parse(sum, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    r.LastTick = Math.Max(r.LastTick, at);
                    break;
                case 'e':
                    r.Events.Add(rest);
                    break;
                case 'x':
                    r.End = rest;
                    r.LastTick = Math.Max(r.LastTick, Split(rest).Tick);
                    break;
                default:
                    throw new InvalidDataException($"replay line '{line}' is not a known record");
            }
        }
        return r;
    }

    /// <summary>Writes the replay, gzipped, through a temporary file so a crash never leaves half a replay.</summary>
    /// <param name="path">The file.</param>
    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        using (var file = File.Create(temp))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        using (var w = new StreamWriter(gzip))
        {
            w.WriteLine(Magic);
            w.WriteLine("h " + JsonSerializer.Serialize(Header, Json));
            w.WriteLine("f " + Start.Write());
            var c = CultureInfo.InvariantCulture;
            foreach (var (tick, frame) in Inputs) w.WriteLine(string.Create(c, $"i {tick} {frame.Write()}"));
            foreach (var (tick, sum) in Checks.OrderBy(k => k.Key)) w.WriteLine(string.Create(c, $"c {tick} {WorldChecksum.Text(sum)}"));
            foreach (string e in Events) w.WriteLine("e " + e);
            if (End.Length > 0) w.WriteLine("x " + End);
        }
        File.Move(temp, path, overwrite: true);
    }

    static (long Tick, string Text) Split(string text)
    {
        int space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? (long.Parse(text, CultureInfo.InvariantCulture), "")
            : (long.Parse(text[..space], CultureInfo.InvariantCulture), text[(space + 1)..]);
    }
}
