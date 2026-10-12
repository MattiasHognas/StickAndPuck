using Raylib_cs;
using StickAndPuck.Render;
using StickAndPuck.Screens;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Core;

/// <summary>Window, fixed-step loop and the order things are drawn in. Systems do the work; this file stays as it is.</summary>
public static class Game
{
    /// <summary>The native frame width in pixels.</summary>
    public const int Width = 480;

    /// <summary>The native frame height in pixels.</summary>
    public const int Height = 270;

    /// <summary>The fixed update step: 60 Hz.</summary>
    public const float Step = 1f / 60f;

    /// <summary>
    /// Runs the game. SAP_SCENE picks the scene; SAP_SCREENSHOT=1 saves screenshot.png after one
    /// second of game time and quits, for headless checks. SAP_RECORD saves every frame for a movie.
    /// </summary>
    public static void Run()
    {
        var clubs = Clubs.LoadAll();
        Console.WriteLine($"SAP: {clubs.Count} club(s): {string.Join(", ", clubs.Select(c => $"{c.Name} ({c.Roster.Count} players)"))}");
        var world = Scenes.CreateFromEnvironment(clubs, Settings.FromEnvironment());
        bool headless = string.Equals(Environment.GetEnvironmentVariable("SAP_SCREENSHOT"), "1", StringComparison.Ordinal);
        var recorder = FrameRecorder.FromEnvironment();

        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        InitWindow(Width * 3, Height * 3, "Stick & Puck");
        SetTargetFPS(60);

        var pixel = new PixelFrame(Width, Height);
        var render = new RenderContext { Toon = pixel.Toon, Mask = pixel.Mask };
        var systems = Restart(world, []);
        Console.WriteLine($"SAP: {systems.Count} systems: {string.Join(", ", systems.Select(s => s.GetType().Name))}");

        float pending = 0;
        while (!WindowShouldClose() && !ScreenFlow.QuitRequested)
        {
            // fixed 60 Hz steps; a slow frame runs a few steps, never more than a quarter second's worth
            pending += Math.Min(GetFrameTime(), 0.25f);
            if (headless || recorder != null) pending = Step;     // headless runs exactly one step per frame, so captures repeat
            while (pending >= Step)
            {
                world.Events.Clear();
                foreach (var s in systems)
                    if (!world.Match.Paused || s.RunsWhilePaused) s.Update(world, Step);
                world.Time += Step; world.Tick++;
                pending -= Step;
            }

            // W11: a screen started a match or went back to the menus; the new world gets fresh systems
            if (ScreenFlow.TakeNextWorld() is { } next) (world, systems) = (next, Restart(next, systems));

            Draw(world, systems, pixel, render, showFps: recorder == null && world.Settings.FpsLine);
            if (recorder?.Save(world) == true) break;

            // F12 saves a frame; SAP_SCREENSHOT=1 saves one after a second and quits (headless checks)
            if (IsKeyPressed(KeyboardKey.F12)) AssetRoot.SaveScreen("screenshot.png");
            if (headless && world.Time >= 1f)
            {
                AssetRoot.SaveScreen("screenshot.png");
                Console.WriteLine($"SAP: {world.Tick} steps of {systems.Count} systems, {world.Players.Count} players, screenshot.png saved");
                break;
            }
        }

        foreach (var s in systems.OfType<IDisposable>()) s.Dispose();
        pixel.Dispose();
        // GPU resources go before the window, or the context is already gone
        CloseWindow();
    }

    /// <summary>Disposes the old systems and starts a fresh set on the world.</summary>
    static IReadOnlyList<ISystem> Restart(World world, IReadOnlyList<ISystem> old)
    {
        foreach (var s in old.OfType<IDisposable>()) s.Dispose();
        var systems = Systems.Create();
        foreach (var s in systems) s.Start(world);
        return systems;
    }

    static void Draw(World world, IReadOnlyList<ISystem> systems, PixelFrame pixel, RenderContext render, bool showFps)
    {
        // 1. the scene, lit with three flat tones
        pixel.BeginScene();
        BeginMode3D(world.Camera);
        foreach (var s in systems) s.DrawScene(world, render);
        EndMode3D();
        PixelFrame.EndScene();

        // 2. a flat mask of the players, so the post pass can draw one outline around each
        pixel.BeginMask();
        BeginMode3D(world.Camera);
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
        if (showFps) DrawText($"{GetFPS()} fps  arrows: skate  space: shoot  tab: kit", 8, 8, 10, new Color(239, 224, 194, 255));
        EndDrawing();
    }
}
