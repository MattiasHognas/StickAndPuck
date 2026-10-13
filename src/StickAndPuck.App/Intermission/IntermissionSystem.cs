using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Intermission;

/// <summary>
/// W31: between periods the Zamboni comes out through the gate in the far boards, laps the ice
/// spiralling in toward the centre and drives back out, and the next period's faceoff follows.
/// While it drives, the AI skaters wait at their bench doors, the camera pans after the machine and
/// enter, space or the A button skips it. W7a's intermission pause is held open until the Zamboni is
/// gone, the same way the goal replay holds the restart. Runs after the camera system, so its
/// camera wins while the Zamboni is out and hands back smoothly before the faceoff.
/// </summary>
internal sealed class IntermissionSystem : ISystem, IDisposable
{
    /// <summary>Seconds of the intermission pause kept in hand while the Zamboni drives.</summary>
    const float HoldSeconds = 0.5f;

    /// <summary>The pause between the Zamboni leaving (or the skip) and the faceoff set-up.</summary>
    const float AfterSeconds = 1.2f;

    /// <summary>Skips are ignored for this long, so the button that ended the period does not skip it too.</summary>
    const float SkipAfterSeconds = 0.5f;

    /// <summary>How long the camera takes to swing over to the Zamboni and back.</summary>
    const float BlendSeconds = 1.2f;

    /// <summary>How far apart the waiting skaters stand along the boards.</summary>
    const float WaitSpacing = 1.1f;

    static readonly Color TagText = new(0xef, 0xe0, 0xc2, 255);
    static readonly Color TagBack = new(0x18, 0x13, 0x1f, 255);

    readonly Func<bool> skipPressed;
    readonly ZamboniRenderer renderer = new();
    float cameraX, cameraVx, blend;

    /// <summary>Initializes a new instance of the <see cref="IntermissionSystem"/> class that reads the skip from the keyboard and gamepads.</summary>
    public IntermissionSystem()
        : this(SkipPressed)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IntermissionSystem"/> class with its own skip button, for the self test.</summary>
    /// <param name="skipPressed">Whether the skip was pressed this step.</param>
    public IntermissionSystem(Func<bool> skipPressed)
    {
        this.skipPressed = skipPressed;
    }

    /// <summary>Gets the route every Zamboni drive follows.</summary>
    public static ZamboniRoute Route { get; } = new();

    /// <summary>
    /// Gets or sets the seconds into its drive the next Zamboni starts at. SAP_ZAMBONI_AT sets it for
    /// headless screenshots of a chosen moment; the scene sets it back to 0 once used.
    /// </summary>
    public static float StartAt { get; set; }

    /// <summary>Gets the Zamboni's drive in this world, while one is under way.</summary>
    public Drive? Current { get; private set; }

    public void Start(World world)
    {
        if (MatchFlow.IsRunning(world)) renderer.Load();
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        if (world.Match.Phase != MatchPhase.Intermission)
        {
            Current = null;
            blend = 0;
            return;
        }

        var flow = MatchFlow.State(world);
        if (Current == null)
        {
            if (flow.Timer <= 0) return;   // the next period is starting this step
            Current = new Drive { Seconds = StartAt, Period = world.Match.Period };
            StartAt = 0;
            cameraX = Route.At(Route.Distance(Current.Seconds)).X;
            cameraVx = 0;
        }

        var drive = Current;
        if (!drive.Finished)
        {
            drive.Seconds += dt;
            drive.Shown += dt;
            if (drive.Shown >= SkipAfterSeconds && skipPressed())
            {
                drive.Skipped = true;
                drive.Finished = true;
            }
            else if (drive.Seconds >= Route.Duration)
            {
                drive.Finished = true;
            }

            if (drive.Finished) flow.Timer = AfterSeconds;
            else flow.Timer = Math.Max(flow.Timer, HoldSeconds);
        }

        if (!drive.Finished) Wait(world);
        Follow(world, drive, dt);
    }

    /// <summary>Keeps the AI skaters lined up along the boards at their bench doors, out of the Zamboni's way.</summary>
    static void Wait(World world)
    {
        foreach (var (p, spot) in WaitSpots(world)) Positioning.Request(world, p, spot);
    }

    /// <summary>Gets where each AI skater on the ice waits: spread along the boards either side of the bench door.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Each skater with its spot (x, z).</returns>
    internal static IEnumerable<(Player Player, Vector2 Spot)> WaitSpots(World world)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var door = Squad.Door(side);
            var skaters = world.Players.Where(p => p.Side == side && p.OnIce && !p.IsGoalie && p.Slot < 0).ToList();
            for (int i = 0; i < skaters.Count; i++)
                yield return (skaters[i], new Vector2(door.X + (i - (skaters.Count - 1) / 2f) * WaitSpacing, door.Y));
        }
    }

    /// <summary>Pans the gameplay camera after the Zamboni, swinging over from the follow camera and back.</summary>
    void Follow(World world, Drive drive, float dt)
    {
        if (Replay.Playing) return;
        var at = Route.At(Route.Distance(drive.Seconds));
        float wanted = drive.Finished || !Route.OnIce(Route.Distance(drive.Seconds)) && drive.Seconds > Route.Duration / 2 ? 0 : 1;
        blend = Math.Clamp(blend + Math.Sign(wanted - blend) * dt / BlendSeconds, 0, 1);
        if (wanted == 0 && blend == 0) return;

        Spring(ref cameraX, ref cameraVx, Math.Clamp(at.X, -MaxX(), MaxX()), dt);
        var own = BroadcastCamera.Straight(cameraX, BroadcastCamera.Back, BroadcastCamera.FovY);
        var follow = world.Camera;
        float w = blend * blend * (3 - 2 * blend);
        float x = Snap(follow.Position.X + (own.Position.X - follow.Position.X) * w);
        world.Camera = BroadcastCamera.Straight(x, follow.Position.Z, follow.FovY);
    }

    public void DrawScene(World world, RenderContext render)
    {
        if (Showing() is { } d) renderer.DrawScene(Route.At(d), Route.HeadingAt(d), render);
    }

    public void DrawMask(World world, RenderContext render)
    {
        if (Showing() is { } d) renderer.DrawMask(Route.At(d), Route.HeadingAt(d), render);
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (Current is not { Finished: false } drive || drive.Shown < SkipAfterSeconds) return;
        string text = Text.Get(K.Skip);
        int w = MeasureText(text, 10) + 10;
        const int h = 13, y = Game.Height - h - 6;
        int x = Game.Width - w - 6;
        DrawRectangle(x, y, w, h, TagBack);
        DrawText(text, x + 5, y + 2, 10, TagText);
    }

    public void Dispose()
    {
        renderer.Dispose();
    }

    /// <summary>Gets the distance along the route to draw at, or null while the Zamboni is out of sight behind the boards or gone.</summary>
    float? Showing()
    {
        if (Current is not { Skipped: false } drive) return null;
        float d = Route.Distance(drive.Seconds);
        return d > 0 && d < Route.Length ? d : null;
    }

    static bool SkipPressed()
    {
        if (!IsWindowReady()) return false;
        if (DeviceInput.KeyPressed(KeyboardKey.Enter) || DeviceInput.KeyPressed(KeyboardKey.KpEnter) || DeviceInput.KeyPressed(KeyboardKey.Space)) return true;
        for (int pad = 0; pad < 2; pad++)
            if (DeviceInput.PadAvailable(pad) && DeviceInput.PadPressed(pad, GamepadButton.RightFaceDown)) return true;
        return false;
    }

    /// <summary>The farthest the camera pans, as the follow camera keeps it: the end boards at the frame edge at centre ice, plus a margin.</summary>
    static float MaxX()
    {
        var forward = BroadcastCamera.Forward;
        float depth = Vector3.Dot(new Vector3(0, -BroadcastCamera.Height, -BroadcastCamera.Back), forward);
        float half = depth * HalfTan();
        return Math.Max(0, RinkSpec.HalfLength + 1.5f - half);
    }

    /// <summary>Rounds a camera x to whole pixels at the far boards, like the follow camera, so the ad boards never shimmer.</summary>
    static float Snap(float x)
    {
        var forward = BroadcastCamera.Forward;
        var board = new Vector3(0, RinkSpec.BoardHeight / 2, -RinkSpec.HalfWidth);
        float depth = Vector3.Dot(board - new Vector3(0, BroadcastCamera.Height, BroadcastCamera.Back), forward);
        float step = 2 * depth * HalfTan() / Game.Width;
        return MathF.Round(x / step) * step;
    }

    static float HalfTan()
    {
        return MathF.Tan(BroadcastCamera.FovY * MathF.PI / 360f) * Game.Width / Game.Height;
    }

    /// <summary>A critically damped spring that settles in about a second.</summary>
    static void Spring(ref float value, ref float velocity, float target, float dt)
    {
        const float omega = 4f / 1.1f;
        float e = MathF.Exp(-omega * dt);
        float diff = value - target;
        float temp = (velocity + omega * diff) * dt;
        velocity = (velocity - omega * temp) * e;
        value = target + (diff + temp) * e;
    }

    /// <summary>One Zamboni drive.</summary>
    internal sealed class Drive
    {
        /// <summary>Gets or sets the seconds since it set off.</summary>
        public float Seconds { get; set; }

        /// <summary>Gets or sets the seconds it has been on show, for the skip guard.</summary>
        public float Shown { get; set; }

        /// <summary>Gets the period that just ended.</summary>
        public int Period { get; init; }

        /// <summary>Gets or sets a value indicating whether it is done, driven out or skipped.</summary>
        public bool Finished { get; set; }

        /// <summary>Gets or sets a value indicating whether it was skipped; it then disappears at once.</summary>
        public bool Skipped { get; set; }
    }
}
