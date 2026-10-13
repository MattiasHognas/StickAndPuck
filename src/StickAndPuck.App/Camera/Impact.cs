using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.Camera;

/// <summary>
/// W30: how big moments feel. A hard check (strength 1.5 and up) or a goal freezes the skaters and
/// the puck for a few steps (hit-stop), shakes the camera in whole pixels and rumbles the pads of
/// the people involved. The fight close-up moved to <see cref="CloseUp"/> (W61).
/// </summary>
/// <remarks>
/// The hit-stop works like the replay: it runs after every gameplay system and puts the players and
/// puck back where the hit left them each step, so for those steps nothing moves on the ice. Timers
/// the rules own (the clock) still run, which over a tenth of a second nobody sees.
/// </remarks>
internal sealed class Impact
{
    /// <summary>Hit-stop steps for a check at the hard-hit line, and the most for a crushing one.</summary>
    const int CheckHoldMin = 4, CheckHoldMax = 8;

    /// <summary>Hit-stop steps when a goal goes in.</summary>
    const int GoalHold = 7;

    /// <summary>Camera shake for a hard check: pixels at the hard-hit line and the most, and how long it lasts.</summary>
    const float CheckShakeMin = 3f, CheckShakeMax = 6f, CheckShakeSeconds = 0.35f;

    /// <summary>Camera shake when a goal goes in, in pixels and seconds.</summary>
    const float GoalShake = 4f, GoalShakeSeconds = 0.5f;

    readonly ReplayBuffer.Frame held = new();
    readonly Random rng = new(30);
    int holdSteps;
    float shake, shakeLeft, shakeSeconds;
    Vector2 shakeOffset;
    int shakeStep;

    /// <summary>Gets the camera shake as whole-pixel steps right and up, for this step.</summary>
    public Vector2 ShakeOffset => shakeLeft > 0 ? shakeOffset : Vector2.Zero;

    /// <summary>Gets the steps of hit-stop left.</summary>
    public int Holding => holdSteps;

    /// <summary>Runs one step: holds the ice still during a hit-stop, starts new ones, and moves shake and close-up along.</summary>
    /// <param name="world">The match state, after every gameplay system has run this step.</param>
    /// <param name="dt">The step length.</param>
    public void Update(World world, float dt)
    {
        Rumble.Update(dt);
        if (Replay.Playing)
        {
            holdSteps = 0;
            shakeLeft = 0;
            return;
        }

        if (holdSteps > 0)
        {
            ReplayBuffer.Apply(held, world);
            holdSteps--;
        }
        else
        {
            foreach (var e in world.Events.Current)
            {
                if (e is CheckLanded { Strength: >= CheckingModel.HardHit } check) HardCheck(world, check);
                else if (e is GoalScored goal) Goal(world, goal);
            }
        }

        Shake(dt);
    }

    /// <summary>Moves a camera by this step's shake.</summary>
    /// <param name="camera">The gameplay camera.</param>
    /// <param name="pixel">One pixel at the far boards, in metres.</param>
    /// <returns>The camera to draw with.</returns>
    public Camera3D Apply(Camera3D camera, float pixel)
    {
        var offset = ShakeOffset;
        if (offset != Vector2.Zero)
        {
            var move = new Vector3(offset.X * pixel, offset.Y * pixel, 0);
            camera.Position += move;
            camera.Target += move;
        }
        return camera;
    }

    void HardCheck(World world, CheckLanded e)
    {
        float over = Math.Clamp((e.Strength - CheckingModel.HardHit) / 1.5f, 0f, 1f);
        Hold(world, CheckHoldMin + (int)MathF.Round((CheckHoldMax - CheckHoldMin) * over));
        Kick(CheckShakeMin + ((CheckShakeMax - CheckShakeMin) * over), CheckShakeSeconds);

        // the checker feels it, the one hit feels it harder
        Pulse(world, world.Players[e.CheckerId].Slot, 0.45f + (0.4f * over), 0.6f, 0.16f);
        var victim = world.Players
            .Where(p => p.Side != e.Side && p.OnIce)
            .MinBy(p => Vector3.DistanceSquared(p.Position, e.Position));
        if (victim != null) Pulse(world, victim.Slot, 0.75f + (0.25f * over), 0.8f, 0.24f + (0.12f * over));
        Console.WriteLine($"SAP: impact: hard check {e.Strength.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}, hold {holdSteps} steps");
    }

    void Goal(World world, GoalScored e)
    {
        Hold(world, GoalHold);
        Kick(GoalShake, GoalShakeSeconds);
        for (int slot = 0; slot < MatchSetup.Slots && slot < world.Setup.Inputs.Count; slot++)
        {
            bool scored = world.Setup.Inputs[slot].Side == e.Side;
            Rumble.Pulse(world.Setup.Inputs[slot].Device, scored ? 0.6f : 0.3f, scored ? 1f : 0.3f, scored ? 0.5f : 0.2f);
        }
        Console.WriteLine("SAP: impact: goal");
    }

    void Hold(World world, int steps)
    {
        ReplayBuffer.Capture(world, held);
        holdSteps = steps;
    }

    void Kick(float pixels, float seconds)
    {
        if (shakeLeft > 0 && shake * (shakeLeft / shakeSeconds) > pixels) return;
        shake = pixels;
        shakeSeconds = seconds;
        shakeLeft = seconds;
        shakeStep = 0;
    }

    /// <summary>
    /// A new offset every other step, swinging side to side and shrinking to nothing; whole pixels so
    /// the boards stay crisp.
    /// </summary>
    void Shake(float dt)
    {
        if (shakeLeft <= 0) return;
        shakeLeft -= dt;
        if (shakeStep++ % 2 != 0) return;
        float amp = shake * Math.Max(shakeLeft, 0) / shakeSeconds;
        float side = shakeStep % 4 == 1 ? 1 : -1;
        float x = side * MathF.Round(amp * (0.6f + (0.4f * rng.NextSingle())));
        float y = MathF.Round(((rng.NextSingle() * 2) - 1) * amp * 0.6f);
        shakeOffset = new Vector2(x, y);
    }

    static void Pulse(World world, int slot, float strong, float weak, float seconds)
    {
        if (slot < 0 || slot >= world.Setup.Inputs.Count) return;
        Rumble.Pulse(world.Setup.Inputs[slot].Device, strong, weak, seconds);
    }
}
