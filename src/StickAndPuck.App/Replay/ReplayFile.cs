using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Replay
{
    /// <summary>A replay file. Gzipped text, one record per line: the magic "SAPREPLAY 1", "h" and the
    /// header as JSON, "f" and the device frame when the match started, then per step "i tick frame"
    /// whenever the devices changed, "c tick checksum" every <see cref="CheckEvery"/> steps, "e tick
    /// event" for every match event, and last "x tick score shots period clock" when the match ended.</summary>
    public sealed class ReplayFile
    {
        /// <summary>Steps between stored checksums.</summary>
        public const int CheckEvery = 30;

        private const string Magic = "SAPREPLAY 1";

        /// <summary>Gets or sets the header.</summary>
        public ReplayHeader Header { get; set; } = new();

        /// <summary>Gets or sets the devices when the match started, before its first step.</summary>
        public InputFrame Start { get; set; } = InputFrame.Empty;

        /// <summary>Gets the device frames by step, only where they changed.</summary>
        public IDictionary<long, InputFrame> Inputs { get; } =
            new SortedDictionary<long, InputFrame>();

        /// <summary>Gets the stored checksums by step.</summary>
        public IDictionary<long, ulong> Checks { get; } = new Dictionary<long, ulong>();

        /// <summary>Gets the event lines, "tick event".</summary>
        public IList<string> Events { get; } = [];

        /// <summary>Gets or sets the end line, "tick score shots period clock", or empty when the recording was cut short.</summary>
        public string End { get; set; } = "";

        /// <summary>Gets or sets the last step recorded.</summary>
        public long LastTick { get; set; } = -1;

        /// <summary>Writes an event as a replay line.</summary>
        public static string EventLine(long tick, MatchEvent e)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{tick} {EventText(e)}");
        }

        /// <summary>Writes an event as "Type time fields...", every float round-trip exact. Replay lines, the
        /// world checksum and the state dump all use it, so a new event type fails here until it has a line.</summary>
        public static string EventText(MatchEvent e)
        {
            string fields = e switch
            {
                GoalScored g => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{g.Side} {g.ScorerId} {V(g.Position)} {g.Speed:R}"
                ),
                PuckDropped f => V(f.Dot),
                OffsideCalled o => $"{o.Attacker} {V(o.Position)}",
                IcingCalled i => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{i.Offender} {i.PlayerId} {V(i.Position)}"
                ),
                PenaltyCalled p => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{p.Side} {p.PlayerId} {V(p.Position)} {p.Seconds:R}"
                ),
                FightStarted f => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{f.Side} {f.PlayerId} {V(f.Position)}"
                ),
                ShotSaved s => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{s.Side} {s.GoalieId} {V(s.Position)} {s.Speed:R}"
                ),
                LineChanged l => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{l.Side} {l.PlayerId} {V(l.Position)}"
                ),
                PeriodEnded p => string.Create(CultureInfo.InvariantCulture, $"{p.Period}"),
                MatchWon w => $"{w.Winner}",
                MatchTied => "",
                ShotTaken s => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{s.Side} {s.ShooterId} {V(s.Position)} {s.Speed:R}"
                ),
                PassMade p => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{p.Side} {p.PasserId} {V(p.Position)} {p.Speed:R}"
                ),
                CheckLanded c => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.Side} {c.CheckerId} {V(c.Position)} {c.Strength:R}"
                ),
                PuckHitBoards b => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{V(b.Position)} {b.Speed:R}"
                ),
                PuckHitPost p => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{V(p.Position)} {p.Speed:R}"
                ),
                WhistleBlown w => V(w.Position),
                _ => throw new UnreachableException($"no replay text for {e.GetType().Name}"),
            };
            return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{e.GetType().Name} {e.Time:R} {fields}"
                )
                .TrimEnd();
        }

        private static string V(Vector3 v)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{v.X:R},{v.Y:R},{v.Z:R}");
        }

        /// <summary>Whether the match ended on the step that just ran: the final horn, or the final phase.</summary>
        public static bool Ends(World world)
        {
            return world.Match.Phase == MatchPhase.Final
                || world.Events.Current.Any(e => e is MatchEnded);
        }

        /// <summary>Writes the end line for a world.</summary>
        public static string EndLine(World world)
        {
            MatchState m = world.Match;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{world.Tick - 1} {m.Score[0]}-{m.Score[1]} {m.Shots[0]}-{m.Shots[1]} {m.Period} {m.Clock:R} {m.Phase}"
            );
        }

        /// <summary>Reads a replay file.</summary>
        public static ReplayFile Read(string path)
        {
            using FileStream file = File.OpenRead(path);
            using GZipStream gzip = new(file, CompressionMode.Decompress);
            using StreamReader reader = new(gzip);
            if (!string.Equals(reader.ReadLine(), Magic, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path} is not a Stick & Puck replay");
            }

            ReplayFile r = new();
            while (reader.ReadLine() is { } line)
            {
                if (line.Length < 2)
                {
                    continue;
                }

                string rest = line[2..];
                switch (line[0])
                {
                    case 'h':
                        r.Header =
                            JsonSerializer.Deserialize(rest, ReplayJsonContext.Default.ReplayHeader)
                            ?? throw new InvalidDataException("empty replay header");
                        break;
                    case 'f':
                        r.Start = InputFrame.Read(rest);
                        break;
                    case 'i':
                        (long tick, string frame) = Split(rest);
                        r.Inputs[tick] = InputFrame.Read(frame);
                        r.LastTick = Math.Max(r.LastTick, tick);
                        break;
                    case 'c':
                        (long at, string sum) = Split(rest);
                        r.Checks[at] = ulong.Parse(
                            sum,
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture
                        );
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
                        throw new InvalidDataException(
                            $"replay line '{line}' is not a known record"
                        );
                }
            }
            return r;
        }

        /// <summary>Writes the replay, gzipped, through a temporary file so a crash never leaves half a replay.</summary>
        public void Write(string path)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            using (FileStream file = File.Create(temp))
            using (GZipStream gzip = new(file, CompressionLevel.Optimal))
            using (StreamWriter w = new(gzip))
            {
                w.WriteLine(Magic);
                w.WriteLine(
                    "h " + JsonSerializer.Serialize(Header, ReplayJsonContext.Default.ReplayHeader)
                );
                w.WriteLine("f " + Start.Write());
                CultureInfo c = CultureInfo.InvariantCulture;
                foreach ((long tick, InputFrame frame) in Inputs)
                {
                    w.WriteLine(string.Create(c, $"i {tick} {frame.Write()}"));
                }

                foreach ((long tick, ulong sum) in Checks.OrderBy(k => k.Key))
                {
                    w.WriteLine(string.Create(c, $"c {tick} {WorldChecksum.Text(sum)}"));
                }

                foreach (string e in Events)
                {
                    w.WriteLine("e " + e);
                }

                if (End.Length > 0)
                {
                    w.WriteLine("x " + End);
                }
            }
            File.Move(temp, path, overwrite: true);
        }

        private static (long Tick, string Text) Split(string text)
        {
            int space = text.IndexOf(' ', StringComparison.Ordinal);
            return space < 0
                ? (long.Parse(text, CultureInfo.InvariantCulture), "")
                : (long.Parse(text[..space], CultureInfo.InvariantCulture), text[(space + 1)..]);
        }
    }
}
