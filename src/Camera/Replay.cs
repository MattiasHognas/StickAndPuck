using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Camera;

/// <summary>
/// Goal replays. Play is recorded every step; a goal lets the celebration run for a moment on the
/// gameplay camera, then cuts to the low angled ice-level camera and plays the build-up again,
/// slowing down around the goal. When it ends (or a skip button is pressed) the players and puck go
/// back to where they were and the gameplay camera cuts back.
/// </summary>
/// <remarks>
/// While a replay plays, it writes the recorded players and puck into the world each step, after
/// every gameplay system has run, so the scene draws the replay. Rules should hold the match in
/// <see cref="MatchPhase.Goal"/> (puck dead) until <see cref="Playing"/> is false again before
/// they set up the next faceoff, or the replay ends by putting the players back where the goal left them.
/// </remarks>
public sealed class Replay
{
    /// <summary>Seconds kept in the buffer.</summary>
    const float BufferSeconds = 7f;

    /// <summary>Seconds of celebration on the gameplay camera before the replay cuts in.</summary>
    const float CelebrateSeconds = 1.5f;

    /// <summary>Seconds of play shown before the goal, and after it.</summary>
    const float LeadInSeconds = 3f, TailSeconds = 0.75f;

    /// <summary>The replay slows to this speed from <see cref="SlowFromSeconds"/> before the goal until just after it.</summary>
    const float SlowRate = 0.4f, SlowFromSeconds = 1.2f, SlowUntilSeconds = 0.3f;

    /// <summary>A skip press counts only after this long, so a held shoot button does not skip at once.</summary>
    const float SkipAfterSeconds = 0.3f;

    /// <summary>Where the ice-level camera sits relative to the puck: behind the play (scaled by the attacked end), low, toward the viewer.</summary>
    static readonly Vector3 ChaseOffset = new(-6.5f, 2.2f, 5.5f);

    static readonly Color TagText = new(0xef, 0xe0, 0xc2, 255);
    static readonly Color TagDot = new(0xe9, 0x6f, 0x5d, 255);
    static readonly Color TagBack = new(0x18, 0x13, 0x1f, 255);

    readonly ReplayBuffer buffer = new((int)(BufferSeconds / Game.Step));
    readonly ReplayBuffer.Frame live = new();
    readonly float testGoalAt = TestGoalTime();
    float goalTime = float.NaN, playhead, shown;
    int endIndex, end = 1;
    bool waiting;
    Vector3 camPos, camTarget;

    /// <summary>Gets a value indicating whether a replay is playing right now. Rules wait on it before the next faceoff.</summary>
    public static bool Playing { get; private set; }

    /// <summary>Gets the replay camera while <see cref="Playing"/>.</summary>
    public Camera3D Camera => BroadcastCamera.IceLevel(camPos, camTarget);

    /// <summary>Records or plays back one step. Runs after every gameplay system.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="dt">The step length.</param>
    /// <returns>True when the replay just ended and the gameplay camera should cut back.</returns>
    public bool Update(World world, float dt)
    {
        if (Playing) return Play(world, dt);

        buffer.Record(world);
        if (!waiting && GoalScored(world, out float x))
        {
            waiting = true;
            goalTime = world.Time;
            end = x < 0 ? -1 : 1;
        }
        if (waiting && world.Time - goalTime >= CelebrateSeconds) Begin(world);
        return false;
    }

    /// <summary>Whether a goal was scored this step, and at which end. SAP_REPLAY_AT fakes one at that world time, for checks before the rules exist.</summary>
    bool GoalScored(World world, out float x)
    {
        x = world.Puck.Position.X;
        foreach (var e in world.Events.Current)
        {
            if (e.Kind != MatchEventKind.Goal) continue;
            if (e.Position.X != 0) x = e.Position.X;
            return true;
        }
        return world.Time >= testGoalAt && world.Time - Game.Step < testGoalAt;
    }

    static float TestGoalTime()
    {
        string? s = Environment.GetEnvironmentVariable("SAP_REPLAY_AT");
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : float.PositiveInfinity;
    }

    void Begin(World world)
    {
        waiting = false;
        Playing = true;
        ReplayBuffer.Capture(world, live);
        int startIndex = buffer.IndexAt(goalTime - LeadInSeconds);
        endIndex = buffer.IndexAt(goalTime + TailSeconds);
        playhead = startIndex;
        shown = 0;
        ReplayBuffer.Apply(buffer[startIndex], world);
        camTarget = Target(world);
        camPos = ChasePosition(world);
    }

    bool Play(World world, float dt)
    {
        shown += dt;
        float t = buffer[(int)playhead].Time - goalTime;
        playhead += t >= -SlowFromSeconds && t <= SlowUntilSeconds ? SlowRate : 1f;
        if (playhead >= endIndex || (shown >= SkipAfterSeconds && SkipPressed()))
        {
            ReplayBuffer.Apply(live, world);
            buffer.Clear();
            Playing = false;
            return true;
        }

        ReplayBuffer.Apply(buffer[(int)playhead], world);   // whole frames: held, like the clips
        float ease = 1 - MathF.Exp(-dt * 6f);
        camTarget += (Target(world) - camTarget) * ease;
        camPos += (ChasePosition(world) - camPos) * ease;
        return false;
    }

    static Vector3 Target(World world)
    {
        return world.Puck.Position + new Vector3(0, 0.5f, 0);
    }

    Vector3 ChasePosition(World world)
    {
        var p = world.Puck.Position + ChaseOffset with { X = ChaseOffset.X * end };
        p.X = Math.Clamp(p.X, -RinkSpec.HalfLength + 1, RinkSpec.HalfLength - 1);
        return p;
    }

    static bool SkipPressed()
    {
        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.Space)) return true;
        for (int pad = 0; pad < 2; pad++)
            if (IsGamepadAvailable(pad) && (IsGamepadButtonPressed(pad, GamepadButton.RightFaceDown) || IsGamepadButtonPressed(pad, GamepadButton.MiddleRight)))
                return true;
        return false;
    }

    /// <summary>Draws a blinking REPLAY tag in the top right while a replay plays.</summary>
    public void DrawHud()
    {
        if (!Playing) return;
        const int w = 54, h = 13, x = Game.Width - w - 6, y = 6;
        DrawRectangle(x, y, w, h, TagBack);
        if ((int)(shown * 2) % 2 == 0) DrawRectangle(x + 4, y + 4, 5, 5, TagDot);
        DrawText("REPLAY", x + 13, y + 2, 10, TagText);
    }
}
