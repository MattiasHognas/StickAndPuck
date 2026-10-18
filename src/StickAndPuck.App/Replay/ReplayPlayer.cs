using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Replay
{
    /// <summary>Plays a replay file back. SAP_REPLAY=file builds the match again from the file's header
    /// (seed, clubs, rules, settings and the SAP_* variables it ran with) and feeds the game the
    /// recorded device frames step by step, checking the world's checksum against the file as it goes.
    /// SAP_AT=period:mm:ss (the clock as the scorebug shows it, or period:seconds, or tN for a step)
    /// runs to that moment without drawing, prints the state and saves replay_at.png; SAP_TAKEOVER=1
    /// then hands the match to the devices from there and records a new replay. Headless
    /// (SAP_SCREENSHOT=1) without SAP_AT it runs the whole replay as fast as it can and exits 0 when
    /// the score, events and checksums match the recording, 1 when they do not.</summary>
    public sealed class ReplayPlayer
    {
        private readonly ReplayFile file;
        private readonly Maybe<ScrubTarget> target;
        private readonly string path;
        private readonly List<string> events = [];
        private long firstMismatch = -1;
        private int mismatches;
        private string playedEnd = "";

        private ReplayPlayer(
            ReplayFile file,
            string path,
            World world,
            Maybe<ScrubTarget> target,
            bool takeOver
        )
        {
            this.file = file;
            this.path = path;
            World = world;
            this.target = target;
            TakeOverAtTarget = takeOver;
        }

        /// <summary>Gets the world the replay rebuilt.</summary>
        public World World { get; }

        /// <summary>Gets a value indicating whether the SAP_AT moment has been reached.</summary>
        public bool Reached { get; private set; }

        /// <summary>Gets a value indicating whether the recorded steps have all played.</summary>
        public bool Ended { get; private set; }

        /// <summary>Gets a value indicating whether the devices play from the SAP_AT moment on (SAP_TAKEOVER=1).</summary>
        public bool TakeOverAtTarget { get; }

        /// <summary>Gets a value indicating whether the devices have taken over.</summary>
        public bool Live { get; private set; }

        /// <summary>Gets a value indicating whether a SAP_AT moment was asked for.</summary>
        public bool HasTarget => target.HasValue;

        /// <summary>Gets the file's header.</summary>
        public ReplayHeader Header => file.Header;

        /// <summary>Gets the devices before the first step.</summary>
        public InputFrame Start => file.Start;

        /// <summary>Reads SAP_REPLAY and builds its match on the game's state, or returns None when it is not set.</summary>
        public static Maybe<ReplayPlayer> FromEnvironment(
            IReadOnlyList<ClubData> clubs,
            AppState app
        )
        {
            if (Env.Get("SAP_REPLAY") is not { Length: > 0 } name)
            {
                return Maybe.None<ReplayPlayer>();
            }

            string filePath = AssetRoot.Output(name);
            ReplayFile replayFile = ReplayFile.Read(filePath);
            ReplayHeader h = replayFile.Header;
            ValidateHeader(filePath, h);

            Maybe<ScrubTarget> scrubTarget = ScrubTarget.Parse(Env.Get("SAP_AT"));
            bool takeOver = Env.Is("SAP_TAKEOVER", "1");
            if (takeOver && !scrubTarget.HasValue)
            {
                throw new InvalidOperationException(
                    "SAP_TAKEOVER=1 needs SAP_AT, the moment to take over from"
                );
            }

            SetVariables(h.Env);
            LogReplayHeader(filePath, h, replayFile.LastTick);
            World world = BuildWorld(clubs, app, h, replayFile);
            CheckClubHash(world, h);
            return new ReplayPlayer(replayFile, filePath, world, scrubTarget, takeOver);
        }

        private static void ValidateHeader(string filePath, ReplayHeader h)
        {
            if (h.Version != ReplayHeader.CurrentVersion)
            {
                throw new InvalidDataException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{filePath} is replay version {h.Version}; this game reads version {ReplayHeader.CurrentVersion}"
                    )
                );
            }
        }

        private static void LogReplayHeader(string filePath, ReplayHeader h, long lastTick)
        {
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: replay: {filePath}: {h.Kind} {(string.Equals(h.Kind, "scene", StringComparison.Ordinal) ? h.Scene : $"{h.Setup.Select(s => s.Home).Or("")} vs {h.Setup.Select(s => s.Away).Or("")}")}, seed {h.Seed}, {lastTick + 1} steps, recorded on {h.Recorded}"
                )
            );
        }

        private static World BuildWorld(
            IReadOnlyList<ClubData> clubs,
            AppState app,
            ReplayHeader h,
            ReplayFile replayFile
        )
        {
            // the frames must be in place before the world is built: the screens read the devices as they start
            DeviceInput.Set(replayFile.Start);
            DeviceInput.Play(tick =>
                replayFile.Inputs.TryGetValue(tick, out InputFrame f) ? f : Maybe.None<InputFrame>()
            );

            World world;
            if (string.Equals(h.Kind, "match", StringComparison.Ordinal))
            {
                MatchSetup setup = (
                    h.Setup.TryGet(out ReplaySetup stored)
                        ? stored
                        : throw new InvalidDataException("a match replay without its setup")
                ).ToSetup(clubs);
                world = app.Screens.BeginReplay(
                    clubs,
                    ScreenPrefsFile.FromJson(h.Prefs),
                    setup,
                    h.Seed,
                    h.Mode
                );
            }
            else
            {
                Env.Set("SAP_SCENE", h.Scene);
                world = Scenes.CreateFromEnvironment(clubs, Settings.FromEnvironment(), app);
                world.Seed = h.Seed;
            }

            return world;
        }

        private static void CheckClubHash(World world, ReplayHeader h)
        {
            if (
                !string.Equals(
                    ReplayRecorder.ClubsHash(world.Setup.Home, world.Setup.Away),
                    h.Clubs,
                    StringComparison.Ordinal
                )
            )
            {
                Console.WriteLine(
                    "SAP: replay: WARNING: a club file changed since this was recorded, so the match may play differently"
                );
            }
        }

        /// <summary>Gets a value indicating whether to run steps as fast as possible without drawing them.</summary>
        /// <returns>True while running to the SAP_AT moment, or to the end of a headless check.</returns>
        public bool FastForward(bool headless)
        {
            return !Live
                && !Ended
                && ((target.HasValue && !Reached) || (headless && !target.HasValue));
        }

        /// <summary>Checks the step that just ran against the recording.</summary>
        public ReplayStep AfterStep(World world, ulong sum)
        {
            long tick = world.Tick - 1;
            if (Live)
            {
                return ReplayStep.Next;
            }

            foreach (MatchEvent e in world.Events.Current)
            {
                events.Add(ReplayFile.EventLine(tick, e));
            }

            if (playedEnd.Length == 0 && ReplayFile.Ends(world))
            {
                playedEnd = ReplayFile.EndLine(world);
            }

            if (file.Checks.TryGetValue(tick, out ulong want) && want != sum)
            {
                if (firstMismatch < 0)
                {
                    firstMismatch = tick;
                    Console.WriteLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"SAP: replay: checksum differs at step {tick} ({WorldChecksum.Text(sum)}, recorded {WorldChecksum.Text(want)}), period {world.Match.Period} {MatchFlow.ClockText(world.Match.Clock)}"
                        )
                    );
                }
                mismatches++;
            }
            if (target.TryGet(out ScrubTarget at) && !Reached && at.Hit(world))
            {
                Reached = true;
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: replay: at {at} (step {tick})"
                    )
                );
                Console.WriteLine(StateDump.Of(world));
                Console.WriteLine(StateDump.Exact(world));
                return ReplayStep.Target;
            }
            if (tick >= file.LastTick)
            {
                Ended = true;
                return ReplayStep.End;
            }
            return ReplayStep.Next;
        }

        /// <summary>Hands the match to the devices from this step on.</summary>
        public void TakeOver()
        {
            Live = true;
            DeviceInput.StopPlaying();
            Console.WriteLine(
                "SAP: replay: taken over; the devices play from here and a new replay records"
            );
        }

        /// <summary>Starts recording the take-over: the replay's own steps so far, then the devices'.</summary>
        public ReplayRecorder TakeOverRecorder()
        {
            string name = Path.GetFileNameWithoutExtension(path) + "_takeover.sapreplay";
            string to =
                Env.Get("SAP_REPLAY_OUT") is { Length: > 0 } o
                && !string.Equals(o, "0", StringComparison.Ordinal)
                    ? AssetRoot.Output(o)
                    : Path.Combine(Path.GetDirectoryName(path) ?? ".", name);
            return ReplayRecorder.Begin(file.Header, file.Start, to);
        }

        /// <summary>Prints how the replay compares with the recording.</summary>
        public int Report(World world)
        {
            string end = playedEnd.Length > 0 ? playedEnd : ReplayFile.EndLine(world);
            string wantEnd = file.End;
            bool endOk = SameEnd(end, wantEnd);
            int sameEvents = events
                .Zip(file.Events)
                .TakeWhile(p => string.Equals(p.First, p.Second, StringComparison.Ordinal))
                .Count();
            bool eventsOk = sameEvents == events.Count && events.Count == file.Events.Count;
            string firstEventDifference = sameEvents < events.Count ? events[sameEvents] : "none";
            string firstRecordedEventDifference =
                sameEvents < file.Events.Count ? file.Events[sameEvents] : "none";
            MatchState m = world.Match;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: replay: played {world.Tick} steps: {world.Setup.Home.Id} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.Id}, shots {m.Shots[0]}-{m.Shots[1]}, {events.Count} events"
                )
            );
            Console.WriteLine(
                $"{(endOk ? "PASS" : "FAIL")}  the replay ends as recorded ({end}; recorded {wantEnd})"
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{(eventsOk ? "PASS" : "FAIL")}  every event happens as recorded ({sameEvents} of {file.Events.Count} the same{(eventsOk ? "" : $", first difference: {firstEventDifference} against {firstRecordedEventDifference}")})"
                )
            );
            Console.WriteLine(
                $"{(mismatches == 0 ? "PASS" : "FAIL")}  the world's checksum matches at all {file.Checks.Count} recorded steps{(mismatches == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $" ({mismatches} differ, first at step {firstMismatch})"))}"
            );
            bool ok = endOk && eventsOk && mismatches == 0;
            Console.WriteLine(
                ok ? "REPLAY: matches the recording" : "REPLAY: differs from the recording"
            );
            return ok ? 0 : 1;
        }

        /// <summary>The end lines match on the step, score, shots, period and phase; a recording cut short ends wherever it stopped.</summary>
        private static bool SameEnd(string a, string b)
        {
            string[] x = a.Split(' '),
                y = b.Split(' ');
            return x.Length == y.Length
                && x.Zip(y).All(p => string.Equals(p.First, p.Second, StringComparison.Ordinal));
        }

        /// <summary>Sets the SAP_* variables the match ran with, and clears every other one that could change the match.</summary>
        private static void SetVariables(IReadOnlyDictionary<string, string> env)
        {
            foreach ((string k, string _) in Env.Sap())
            {
                if (
                    !env.ContainsKey(k)
                    && !ReplayRecorder.ControlVariables.Contains(k, StringComparer.Ordinal)
                )
                {
                    Env.Set(k, ""); // an empty value removes the variable
                }
            }
            foreach ((string k, string v) in env)
            {
                Env.Set(k, v);
            }

            Env.Set("SAP_SCREEN_KEYS", ""); // the scripted presses are in the frames
        }
    }
}
