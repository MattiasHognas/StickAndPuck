using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Camera;

/// <summary>The gameplay camera's motion. It pans along the rink after the puck, leading a little in the
/// direction of play, and zooms out with puck speed when the setting is on. When play comes to the near
/// boards it also slides down the frame, so the near boards and the penalty boxes come into view, and
/// slides back up as play leaves them, which keeps the far benches in frame. The slide is along the
/// screen's own down direction (lower and back, same tilt), so every point keeps its distance along
/// the view and the far boards keep their size. Both moves go in whole pixels at the far boards, so
/// the ad boards (which match their texture texel for texel) never shimmer. Anything that changes the
/// boards' size on screen (the zoom, or a plain truck in z) breaks that match, which is why the camera
/// never trucks.</summary>
internal sealed class FollowCamera
{
    /// <summary>Seconds of puck travel the camera leads by.</summary>
    const float LeadSeconds = 0.45f;

    /// <summary>The most the camera leads the puck, in metres.</summary>
    const float MaxLead = 4f;

    /// <summary>How quickly the camera catches up: the spring's settle time in seconds.</summary>
    const float SettleSeconds = 0.6f;

    /// <summary>How quickly the lead turns around when play changes direction.</summary>
    const float LeadTurnSeconds = 0.8f;

    /// <summary>How far past the end boards the view may reach at centre ice, so play behind the net shows.</summary>
    const float EndMargin = 1.5f;

    /// <summary>Puck speeds (m/s) where zoom starts and where it is fully out.</summary>
    const float ZoomFromSpeed = 5f, ZoomFullSpeed = 18f;

    /// <summary>How much wider the field gets at full zoom.</summary>
    const float ZoomAmount = 0.14f;

    /// <summary>How quickly zoom follows speed, in seconds.</summary>
    const float ZoomSeconds = 0.9f;

    /// <summary>Puck depths (m, positive toward the viewer) where the slide down starts and where it is full.
    /// Play nearer the centre or the far boards leaves the camera where it is: the dead zone.</summary>
    const float DropFromZ = 3.5f, DropFullZ = 10.5f;

    /// <summary>How far the camera slides down at most, in metres: the near boards and penalty boxes come into view.</summary>
    const float MaxDrop = 3.5f;

    /// <summary>The slide's settle time in seconds: slower than the pan, so a puck wobbling near the boards doesn't bob the view.</summary>
    const float DropSettleSeconds = 1.2f;

    const float Z = BroadcastCamera.Back;
    float x, vx, drop, vdrop, lead, zoom;

    /// <summary>Gets the camera for the current state.</summary>
    public Camera3D Camera { get; private set; } = BroadcastCamera.Straight();

    /// <summary>Gets one pixel of the frame at the far boards, in metres; a camera moved by whole steps of this keeps the boards on the pixel grid.</summary>
    public float PixelStep => PixelAtFarBoards();

    /// <summary>Jumps straight to where the camera wants to be, with no easing (start of play, end of a replay).</summary>
    public void Cut(World world)
    {
        lead = 0;
        vx = 0;
        zoom = 0;
        vdrop = 0;
        x = Goal(world);
        drop = DropGoal(world);
        Camera = Build();
    }

    /// <summary>Moves the camera one step toward the play.</summary>
    public void Update(World world, float dt)
    {
        var puck = world.Puck;
        float wantLead = Math.Clamp(puck.Velocity.X * LeadSeconds, -MaxLead, MaxLead);
        lead += (wantLead - lead) * (1 - MathF.Exp(-dt * 4f / LeadTurnSeconds));

        float speed = new Vector2(puck.Velocity.X, puck.Velocity.Z).Length();
        float wantZoom = world.Settings.CameraZoom ? SmoothStep(ZoomFromSpeed, ZoomFullSpeed, speed) : 0;
        zoom += (wantZoom - zoom) * (1 - MathF.Exp(-dt * 4f / ZoomSeconds));

        Spring(ref x, ref vx, Goal(world), dt, SettleSeconds);
        x = Math.Clamp(x, -MaxX(), MaxX());
        Spring(ref drop, ref vdrop, DropGoal(world), dt, DropSettleSeconds);
        drop = Math.Clamp(drop, 0, MaxDrop);
        Camera = Build();
    }

    /// <summary>Where the camera wants to be: over the puck (on the carrier's blade while carried) plus the lead, clamped to the rink.</summary>
    float Goal(World world)
    {
        return Math.Clamp(world.Puck.Position.X + lead, -MaxX(), MaxX());
    }

    /// <summary>How far down the camera wants to be: none while play is away from the near boards, easing in toward them.</summary>
    static float DropGoal(World world)
    {
        return MaxDrop * SmoothStep(DropFromZ, DropFullZ, world.Puck.Position.Z);
    }

    float Fov => BroadcastCamera.FovY * (1 + ZoomAmount * zoom);

    /// <summary>Half the width of the view at centre ice, in metres.</summary>
    float HalfWidthAtCentre()
    {
        var forward = BroadcastCamera.Forward;
        float depth = Vector3.Dot(new Vector3(0, -BroadcastCamera.Height, -Z), forward);
        return depth * HalfTan();
    }

    float HalfTan()
    {
        return MathF.Tan(Fov * MathF.PI / 360f) * Game.Width / Game.Height;
    }

    /// <summary>The farthest the camera pans: the end boards reach the frame edge at centre ice, plus a margin.</summary>
    float MaxX()
    {
        return Math.Max(0, RinkSpec.HalfLength + EndMargin - HalfWidthAtCentre());
    }

    /// <summary>One pixel of the 480-wide frame, in metres, at the far boards. Moving the camera across the
    /// view (along x, or down the frame) by whole steps of this shifts the far boards by whole pixels.</summary>
    float PixelAtFarBoards()
    {
        var forward = BroadcastCamera.Forward;
        var board = new Vector3(0, RinkSpec.BoardHeight / 2, -RinkSpec.HalfWidth);
        float depth = Vector3.Dot(board - new Vector3(0, BroadcastCamera.Height, Z), forward);
        return 2 * depth * HalfTan() / Game.Width;
    }

    Camera3D Build()
    {
        float step = PixelAtFarBoards();
        return BroadcastCamera.Lowered(MathF.Round(x / step) * step, MathF.Round(drop / step) * step, Fov);
    }

    /// <summary>A critically damped spring: eases in and out with no overshoot.</summary>
    static void Spring(ref float value, ref float velocity, float target, float dt, float settleSeconds)
    {
        float omega = 4f / settleSeconds;
        float e = MathF.Exp(-omega * dt);
        float diff = value - target;
        float temp = (velocity + omega * diff) * dt;
        velocity = (velocity - omega * temp) * e;
        value = target + (diff + temp) * e;
    }

    static float SmoothStep(float from, float to, float v)
    {
        float t = Math.Clamp((v - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
