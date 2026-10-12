using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>
/// W4b: possession and the stick (see <see cref="PuckHandling"/> for the numbers).
/// <list type="bullet">
/// <item>Taking the puck: a skater whose blade a loose, live puck passes close to takes it, if the
/// puck is low and slow enough for their puck control; the nearest blade wins, so an opponent in
/// the lane intercepts. A puck inside a net, or held by a goalie, is left alone.</item>
/// <item>Carrying: the puck rides the blade, settling back onto it as the skater turns, tighter
/// with better puck control, and never leaves the rink.</item>
/// <item>Passing: pass sends the puck along the ice to the teammate nearest the way the stick
/// points, leading a moving receiver by smart passes. A human's control follows the pass to the
/// receiver straight away, unless <see cref="Settings.Switching"/> is Manual.</item>
/// <item>Shooting: a tap of shoot is a wrist shot, a hold past <see cref="PuckHandling.SlapAfter"/> a
/// slap shot that grows with the wind-up (it fires by itself at full wind-up plus a moment).
/// The hold also sets the height: a tap stays low, a full wind-up goes high (<see cref="ShotHeight"/>).
/// The puck leaves the blade <see cref="PuckHandling.StrikeDelay"/> into the shoot clip.</item>
/// </list>
/// Publishes <see cref="MatchEventKind.Pass"/> and <see cref="MatchEventKind.Shot"/> (every shot,
/// on goal or not) with the passer or shooter, the puck and its speed as Strength.
/// </summary>
internal sealed class PuckHandlingSystem : ISystem
{
    /// <summary>The shoot clip's length: 46 frames at 24 per second.</summary>
    const float ShootSeconds = 46 / 24f;
    const float PassSeconds = 0.3f;
    const float MaxHold = PuckHandling.FullCharge + 0.4f;

    readonly Random rng;
    readonly HashSet<int> shootHeld = [];
    readonly Dictionary<int, float> charge = [];
    (int Player, ShotKind Kind, float Speed, float Held)? pendingShot;
    int releasedBy = -1;
    float releasedAt = float.NegativeInfinity;
    Vector3 lastPuck;

    public PuckHandlingSystem()
#pragma warning disable S2245 // gameplay variation, not security
        : this(new Random())
#pragma warning restore S2245
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for pass and shot error.</param>
    public PuckHandlingSystem(Random rng)
    {
        this.rng = rng;
    }

    public void Start(World world)
    {
        lastPuck = world.Puck.Position;
    }

    public void Update(World world, float dt)
    {
        HandlingDemo.Update(world);
        ShotDemo.Update(world);
        var pk = world.Puck;
        if (pk.Carrier >= 0 && (pk.Carrier >= world.Players.Count || world.Players[pk.Carrier].IsGoalie))
        {
            Buttons(world, dt, null);   // a goalie's puck is W5's
            lastPuck = pk.Position;
            return;
        }

        var carrier = pk.Carrier >= 0 ? world.Players[pk.Carrier] : null;
        if (carrier == null) TakeLoose(world);
        carrier = pk.Carrier >= 0 ? world.Players[pk.Carrier] : null;

        Buttons(world, dt, carrier);
        Strike(world);
        if (pk.Carrier >= 0) Carry(world.Players[pk.Carrier], pk, dt);
        lastPuck = pk.Position;
    }

    /// <summary>Reads shoot and pass for every skater: winds up, fires, passes, and ends finished actions.</summary>
    void Buttons(World world, float dt, Player? carrier)
    {
        foreach (var p in world.Players)
        {
            if (p.IsGoalie) continue;
            bool shoot = p.Command.Shoot, pressed = shoot && !shootHeld.Contains(p.Id), released = !shoot && shootHeld.Contains(p.Id);
            if (shoot) shootHeld.Add(p.Id); else shootHeld.Remove(p.Id);
            EndAction(p);

            if (p != carrier || pendingShot != null)
            {
                charge.Remove(p.Id);
                if (pressed && p.Action == PlayerAction.None) p.Begin(PlayerAction.Shoot);   // a swing at nothing
                continue;
            }

            if (p.Command.Pass && p.Action is PlayerAction.None or PlayerAction.Pass) { charge.Remove(p.Id); Pass(world, p); continue; }
            if (shoot) charge[p.Id] = charge.GetValueOrDefault(p.Id) + dt;
            float held = charge.GetValueOrDefault(p.Id);
            if ((released && held > 0) || held >= MaxHold)
            {
                charge.Remove(p.Id);
                var kind = PuckHandling.KindOf(held);
                float speed = kind == ShotKind.Slap ? PuckHandling.SlapSpeed(p.Data.Traits, held) : PuckHandling.WristSpeed(p.Data.Traits);
                speed *= Lines.LinesTuning.ShotFactor(p.Energy);   // W13: tired skaters shoot softer
                pendingShot = (p.Id, kind, speed, held);
                p.Begin(PlayerAction.Shoot);
            }
        }
    }

    static void EndAction(Player p)
    {
        if ((p.Action == PlayerAction.Shoot && p.ActionTime >= ShootSeconds) || (p.Action == PlayerAction.Pass && p.ActionTime >= PassSeconds))
            p.Begin(PlayerAction.None);
    }

    /// <summary>Sends the puck to the teammate the stick points at, or into space, and moves the human's control with it.</summary>
    void Pass(World world, Player passer)
    {
        var pk = world.Puck;
        var from = pk.Position;
        var receiver = PuckHandling.PickReceiver(world, passer);
        int smart = passer.Data.Traits.HockeyIq;
        Vector3 velocity;
        if (receiver != null)
        {
            var (target, speed) = PuckHandling.Lead(from, receiver, smart);
            velocity = PuckHandling.PassVelocity(from, target, speed, smart, rng);
        }
        else
        {
            var dir = PuckHandling.Intent(passer);
            velocity = PuckHandling.PassVelocity(from, from + new Vector3(dir.X, 0, dir.Y), 12f, smart, rng);
        }

        Release(world, passer, velocity);
        passer.Begin(PlayerAction.Pass);
        world.Events.Publish(new MatchEvent(MatchEventKind.Pass, world.Time, passer.Side, passer.Id, from, velocity.Length()));

        if (receiver != null && passer.Slot >= 0 && world.Settings.Switching != ControlSwitching.Manual)
        {
            receiver.Slot = passer.Slot;
            passer.Slot = -1;
            passer.Command = default;   // glide on rather than keep the human's last stick
        }
    }

    /// <summary>Fires a wound-up shot once the clip reaches the sweep.</summary>
    void Strike(World world)
    {
        if (pendingShot is not { } shot) return;
        var p = world.Players[shot.Player];
        if (world.Puck.Carrier != p.Id || p.Action != PlayerAction.Shoot) { pendingShot = null; return; }
        if (p.ActionTime < PuckHandling.StrikeDelay) return;
        pendingShot = null;
        var from = world.Puck.Position;
        var velocity = PuckHandling.ShotVelocity(world, p, from, shot.Kind, shot.Speed, rng, shot.Held);
        Release(world, p, velocity);
        world.Events.Publish(new MatchEvent(MatchEventKind.Shot, world.Time, p.Side, p.Id, from, velocity.Length()));
    }

    void Release(World world, Player p, Vector3 velocity)
    {
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.LastTouch = p.Id;
        pk.Velocity = velocity;
        releasedBy = p.Id;
        releasedAt = world.Time;
    }

    /// <summary>Gives a loose puck to the nearest skater whose blade it passed within reach of this step.</summary>
    void TakeLoose(World world)
    {
        var pk = world.Puck;
        if (!pk.Live || pk.Position.Y > PuckHandling.StickHeight || PuckHandling.InNet(pk.Position)) return;
        Player? best = null;
        float bestDistance = float.MaxValue;
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            if (p.Id == releasedBy && world.Time - releasedAt < PuckHandling.Repick) continue;
            var traits = p.Data.Traits;
            float relative = (pk.Velocity - p.Velocity).Length();
            float d = SegmentDistance(lastPuck, pk.Position, PuckHandling.Blade(p));
            bool stick = d <= PuckHandling.Reach(traits) && relative <= PuckHandling.CatchSpeed(traits);
            // a soft puck into the skates is kicked up onto the stick
            float body = SegmentDistance(lastPuck, pk.Position, p.Position) - PuckHandling.SkateReach;
            bool skates = body <= 0 && relative <= PuckHandling.CatchSpeed(traits) * 0.6f;
            if (skates && !stick) d = Math.Max(0, body + PuckHandling.Reach(traits));
            if (!(stick || skates) || d >= bestDistance) continue;
            best = p;
            bestDistance = d;
        }
        if (best == null) return;
        pk.Carrier = best.Id;
        pk.LastTouch = best.Id;
        pk.Position = pk.Position with { Y = 0 };
    }

    /// <summary>Keeps the puck on the carrier's blade: it moves with the skater and settles onto the blade.</summary>
    static void Carry(Player carrier, Puck pk, float dt)
    {
        var blade = PuckHandling.Blade(carrier);
        var pos = pk.Position + carrier.Velocity * dt;
        float k = Math.Min(1f, PuckHandling.Follow(carrier.Data.Traits) * dt);
        pos += (blade - pos) * k;
        pk.Position = PuckHandling.InsideBoards(pos with { Y = 0 });
        pk.Velocity = carrier.Velocity;
    }

    static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        var ab = new Vector2(b.X - a.X, b.Z - a.Z);
        var ap = new Vector2(p.X - a.X, p.Z - a.Z);
        float len = ab.LengthSquared();
        float t = len > 1e-8f ? Math.Clamp(Vector2.Dot(ap, ab) / len, 0f, 1f) : 0f;
        return (ap - ab * t).Length();
    }
}
