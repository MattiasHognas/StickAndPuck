using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Render;
using StickAndPuck.App.Replays;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

/// <summary>Window, fixed-step loop and the order things are drawn in. Systems do the work. The state that
/// outlives a match (<see cref="AppState"/>) is made here, once per run, and handed to every world.</summary>
public static class Game
{
    /// <summary>The native frame width in pixels.</summary>
    public const int Width = 480;

    /// <summary>The native frame height in pixels.</summary>
    public const int Height = 270;

    /// <summary>The fixed update step: 60 Hz.</summary>
    public const float Step = 1f / 60f;

    /// <summary>Runs the game. SAP_SCENE picks the scene; SAP_SCREENSHOT=1 saves screenshot.png after one
    /// second of game time and quits, for headless checks. SAP_RECORD saves every frame for a movie.
    /// SAP_REPLAY plays a replay file (see <see cref="ReplayPlayer"/>); every match the
    /// screens start is recorded (see <see cref="ReplayRecorder"/>). SAP_STEPS=n runs n steps as
    /// fast as it can without waiting for the screen, then quits.</summary>
    public static void Run()
    {
        using var session = new Session();
#pragma warning disable S108 // Nested blocks of code should not be left empty
        while (session.Frame()) { }
#pragma warning restore S108 // Nested blocks of code should not be left empty
    }

    /// <summary>A running game, driven by the desktop loop or one browser animation frame at a time.</summary>
    public sealed class Session : IDisposable
    {
        readonly AppState app;
        readonly Maybe<ReplayPlayer> replay;
        World world;
        readonly Loop loop;
        readonly PixelFrame pixel;
        readonly RenderContext render;
        IReadOnlyList<ISystem> systems;
        bool disposed;

        /// <summary>Loads the world and its render resources.</summary>
        public Session()
        {
            var clubs = Clubs.LoadAll();
            Text.Use(Settings.FromEnvironment().Language);
            Console.WriteLine($"SAP: {clubs.Count} club(s): {string.Join(", ", clubs.Select(c => $"{c.Name} ({c.Roster.Count} players)"))}");
            app = new AppState();
            replay = ReplayPlayer.FromEnvironment(clubs, app);
            world = replay.TryGet(out var played) ? played.World : Scenes.CreateFromEnvironment(clubs, Settings.FromEnvironment(), app);
            Maybe<ReplayRecorder> recorder;
            if (replay.TryGet(out var r))
            {
                recorder = r.TakeOverAtTarget ? r.TakeOverRecorder() : Maybe<ReplayRecorder>.None;
            }
            else
            {
                recorder = ReplayRecorder.Begin(world, RecordedScene());
            }

            loop = new Loop
            {
                Headless = Env.Is("SAP_SCREENSHOT", "1"),
                Frames = FrameRecorder.FromEnvironment(),
                Replay = replay,
                Limit = long.TryParse(Env.Get("SAP_STEPS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) && n > 0 ? n : 0,
                Recorder = recorder,
                Sums = ChecksumLog.FromEnvironment(),
            };

            SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
            InitWindow(Width * 3, Height * 3, "Stick & Puck");
            if (OperatingSystem.IsBrowser() && !IsWindowReady())
                throw new InvalidOperationException("WebGL could not initialize. Check your browser's graphics settings and reload.");
            SetTargetFPS(OperatingSystem.IsBrowser() ? 0 : 60);

            pixel = new PixelFrame(Width, Height, app.Scale, app.Dither);
            render = new RenderContext { Toon = pixel.Toon, Mask = pixel.Mask, Unlit = pixel.Unlit };
            systems = Restart(world, []);
            Console.WriteLine($"SAP: {systems.Count} systems: {string.Join(", ", systems.Select(s => s.GetType().Name))}");
        }

        /// <summary>Updates and draws one frame; false when the game should close.</summary>
        public bool Frame() => Frame(GetFrameTime());

        /// <summary>Updates and draws using elapsed host time; simulation still advances in fixed steps.</summary>
        public bool Frame(float frameSeconds)
        {
            if (disposed || (!OperatingSystem.IsBrowser() && WindowShouldClose()) || app.Screens.QuitRequested) return false;
            loop.Frame(world, systems, frameSeconds, render.Motion);

            // A screen started a match or went back to the menus; the new world gets fresh systems
            if (app.Screens.TakeNextWorld().TryGet(out var next))
            {
                if (replay.Is(p => !p.Live)) return false;    // the replayed match is over
                if (loop.Recorder.TryGet(out var recorder)) recorder.Finish(world);
                loop.Recorder = ReplayRecorder.Begin(next, "");
                next.Switches = world.Switches.Copy();    // the scene's switches stay on, as the statics they were did
                (world, systems) = (next, Restart(next, systems));
                render.Motion.Reset();
            }

            Draw(world, systems, pixel, render, showFps: !loop.Frames.HasValue && world.Settings.FpsLine);
            return !loop.AfterDraw(world, systems.Count);
        }

        /// <summary>Current simulation state for browser replay checks.</summary>
        public string State => string.Create(CultureInfo.InvariantCulture, $"{world.Tick}:{WorldChecksum.Of(world):x16}\n{StateDump.Of(world)}\n{StateDump.Exact(world)}");

        /// <summary>Reports replay verification after its final step.</summary>
        public string ReplayResult
        {
            get
            {
                if (!replay.TryGet(out var played)) return "";
                if (!played.Ended) return "Replay stopped before its final step.";
                return loop.ExitCode == 0
                    ? "Replay verified: events, result and checksums match."
                    : "Replay differs from the recording.";
            }
        }

        /// <summary>Writes the current recording so the browser can download it.</summary>
        public Maybe<string> ReplaySnapshot() => loop.Recorder.Select(recorder => recorder.Snapshot(world));

        /// <summary>Finishes recordings and releases graphics before closing the window.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            loop.ReportOnce(world);    // a recording that went on to the menus or a quit ends the loop before its last step
            if (loop.Recorder.TryGet(out var last)) last.Finish(world);
            loop.Sums.Dispose();
            foreach (var s in systems.OfType<IDisposable>()) s.Dispose();
            pixel.Dispose();
            // GPU resources go before the window, or the context is already gone
            CloseWindow();
            Environment.ExitCode = loop.ExitCode;
        }

        /// <summary>The SAP_SCENE to note in a scene's replay, or "" for the screens, which record their own matches.</summary>
        static string RecordedScene()
        {
            string scene = Env.Get("SAP_SCENE") is { Length: > 0 } s ? s : Scenes.Default;
            return string.Equals(scene, Scenes.Default, StringComparison.OrdinalIgnoreCase)
                ? ""
                : scene;
        }

        /// <summary>Disposes the old systems and starts a fresh set on the world.</summary>
        static IReadOnlyList<ISystem> Restart(World world, IReadOnlyList<ISystem> old)
        {
            foreach (var s in old.OfType<IDisposable>())
                s.Dispose();
            var freshSystems = Systems.Create(world.Seed);
            foreach (var s in freshSystems)
                s.Start(world);
            return freshSystems;
        }

        static void Draw(World world, IReadOnlyList<ISystem> systems, PixelFrame pixel, RenderContext render, bool showFps)
        {
            // 1. the scene, lit with three flat tones
            pixel.BeginScene();
            if (OperatingSystem.IsBrowser()) BeginShaderMode(render.Unlit);
            BeginMode3D(render.Motion.Camera(world));
            foreach (var s in systems) s.DrawScene(world, render);
            EndMode3D();
            if (OperatingSystem.IsBrowser()) EndShaderMode();
            PixelFrame.EndScene();

            // 2. a flat mask of the players, so the post pass can draw one outline around each
            pixel.BeginMask();
            BeginMode3D(render.Motion.Camera(world));
            foreach (var s in systems) s.DrawMask(world, render);
            EndMode3D();
            PixelFrame.EndMask();

            // 3. HUD at the native size, never snapped or outlined
            pixel.BeginHud();
            foreach (var s in systems) s.DrawHud(world, render);
            PixelFrame.EndHud();

            // 4. palette snap and outline, HUD on top, then whole-pixel upscale into the window
            BeginDrawing();
            ClearBackground(new Color(24, 19, 31, 255));
            pixel.Present();
            if (showFps) DrawText(string.Create(CultureInfo.InvariantCulture, $"{GetFPS()} fps  arrows: skate  space: shoot  tab: kit"), 8, 8, 10, new Color(239, 224, 194, 255));
            EndDrawing();
        }
    }

    /// <summary>Runs one fixed step of every system on the world.</summary>
    internal static void StepWorld(World world, IReadOnlyList<ISystem> systems)
    {
        DeviceInput.Step(world.Tick);
        world.Events.Clear();
        foreach (var s in systems)
            if (!world.Match.Paused || s.RunsWhilePaused) s.Update(world, Step);
        world.Time += Step;
        world.Tick++;
    }

    /// <summary>The per-frame part of the loop: steps, replay checks, screenshots.</summary>
    sealed class Loop
    {
        readonly SimClock clock = new();
        bool scrubShot, reported;

        public bool Headless { get; init; }

        public Maybe<FrameRecorder> Frames { get; init; }

        public Maybe<ReplayPlayer> Replay { get; init; }

        /// <summary>Gets the steps SAP_STEPS asks for: run that many as fast as possible, then quit (0: no limit).</summary>
        public long Limit { get; init; }

        public Maybe<ReplayRecorder> Recorder { get; set; }

        /// <summary>Gets the SAP_CHECKSUMS log of every step's checksum.</summary>
        public required ChecksumLog Sums { get; init; }

        public int ExitCode { get; private set; }

        /// <summary>Runs this frame's steps.</summary>
        public void Frame(World world, IReadOnlyList<ISystem> systems, float frameSeconds, RenderInterpolation motion)
        {
            DeviceInput.Poll();
            bool frozen = Replay.Is(r => r is { Ended: true, Live: false });    // a replay that played out holds its last frame
            bool fast = Replay.Is(r => r.FastForward(Headless)) || (Limit > 0 && !Replay.HasValue);
            int steps = frozen ? 0 : clock.Steps(frameSeconds, lockstep: Headless || Frames.HasValue, fast);
            if (Limit > 0) steps = (int)Math.Min(steps, Limit - world.Tick);
            for (int i = 0; i < steps; i++)
            {
                motion.Capture(world);
                StepWorld(world, systems);
                ulong sum = WorldChecksum.Of(world);
                if (Recorder.HasValue || Replay.HasValue) Sums.Write(world.Tick - 1, sum);    // the recorded or replayed match only
                if (Recorder.TryGet(out var recorder)) recorder.AfterStep(world, sum);
                var step = Replay.TryGet(out var replay) ? replay.AfterStep(world, sum) : ReplayStep.Next;
                if (step == ReplayStep.Target)
                {
                    scrubShot = true;
                    if (Replay.TryGet(out var scrubbed) && scrubbed.TakeOverAtTarget) scrubbed.TakeOver();
                    break;
                }
                if (step == ReplayStep.End) break;
            }
            motion.Alpha = frozen || fast || scrubShot || Replay.Is(r => r.Ended) ? 1 : clock.Alpha;
        }

        /// <summary>Prints the replay's report the first time it is asked for.</summary>
        public bool ReportOnce(World world)
        {
            if (!Replay.TryGet(out var replay) || reported) return false;
            reported = true;
            ExitCode = replay.Report(world);
            return true;
        }

        /// <summary>Saves what this frame should save; true when the game should quit.</summary>
        public bool AfterDraw(World world, int systemCount)
        {
            if (scrubShot)
            {
                scrubShot = false;
                AssetRoot.SaveScreen("replay_at.png");
                Console.WriteLine($"SAP: replay: replay_at.png saved");
                if (Headless && Replay.Is(r => !r.Live)) return true;
            }
            if (Replay.Is(r => r.Ended) && ReportOnce(world) && Headless) return true;
            if (Frames.Is(f => f.Save(world))) return true;
            if (Limit > 0 && world.Tick >= Limit)
            {
                Console.WriteLine($"SAP: {world.Tick} steps run (SAP_STEPS); {StateDump.Of(world).Split('\n')[1]}");
                return true;
            }

            // F12 saves a frame; SAP_SCREENSHOT=1 saves one after a second and quits (headless checks)
            if (IsKeyPressed(KeyboardKey.F12)) AssetRoot.SaveScreen("screenshot.png");
            if (Headless && !Replay.HasValue && Limit == 0 && world.Time >= 1f)
            {
                AssetRoot.SaveScreen("screenshot.png");
                Console.WriteLine($"SAP: {world.Tick} steps of {systemCount} systems, {world.Players.Count} players, screenshot.png saved");
                return true;
            }
            return false;
        }
    }

}
