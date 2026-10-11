using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>The two kinds of shot.</summary>
public enum ShotKind
{
    /// <summary>A quick tap of shoot: a wrist shot, quick and accurate.</summary>
    Wrist,

    /// <summary>Shoot held past <see cref="PuckHandling.SlapAfter"/>: a slap shot, harder the longer the wind-up.</summary>
    Slap,
}

/// <summary>
/// W4b: the numbers and aiming maths of puck handling, with no Raylib and no state, so AI (W6)
/// and tests can ask what a pass or shot would do. Traits read here: puck control (reach, how
/// hard a puck can be taken, how tight the puck sits on the blade), smart passes (picking and
/// leading the receiver, pass accuracy), shot power and shot accuracy.
/// </summary>
public static class PuckHandling
{
    /// <summary>Shoot held this long or longer makes a slap shot; a shorter tap is a wrist shot.</summary>
    public const float SlapAfter = 0.3f;

    /// <summary>A slap shot wound up this long is at full power.</summary>
    public const float FullCharge = 1.0f;

    /// <summary>Seconds from the start of the shoot clip to the puck leaving the blade (the sweep pose).</summary>
    public const float StrikeDelay = 6 / 24f;

    /// <summary>Seconds after a release before the same player can take the puck again.</summary>
    public const float Repick = 0.35f;

    /// <summary>A puck higher than this flies over the stick.</summary>
    public const float StickHeight = 0.25f;

    /// <summary>A puck this close to a skater's centre is at their skates.</summary>
    public const float SkateReach = 0.5f;

    /// <summary>Passes look for teammates within this angle of the stick, in radians.</summary>
    public const float PassCone = 70f * MathF.PI / 180f;

    const float LongestPass = 40f;
    const float NetAimCone = 80f * MathF.PI / 180f;
    const float LongestShot = 26f;

    /// <summary>Gets how far from the blade a player can take a loose puck, in metres.</summary>
    /// <param name="t">The player's traits.</param>
    /// <returns>The reach.</returns>
    public static float Reach(Traits t)
    {
        return 0.45f + 0.35f * Traits.Unit(t.PuckControl);
    }

    /// <summary>Gets the fastest puck, relative to the player, that the player can take cleanly.</summary>
    /// <param name="t">The player's traits.</param>
    /// <returns>The speed in metres per second.</returns>
    public static float CatchSpeed(Traits t)
    {
        return 14f + 12f * Traits.Unit(t.PuckControl);
    }

    /// <summary>Gets how quickly a carried puck settles back onto the blade, per second; higher is tighter.</summary>
    /// <param name="t">The player's traits.</param>
    /// <returns>The rate.</returns>
    public static float Follow(Traits t)
    {
        return 25f + 35f * Traits.Unit(t.PuckControl);
    }

    /// <summary>Gets the speed of a wrist shot.</summary>
    /// <param name="t">The shooter's traits.</param>
    /// <returns>Metres per second.</returns>
    public static float WristSpeed(Traits t)
    {
        return 20f + 9f * Traits.Unit(t.ShotPower);
    }

    /// <summary>Gets the speed of a slap shot after a wind-up.</summary>
    /// <param name="t">The shooter's traits.</param>
    /// <param name="charge">Seconds shoot was held.</param>
    /// <returns>Metres per second.</returns>
    public static float SlapSpeed(Traits t, float charge)
    {
        float wound = Math.Clamp((charge - SlapAfter) / (FullCharge - SlapAfter), 0f, 1f);
        return (27f + 15f * Traits.Unit(t.ShotPower)) * (0.75f + 0.25f * wound);
    }

    /// <summary>Gets the kind of shot a hold of shoot makes.</summary>
    /// <param name="charge">Seconds shoot was held.</param>
    /// <returns>The kind.</returns>
    public static ShotKind KindOf(float charge)
    {
        return charge >= SlapAfter ? ShotKind.Slap : ShotKind.Wrist;
    }

    /// <summary>Gets the speed of a pass over a distance: firm enough to arrive, soft enough to take.</summary>
    /// <param name="distance">Metres to the receiver's blade.</param>
    /// <returns>Metres per second.</returns>
    public static float PassSpeed(float distance)
    {
        return Math.Clamp(8f + 0.35f * distance, 8f, 17f);
    }

    /// <summary>Gets the end of the rink a club attacks: +1 for the +x net, -1 for the -x net.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>+1 or -1.</returns>
    public static int AttackEnd(World world, Side side)
    {
        return world.Match.DefendsLeft == side ? 1 : -1;
    }

    /// <summary>Where a player's blade is: just ahead of the body, on the stick side.</summary>
    /// <param name="p">The player.</param>
    /// <returns>The blade on the ice.</returns>
    public static Vector3 Blade(Player p)
    {
        return Blade(p.Position, p.Heading);
    }

    /// <summary>Where a blade is for a body at a position and heading.</summary>
    /// <param name="position">The body.</param>
    /// <param name="heading">Radians; 0 faces +z.</param>
    /// <returns>The blade on the ice.</returns>
    public static Vector3 Blade(Vector3 position, float heading)
    {
        var tip = position + Vector3.Transform(new Vector3(0.55f, 0, 1.05f) * SkaterRenderer.Scale, Matrix4x4.CreateRotationY(heading));
        return tip with { Y = 0 };
    }

    /// <summary>Gets the way a player faces, on the ice (x, z).</summary>
    /// <param name="p">The player.</param>
    /// <returns>A unit vector.</returns>
    public static Vector2 Facing(Player p)
    {
        return new Vector2(MathF.Sin(p.Heading), MathF.Cos(p.Heading));
    }

    /// <summary>Gets the way a player means: the stick when pushed, else the way the body faces.</summary>
    /// <param name="p">The player.</param>
    /// <returns>A unit vector on the ice (x, z).</returns>
    public static Vector2 Intent(Player p)
    {
        var stick = p.Command.Move;
        return stick.Length() > 0.3f ? Vector2.Normalize(stick) : Facing(p);
    }

    /// <summary>
    /// Picks who a pass goes to: the teammate on the ice nearest the way the passer means, within
    /// <see cref="PassCone"/>, closer ones preferred. Goalies are not picked.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="passer">The passer.</param>
    /// <returns>The receiver, or null to pass into space.</returns>
    public static Player? PickReceiver(World world, Player passer)
    {
        var dir = Intent(passer);
        var from = Flat(Blade(passer));
        Player? best = null;
        float bestScore = float.MaxValue;
        foreach (var mate in world.OnIce(passer.Side))
        {
            if (mate.Id == passer.Id || mate.IsGoalie) continue;
            var to = Flat(Blade(mate)) - from;
            float d = to.Length();
            if (d is < 2f or > LongestPass) continue;
            float angle = MathF.Abs(Angle(dir, to));
            if (angle > PassCone) continue;
            float score = angle / PassCone + 0.5f * d / LongestPass;
            if (score < bestScore) { bestScore = score; best = mate; }
        }
        return best;
    }

    /// <summary>
    /// Where to send a pass so it meets a moving receiver's blade. A smarter passer leads the
    /// receiver closer to where they will be; a poor one aims nearer where they are now.
    /// </summary>
    /// <param name="from">The puck.</param>
    /// <param name="receiver">The receiver.</param>
    /// <param name="hockeyIq">The passer's hockey IQ.</param>
    /// <returns>The aim point on the ice and the pass speed.</returns>
    public static (Vector3 Target, float Speed) Lead(Vector3 from, Player receiver, int hockeyIq)
    {
        float lead = 0.4f + 0.6f * Traits.Unit(hockeyIq);
        var blade = Blade(receiver);
        var target = blade;
        float speed = PassSpeed(Vector3.Distance(from, blade));
        for (int i = 0; i < 3; i++)
        {
            float d = Vector3.Distance(from, target);
            speed = PassSpeed(d);
            target = blade + receiver.Velocity * SlideTime(d, speed) * lead;
        }
        return (target with { Y = 0 }, speed);
    }

    /// <summary>Gets how long a puck sliding on the ice takes to cover a distance, slowing as W3's physics slows it.</summary>
    /// <param name="distance">Metres.</param>
    /// <param name="speed">Starting speed in metres per second.</param>
    /// <returns>Seconds, or a long time when it stops short.</returns>
    public static float SlideTime(float distance, float speed)
    {
        const float step = 1 / 60f;
        float t = 0;
        while (distance > 0 && speed > 0.1f && t < 5f)
        {
            distance -= speed * step;
            speed -= (Physics.PuckTuning.IceFriction + Physics.PuckTuning.Drag * speed) * step;
            t += step;
        }
        return t;
    }

    /// <summary>
    /// The velocity of a pass toward a point, with the passer's error: a smart passer is accurate,
    /// a poor one sprays the angle and the weight.
    /// </summary>
    /// <param name="from">The puck.</param>
    /// <param name="target">The aim point.</param>
    /// <param name="speed">The planned speed.</param>
    /// <param name="hockeyIq">The passer's hockey IQ.</param>
    /// <param name="rng">Randomness.</param>
    /// <returns>The puck's velocity, flat on the ice.</returns>
    public static Vector3 PassVelocity(Vector3 from, Vector3 target, float speed, int hockeyIq, Random rng)
    {
        float miss = 1f - Traits.Unit(hockeyIq);
        var dir = Flat(target - from);
        dir = dir.LengthSquared() > 1e-6f ? Vector2.Normalize(dir) : Vector2.UnitX;
        dir = Rotate(dir, Gauss(rng) * Deg(0.3f + 2f * miss));
        speed *= 1f + Gauss(rng) * 0.08f * miss;
        return new Vector3(dir.X, 0, dir.Y) * speed;
    }

    /// <summary>
    /// The velocity of a shot. Facing the attacked net and within range, the shot goes at the net:
    /// to the side the stick points (up or down the screen), else to the far side, at a height
    /// that varies; shot accuracy sets how far it strays, and slap shots stray more. Otherwise the
    /// shot goes the way the player means, low.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="shooter">The shooter.</param>
    /// <param name="from">The puck.</param>
    /// <param name="kind">Wrist or slap.</param>
    /// <param name="speed">Speed over the ice.</param>
    /// <param name="rng">Randomness.</param>
    /// <returns>The puck's velocity.</returns>
    public static Vector3 ShotVelocity(World world, Player shooter, Vector3 from, ShotKind kind, float speed, Random rng)
    {
        int end = AttackEnd(world, shooter.Side);
        var net = new Vector2(end * RinkSpec.GoalLineX, 0);
        var toNet = net - Flat(from);
        var intent = Intent(shooter);
        bool atNet = end * from.X < RinkSpec.GoalLineX && toNet.Length() < LongestShot && MathF.Abs(Angle(intent, toNet)) < NetAimCone;
        if (!atNet) return new Vector3(intent.X * speed, kind == ShotKind.Wrist ? 1f : 0f, intent.Y * speed);

        float stray = (1f - Traits.Unit(shooter.Data.Traits.ShotAccuracy)) * (kind == ShotKind.Slap ? 1.4f : 1f);
        float side = RinkSpec.NetWidth / 2 - 0.2f;
        float stickZ = shooter.Command.Move.Y;
        float z = MathF.Abs(stickZ) > 0.35f ? MathF.Sign(stickZ) * side
            : MathF.Abs(from.Z) > 0.3f ? -MathF.Sign(from.Z) * side * 0.8f
            : (rng.NextSingle() * 2 - 1) * side;
        float h = kind == ShotKind.Slap ? 0.1f + rng.NextSingle() * 0.4f : 0.15f + rng.NextSingle() * 0.6f;
        h = Math.Max(0.02f, h + Gauss(rng) * 0.3f * stray);

        var dir = Vector2.Normalize(new Vector2(end * RinkSpec.GoalLineX, z) - Flat(from));
        dir = Rotate(dir, Gauss(rng) * Deg(0.3f + 3.5f * stray));
        float distance = MathF.Abs((end * RinkSpec.GoalLineX - from.X) / dir.X);
        float t = distance / speed;
        float vy = (h - from.Y) / t + 0.5f * Physics.PuckTuning.Gravity * t;
        return new Vector3(dir.X * speed, vy, dir.Y * speed);
    }

    /// <summary>Gets a value indicating whether a point is inside a net, past the goal line.</summary>
    /// <param name="p">The point.</param>
    /// <returns>True inside either net.</returns>
    public static bool InNet(Vector3 p)
    {
        float ax = MathF.Abs(p.X);
        return ax > RinkSpec.GoalLineX && ax < RinkSpec.GoalLineX + RinkSpec.NetDepth + 0.1f && MathF.Abs(p.Z) < RinkSpec.NetWidth / 2 + 0.1f;
    }

    /// <summary>Keeps a carried puck inside the boards and their rounded corners.</summary>
    /// <param name="p">The point on the ice.</param>
    /// <returns>The point, moved inside if it was beyond the boards.</returns>
    public static Vector3 InsideBoards(Vector3 p)
    {
        const float margin = 0.08f;
        float hx = RinkSpec.HalfLength - margin, hz = RinkSpec.HalfWidth - margin, r = RinkSpec.CornerRadius - margin;
        float x = Math.Clamp(p.X, -hx, hx), z = Math.Clamp(p.Z, -hz, hz);
        float cx = hx - r, cz = hz - r;
        if (MathF.Abs(x) > cx && MathF.Abs(z) > cz)
        {
            var corner = new Vector2(MathF.Sign(x) * cx, MathF.Sign(z) * cz);
            var off = new Vector2(x, z) - corner;
            if (off.Length() > r) { off = Vector2.Normalize(off) * r; x = corner.X + off.X; z = corner.Y + off.Y; }
        }
        return new Vector3(x, p.Y, z);
    }

    /// <summary>A normally distributed number with mean 0 and spread 1.</summary>
    /// <param name="rng">Randomness.</param>
    /// <returns>The number.</returns>
    public static float Gauss(Random rng)
    {
        float u = 1f - rng.NextSingle(), v = rng.NextSingle();
        return MathF.Sqrt(-2f * MathF.Log(u)) * MathF.Cos(MathF.Tau * v);
    }

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }

    static float Deg(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    static float Angle(Vector2 from, Vector2 to)
    {
        return MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to));
    }

    static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
