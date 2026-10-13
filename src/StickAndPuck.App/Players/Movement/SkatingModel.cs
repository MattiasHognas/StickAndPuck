using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>How one skater moves over the ice from its command and its traits. Pure maths with no
/// Raylib, so AI, tests and other systems can ask how fast a player is without running a frame.
/// <list type="bullet">
/// <item>Top speed comes from the top speed trait, acceleration from the acceleration trait,
/// both scaled down a little as energy runs out (the lines system drains energy).</item>
/// <item>Pushing the stick carves the skater's path toward it, tighter when slow and wider at speed.</item>
/// <item>Pulling the stick against the motion is a hockey stop; letting go glides.</item>
/// <item>The boards, with their rounded corners, stop the skater.</item>
/// </list></summary>
public static class SkatingModel
{
    /// <summary>The top speed of an average player (trait 50, fresh), in metres per second. Clips scale their rate by it.</summary>
    public const float AverageTopSpeed = SlowestTop + (FastestTop - SlowestTop) * 0.5f;

    /// <summary>How far the body's centre stays from the boards.</summary>
    public const float BodyRadius = 0.45f;

    const float SlowestTop = 6.0f, FastestTop = 8.4f;       // m/s at top speed 0 and 100
    const float SlowestPush = 5f, FastestPush = 12f;        // m/s² at acceleration 0 and 100
    const float SprintBoost = 1.15f;                        // sprint raises the top speed
    const float Glide = 0.8f;                               // m/s² lost when coasting
    const float CarveDrag = 0.35f;                          // share of speed lost per radian turned
    const float StopAngle = 2.2f;                           // radians between stick and motion that start a hockey stop
    const float StopDecel = 16f;                            // m/s² at acceleration 50
    const float TightTurn = 9f, WideTurn = 2.8f;            // rad/s turning at standstill and at top speed
    const float FaceTurn = 12f;                             // rad/s the body turns to face its path
    const float Pivot = 0.6f;                               // m/s below which the skater turns on the spot
    const float Dead = 0.1f;                                // stick lengths below this count as no input

    /// <summary>Gets how much energy leaves of a player's skating: 1 when fresh, 0.7 when spent.</summary>
    public static float EnergyFactor(float energy)
    {
        return 0.7f + 0.3f * Math.Clamp(energy, 0f, 1f);
    }

    /// <summary>Gets a player's top speed when not sprinting, in metres per second.</summary>
    public static float TopSpeed(Traits traits, float energy = 1f)
    {
        return (SlowestTop + (FastestTop - SlowestTop) * Traits.Unit(traits.TopSpeed)) * EnergyFactor(energy);
    }

    /// <summary>Gets a player's push from a standstill, in metres per second squared.</summary>
    public static float Push(Traits traits, float energy = 1f)
    {
        return (SlowestPush + (FastestPush - SlowestPush) * Traits.Unit(traits.Acceleration)) * EnergyFactor(energy);
    }

    /// <summary>Moves one skater by one step: velocity, heading and position.</summary>
    public static void Step(Player p, float dt)
    {
        var traits = p.Data.Traits;
        var stick = p.Command.Move;
        float amount = Math.Min(stick.Length(), 1f);
        var v = new Vector2(p.Velocity.X, p.Velocity.Z);
        float speed = v.Length();

        if (amount < Dead)
        {
            speed = Math.Max(0, speed - Glide * dt);
            v = speed > 0 ? Vector2.Normalize(v) * speed : Vector2.Zero;
        }
        else
        {
            var want = stick / stick.Length();
            float top = TopSpeed(traits, p.Energy) * (p.Command.Sprint ? SprintBoost : 1f) * amount;
            float push = Push(traits, p.Energy);
            float turn = speed > 0.01f ? Angle(v, want) : 0f;

            if (MathF.Abs(turn) > StopAngle && speed > Pivot)
            {
                // stick against the motion: dig the edges in and stop, sharper players stop quicker
                float stop = StopDecel * (0.7f + 0.6f * Traits.Unit(traits.Acceleration)) * EnergyFactor(p.Energy);
                speed = Math.Max(0, speed - stop * dt);
                v = Vector2.Normalize(v) * speed;
            }
            else
            {
                // carve toward the stick: tight when slow, wide at speed; turning bleeds a little speed
                // nearly stopped, the skater pivots straight onto the stick instead
                var dir = speed > Pivot ? Vector2.Normalize(v) : want;
                float rate = TightTurn + (WideTurn - TightTurn) * Math.Clamp(speed / FastestTop, 0f, 1f);
                float step = Math.Clamp(turn, -rate * dt, rate * dt);
                dir = Rotate(dir, step);
                speed = Math.Max(0, speed - speed * CarveDrag * MathF.Abs(step));

                // push hardest from a standstill, easing off as the skater nears top speed
                if (speed < top) speed = Math.Min(top, speed + push * (1f - 0.6f * speed / top) * dt);
                else speed = Math.Max(top, speed - Glide * 4f * dt);
                v = dir * speed;
            }
        }

        FaceTravel(p, v, stick, amount, dt);
        var next = new Vector2(p.Position.X, p.Position.Z) + v * dt;
        (next, v) = Boards(next, v);
        p.Velocity = new Vector3(v.X, 0, v.Y);
        p.Position = new Vector3(next.X, p.Position.Y, next.Y);
    }

    /// <summary>The body faces where it is going; standing still, it turns to the stick.</summary>
    static void FaceTravel(Player p, Vector2 v, Vector2 stick, float amount, float dt)
    {
        Vector2 look;
        if (v.LengthSquared() > 0.25f) look = v;
        else if (amount >= Dead) look = stick;
        else return;

        // heading 0 faces +z; a rink vector's y is +z
        float target = SimMath.Atan2(look.X, look.Y);
        float delta = MathF.IEEERemainder(target - p.Heading, MathF.Tau);
        p.Heading = MathF.IEEERemainder(p.Heading + Math.Clamp(delta, -FaceTurn * dt, FaceTurn * dt), MathF.Tau);
    }

    /// <summary>Keeps the body inside the boards and their rounded corners, and drops the speed into them.</summary>
    public static (Vector2 Pos, Vector2 V) Boards(Vector2 pos, Vector2 v)
    {
        float hx = RinkSpec.HalfLength - BodyRadius, hz = RinkSpec.HalfWidth - BodyRadius;
        float r = RinkSpec.CornerRadius - BodyRadius;
        var corner = new Vector2(Math.Sign(pos.X) * (hx - r), Math.Sign(pos.Y) * (hz - r));
        Vector2 normal;   // pointing out of the rink, zero when clear of the boards
        if (MathF.Abs(pos.X) > hx - r && MathF.Abs(pos.Y) > hz - r)
        {
            var off = pos - corner;
            if (off.Length() <= r) return (pos, v);
            normal = Vector2.Normalize(off);
            pos = corner + normal * r;
        }
        else if (MathF.Abs(pos.X) > hx) { normal = new Vector2(Math.Sign(pos.X), 0); pos.X = Math.Sign(pos.X) * hx; }
        else if (MathF.Abs(pos.Y) > hz) { normal = new Vector2(0, Math.Sign(pos.Y)); pos.Y = Math.Sign(pos.Y) * hz; }
        else
        {
            return (pos, v);
        }

        // lose the speed into the boards with a small bounce, keep the speed along them
        float into = Vector2.Dot(v, normal);
        if (into > 0) v -= normal * into * 1.2f;
        return (pos, v);
    }

    static float Angle(Vector2 from, Vector2 to)
    {
        return SimMath.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to));
    }

    static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = SimMath.Cos(radians), s = SimMath.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
