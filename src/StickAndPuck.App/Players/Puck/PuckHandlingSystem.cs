using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Dev;

namespace StickAndPuck.App.Players;

/// <summary>Possession and the stick (see <see cref="PuckHandling"/> for the numbers).
/// <list type="bullet">
/// <item>Taking the puck: a skater whose blade a loose, live puck passes close to takes it, if the
/// puck is low and slow enough for their puck control; the nearest blade wins, so an opponent in
/// the lane intercepts. A puck inside a net, or held by a goalie, is left alone.</item>
/// <item>Carrying: the puck rides the blade, settling back onto it as the skater turns, tighter
/// with better puck control, and never leaves the rink.</item>
/// <item>Passing: pass sends the puck along the ice to the teammate nearest the way the stick
/// points, leading a moving receiver by smart passes. A human's control follows the pass to the
/// receiver straight away, unless <see cref="Settings.Switching"/> is Manual. A human's pass
/// with nobody in the cone goes to the nearest open teammate (<see cref="PuckHandling.PickOpen"/>),
/// and is recorded (<see cref="LastHumanPass"/>) so the AI skates the receiver to meet it.</item>
/// <item>Shooting: a tap of shoot is a wrist shot, a hold past <see cref="PuckHandling.SlapAfter"/> a
/// slap shot that grows with the wind-up (it fires by itself at full wind-up plus a moment).
/// The hold also sets the height: a tap stays low, a full wind-up goes high (<see cref="ShotHeight"/>).
/// The puck leaves the blade <see cref="PuckHandling.StrikeDelay"/> into the shoot clip.</item>
/// <item>Deking: deke while a defender closes in front sidesteps with the puck; well timed
/// it usually beats them (they bite and lunge at nothing), mistimed it usually loses the puck
/// (see <see cref="DekeModel"/>). With <see cref="Settings.AutoDeke"/> on, a human carrier dekes
/// by themselves when a defender reaches the sweet spot, at worse odds.</item>
/// <item>One-timers: shoot pressed while a teammate's pass is on its way arms a one-timer;
/// when the puck reaches the blade it is struck at once, without being stopped and carried.
/// <see cref="Settings.OneTimers"/> off turns this off: the pass is taken, then shot.</item>
/// </list>
/// Publishes <see cref="PassMade"/> and <see cref="ShotTaken"/> (every shot,
/// on goal or not) with the passer or shooter, the puck and its speed as Strength.</summary>
internal sealed partial class PuckHandlingSystem : ISystem
{
    /// <summary>The shoot clip's length: 46 frames at 24 per second.</summary>
    const float ShootSeconds = 46 / 24f;
    const float PassSeconds = 0.3f;
    const float MaxHold = PuckHandling.FullCharge + 0.4f;

    /// <summary>Seconds after a pass that a teammate can still arm a one-timer on it.</summary>
    public const float OneTimerWindow = 1.6f;

    /// <summary>The dice seed without a match seed.</summary>
    internal const int Fallback = 7;


    readonly Random rng;
    readonly HashSet<int> shootHeld = [];
    readonly Dictionary<int, float> charge = [];
    Maybe<(int Player, ShotKind Kind, float Speed, float Held)> pendingShot;
    int releasedBy = -1;
    float releasedAt = float.NegativeInfinity;
    Vector3 lastPuck;
    Maybe<(Side Side, int Passer, float At)> lastPass;
    Maybe<(Side Side, float At)> lastShot;
    readonly Dictionary<int, float> armed = [];
    readonly Dictionary<int, (float At, Vector2 Side, float Speed)> deking = [];
    readonly Dictionary<int, float> dekedAt = [];
    readonly HashSet<int> autoTried = [];
    readonly HashSet<int> dekeHeld = [];

    public PuckHandlingSystem()
        : this(Seeds.Dice(Seeds.Environment, "puckhandling", Fallback))
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
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
            Buttons(world, dt, Maybe<Player>.None);   // a goalie's puck is the goalie system's
            lastPuck = pk.Position;
            return;
        }

        var carrier = pk.Carrier >= 0 ? world.Players[pk.Carrier] : Maybe<Player>.None;
        if (!carrier.HasValue) TakeLoose(world);
        carrier = pk.Carrier >= 0 ? world.Players[pk.Carrier] : Maybe<Player>.None;
        if (carrier.HasValue || !PassOpen(world)) { lastPass = Maybe<(Side Side, int Passer, float At)>.None; armed.Clear(); }

        Buttons(world, dt, carrier);
        if (pk.Carrier >= 0) Deke(world, world.Players[pk.Carrier]);
        Strike(world);
        Sidestep(world, dt);
        if (pk.Carrier >= 0) Carry(world.Players[pk.Carrier], pk, dt, DekePull(world, pk.Carrier));
        lastPuck = pk.Position;
    }

    /// <summary>Reads shoot and pass for every skater: winds up, fires, passes, and ends finished actions.</summary>
    void Buttons(World world, float dt, Maybe<Player> carrier)
    {
        foreach (var p in world.Players)
        {
            if (p.IsGoalie) continue;
            bool shoot = p.Command.Shoot, pressed = shoot && !shootHeld.Contains(p.Id), released = !shoot && shootHeld.Contains(p.Id);
            if (shoot) shootHeld.Add(p.Id); else shootHeld.Remove(p.Id);
            EndAction(p);

            if (p != carrier || pendingShot.HasValue)
            {
                if (pressed && CanArm(world, p)) armed[p.Id] = world.Time;
                if (armed.ContainsKey(p.Id))
                {
                    if (shoot) charge[p.Id] = charge.GetValueOrDefault(p.Id) + dt;   // the wind-up while the pass comes
                    continue;
                }
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
                speed *= Lines.LinesTuning.ShotFactor(p.Energy);   // tired skaters shoot softer
                pendingShot = Maybe.Some((p.Id, kind, speed, held));
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
        bool human = passer.Slot >= 0;
        var receiver = PuckHandling.PickReceiver(world, passer);
        if (!receiver.HasValue && human) receiver = PuckHandling.PickOpen(world, passer);
        int smart = passer.Data.Traits.HockeyIq;
        Vector3 velocity;
        if (receiver.TryGet(out var lead))
        {
            var (target, speed) = PuckHandling.Lead(from, lead, smart);
            velocity = PuckHandling.PassVelocity(from, target, speed, smart, rng);
        }
        else
        {
            var dir = PuckHandling.Intent(passer);
            velocity = PuckHandling.PassVelocity(from, from + new Vector3(dir.X, 0, dir.Y), 12f, smart, rng);
        }

        Release(world, passer, velocity);
        passer.Begin(PlayerAction.Pass);
        lastPass = Maybe.Some((passer.Side, passer.Id, world.Time));
        armed.Clear();
        world.Events.Publish(new PassMade(world.Time, passer.Side, passer.Id, from, velocity.Length()));
        if (human && receiver.TryGet(out var mate)) world.LastHumanPass = new HumanPass(passer.Id, mate.Id, passer.Command.Move, world.Time);

        if (receiver.TryGet(out var taker) && passer.Slot >= 0 && world.Settings.Switching != ControlSwitching.Manual)
        {
            taker.Slot = passer.Slot;
            passer.Slot = -1;
            passer.Command = default;   // glide on rather than keep the human's last stick
        }
    }

    /// <summary>Gets a human's last pass to a teammate in this match, or None before the first.</summary>
    internal static Maybe<HumanPass> LastHumanPass(World world)
    {
        return world.LastHumanPass;
    }

    /// <summary>Fires a wound-up shot once the clip reaches the sweep.</summary>
    void Strike(World world)
    {
        if (!pendingShot.TryGet(out var shot)) return;
        var p = world.Players[shot.Player];
        if (world.Puck.Carrier != p.Id || p.Action != PlayerAction.Shoot) { pendingShot = Maybe<(int Player, ShotKind Kind, float Speed, float Held)>.None; return; }
        if (p.ActionTime < PuckHandling.StrikeDelay) return;
        pendingShot = Maybe<(int Player, ShotKind Kind, float Speed, float Held)>.None;
        var from = world.Puck.Position;
        var velocity = PuckHandling.ShotVelocity(world, p, from, shot.Kind, shot.Speed, rng, shot.Held);
        Release(world, p, velocity);
        lastShot = (p.Side, world.Time);
        world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
    }

    /// <summary>An armed one-timer meets a pass this much harder than a skater could stop.</summary>
    const float OneTimerCatch = 1.6f;

    /// <summary>Gets a value indicating whether a teammate's pass is still on its way, loose and live, inside the one-timer window.</summary>
    bool PassOpen(World world)
    {
        return lastPass.TryGet(out var pass) && world.Puck.Live && world.Puck.Carrier < 0 && world.Time - pass.At <= OneTimerWindow;
    }

    /// <summary>Gets a value indicating whether a press of shoot arms a one-timer: switched on, a teammate's pass on its way, on their feet and free.</summary>
    bool CanArm(World world, Player p)
    {
        return world.Settings.OneTimers && PassOpen(world) && lastPass.TryGet(out var pass) && pass.Side == p.Side && pass.Passer != p.Id
            && p.OnIce && p.Action == PlayerAction.None;
    }

    /// <summary>Strikes a pass on arrival: no stop, the shot leaves the blade this step, as hard and as high as the wind-up allows.</summary>
    void OneTimer(World world, Player p)
    {
        float held = charge.GetValueOrDefault(p.Id);
        charge.Remove(p.Id);
        armed.Clear();
        lastPass = Maybe<(Side Side, int Passer, float At)>.None;
        var pk = world.Puck;
        var from = pk.Position with { Y = 0 };
        // a one-timer borrows the pass's pace: struck like a slap shot wound up at least half way
        float speed = PuckHandling.SlapSpeed(p.Data.Traits, Math.Max(held, OneTimerPower)) * Lines.LinesTuning.ShotFactor(p.Energy);
        var velocity = PuckHandling.ShotVelocity(world, p, from, ShotKind.Slap, speed, rng, held);
        pk.Position = from;
        Release(world, p, velocity);
        p.Begin(PlayerAction.Shoot);
        p.ActionTime = PuckHandling.StrikeDelay;   // the clip at the sweep, where the puck leaves
        lastShot = (p.Side, world.Time);
        world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
    }

    /// <summary>The wind-up a one-timer is struck with at least, in seconds of hold.</summary>
    const float OneTimerPower = 0.65f;

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
        var nearest = Maybe<Player>.None;
        float bestDistance = float.MaxValue;
        bool block = false;
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            if (p.Id == releasedBy && world.Time - releasedAt < PuckHandling.Repick) continue;
            var traits = p.Data.Traits;
            float relative = (pk.Velocity - p.Velocity).Length();
            float d = SegmentDistance(lastPuck, pk.Position, PuckHandling.Blade(p));
            bool oneTimer = armed.ContainsKey(p.Id);
            bool stick = d <= PuckHandling.Reach(traits) && relative <= PuckHandling.CatchSpeed(traits) * (oneTimer ? OneTimerCatch : 1f);
            // a soft puck into the skates is kicked up onto the stick
            float body = SegmentDistance(lastPuck, pk.Position, p.Position) - PuckHandling.SkateReach;
            bool skates = !oneTimer && body <= 0 && relative <= PuckHandling.CatchSpeed(traits) * 0.6f;
            if (skates && !stick) d = Math.Max(0, body + PuckHandling.Reach(traits));
            if (!(stick || skates) || d >= bestDistance) continue;
            nearest = p;
            bestDistance = d;
            block = Blocks(world, p, relative);
        }
        if (!nearest.TryGet(out var best)) return;
        if (block)
        {
            Block(pk, best);
            return;
        }
        if (armed.ContainsKey(best.Id)) { OneTimer(world, best); return; }
        pk.Carrier = best.Id;
        pk.LastTouch = best.Id;
        pk.Position = pk.Position with { Y = 0 };
    }

    /// <summary>Seconds after a shot leaves the blade that an opponent who meets it blocks it rather than takes it.</summary>
    const float ShotLive = 0.4f;

    /// <summary>A fresh shot meeting an opponent's stick or skates faster than this, in metres per second, is blocked, not caught.</summary>
    const float BlockSpeed = 12f;

    /// <summary>Whether a skater meeting the puck blocks a shot rather than takes it: an opponent's fresh shot, coming harder than a stick can stop.</summary>
    bool Blocks(World world, Player p, float relative)
    {
        return lastShot.TryGet(out var shot) && shot.Side != p.Side && world.Time - shot.At < ShotLive && relative > BlockSpeed;
    }

    /// <summary>A blocked shot: the puck comes off the stick or the shin pads slower, knocked off to one side, loose.</summary>
    void Block(Puck pk, Player p)
    {
        lastShot = Maybe<(Side Side, float At)>.None;
        float angle = (0.5f + (rng.NextSingle() * 1.2f)) * (rng.Next(2) == 0 ? -1 : 1);
        var flat = new Vector2(pk.Velocity.X, pk.Velocity.Z);
        float keep = flat.Length() * (0.2f + (0.25f * rng.NextSingle()));
        var dir = flat.Length() > 1e-4f ? Vector2.Normalize(flat) : Vector2.UnitX;
        var turned = new Vector2((dir.X * SimMath.Cos(angle)) - (dir.Y * SimMath.Sin(angle)), (dir.X * SimMath.Sin(angle)) + (dir.Y * SimMath.Cos(angle)));
        pk.Velocity = new Vector3(turned.X * keep, 0, turned.Y * keep);
        pk.Position = pk.Position with { Y = 0 };
        pk.LastTouch = p.Id;
    }

    /// <summary>Keeps the puck on the carrier's blade: it moves with the skater and settles onto the blade (pulled across the body during a deke).</summary>
    static void Carry(Player carrier, Puck pk, float dt, Vector3 pull)
    {
        var blade = PuckHandling.Blade(carrier) + pull;
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
