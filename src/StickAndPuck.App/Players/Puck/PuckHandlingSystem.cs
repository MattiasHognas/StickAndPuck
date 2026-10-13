using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

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
/// receiver straight away, unless <see cref="Settings.Switching"/> is Manual. W96: a human's pass
/// with nobody in the cone goes to the nearest open teammate (<see cref="PuckHandling.PickOpen"/>),
/// and is recorded (<see cref="LastHumanPass"/>) so the AI skates the receiver to meet it.</item>
/// <item>Shooting: a tap of shoot is a wrist shot, a hold past <see cref="PuckHandling.SlapAfter"/> a
/// slap shot that grows with the wind-up (it fires by itself at full wind-up plus a moment).
/// The hold also sets the height: a tap stays low, a full wind-up goes high (<see cref="ShotHeight"/>).
/// The puck leaves the blade <see cref="PuckHandling.StrikeDelay"/> into the shoot clip.</item>
/// <item>Deking (W32): deke while a defender closes in front sidesteps with the puck; well timed
/// it usually beats them (they bite and lunge at nothing), mistimed it usually loses the puck
/// (see <see cref="DekeModel"/>). With <see cref="Settings.AutoDeke"/> on, a human carrier dekes
/// by themselves when a defender reaches the sweet spot, at worse odds.</item>
/// <item>One-timers (W32): shoot pressed while a teammate's pass is on its way arms a one-timer;
/// when the puck reaches the blade it is struck at once, without being stopped and carried.
/// <see cref="Settings.OneTimers"/> off turns this off: the pass is taken, then shot.</item>
/// </list>
/// Publishes <see cref="PassMade"/> and <see cref="ShotTaken"/> (every shot,
/// on goal or not) with the passer or shooter, the puck and its speed as Strength.
/// </summary>
internal sealed class PuckHandlingSystem : ISystem
{
    /// <summary>The shoot clip's length: 46 frames at 24 per second.</summary>
    const float ShootSeconds = 46 / 24f;
    const float PassSeconds = 0.3f;
    const float MaxHold = PuckHandling.FullCharge + 0.4f;

    /// <summary>Seconds after a pass that a teammate can still arm a one-timer on it.</summary>
    public const float OneTimerWindow = 1.6f;

    /// <summary>The dice seed without a match seed (W67).</summary>
    internal const int Fallback = 7;

    static readonly ConditionalWeakTable<World, StrongBox<HumanPass?>> HumanPasses = [];

    readonly Random rng;
    readonly HashSet<int> shootHeld = [];
    readonly Dictionary<int, float> charge = [];
    (int Player, ShotKind Kind, float Speed, float Held)? pendingShot;
    int releasedBy = -1;
    float releasedAt = float.NegativeInfinity;
    Vector3 lastPuck;
    (Side Side, int Passer, float At)? lastPass;
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
        if (carrier != null || !PassOpen(world)) { lastPass = null; armed.Clear(); }

        Buttons(world, dt, carrier);
        if (pk.Carrier >= 0) Deke(world, world.Players[pk.Carrier]);
        Strike(world);
        Sidestep(world, dt);
        if (pk.Carrier >= 0) Carry(world.Players[pk.Carrier], pk, dt, DekePull(world, pk.Carrier));
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
        bool human = passer.Slot >= 0;
        var receiver = PuckHandling.PickReceiver(world, passer) ?? (human ? PuckHandling.PickOpen(world, passer) : null);
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
        lastPass = (passer.Side, passer.Id, world.Time);
        armed.Clear();
        world.Events.Publish(new PassMade(world.Time, passer.Side, passer.Id, from, velocity.Length()));
        if (human && receiver != null) HumanPasses.AddOrUpdate(world, new StrongBox<HumanPass?>(new HumanPass(passer.Id, receiver.Id, passer.Command.Move, world.Time)));

        if (receiver != null && passer.Slot >= 0 && world.Settings.Switching != ControlSwitching.Manual)
        {
            receiver.Slot = passer.Slot;
            passer.Slot = -1;
            passer.Command = default;   // glide on rather than keep the human's last stick
        }
    }

    /// <summary>Gets a human's last pass to a teammate in this match (W96), or null before the first.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The pass.</returns>
    internal static HumanPass? LastHumanPass(World world)
    {
        return HumanPasses.TryGetValue(world, out var box) ? box.Value : null;
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
        world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
    }

    /// <summary>An armed one-timer meets a pass this much harder than a skater could stop.</summary>
    const float OneTimerCatch = 1.6f;

    /// <summary>Gets a value indicating whether a teammate's pass is still on its way, loose and live, inside the one-timer window.</summary>
    bool PassOpen(World world)
    {
        return lastPass is { } pass && world.Puck.Live && world.Puck.Carrier < 0 && world.Time - pass.At <= OneTimerWindow;
    }

    /// <summary>Gets a value indicating whether a press of shoot arms a one-timer: switched on, a teammate's pass on its way, on their feet and free.</summary>
    bool CanArm(World world, Player p)
    {
        return world.Settings.OneTimers && PassOpen(world) && lastPass is { } pass && pass.Side == p.Side && pass.Passer != p.Id
            && p.OnIce && p.Action == PlayerAction.None;
    }

    /// <summary>Strikes a pass on arrival: no stop, the shot leaves the blade this step, as hard and as high as the wind-up allows.</summary>
    void OneTimer(World world, Player p)
    {
        float held = charge.GetValueOrDefault(p.Id);
        charge.Remove(p.Id);
        armed.Clear();
        lastPass = null;
        var pk = world.Puck;
        var from = pk.Position with { Y = 0 };
        // a one-timer borrows the pass's pace: struck like a slap shot wound up at least half way
        float speed = PuckHandling.SlapSpeed(p.Data.Traits, Math.Max(held, OneTimerPower)) * Lines.LinesTuning.ShotFactor(p.Energy);
        var velocity = PuckHandling.ShotVelocity(world, p, from, ShotKind.Slap, speed, rng, held);
        pk.Position = from;
        Release(world, p, velocity);
        p.Begin(PlayerAction.Shoot);
        p.ActionTime = PuckHandling.StrikeDelay;   // the clip at the sweep, where the puck leaves
        world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
    }

    /// <summary>The wind-up a one-timer is struck with at least, in seconds of hold.</summary>
    const float OneTimerPower = 0.65f;

    /// <summary>Starts a deke when the carrier presses deke, or by itself for a human carrier when auto-deke is on and a defender reaches the sweet spot.</summary>
    void Deke(World world, Player carrier)
    {
        bool down = carrier.Command.Deke, pressed = down && !dekeHeld.Contains(carrier.Id);
        if (down) dekeHeld.Add(carrier.Id); else dekeHeld.Remove(carrier.Id);
        if (carrier.Action != PlayerAction.None || pendingShot != null || deking.ContainsKey(carrier.Id)) return;
        if (world.Time - dekedAt.GetValueOrDefault(carrier.Id, float.NegativeInfinity) < DekeModel.Cooldown) return;

        var threat = DekeModel.Threat(world, carrier);
        if (threat == null) autoTried.Remove(carrier.Id);   // the next defender to come gets a fresh auto-deke
        int pc = carrier.Data.Traits.PuckControl;
        float chance;
        if (pressed) chance = threat is { } t ? DekeModel.Chance(DekeModel.Timing(t.Distance), pc) : 1f;
        else if (world.Settings.AutoDeke && carrier.Slot >= 0 && threat is { } a && a.Distance <= DekeModel.SweetFar && autoTried.Add(carrier.Id))
            chance = DekeModel.AutoChance(pc);
        else return;

        var defender = threat?.Defender;
        bool beaten = rng.NextSingle() < chance;
        dekedAt[carrier.Id] = world.Time;
        deking[carrier.Id] = (world.Time, DekeModel.StepSide(carrier, defender), DekeModel.StepSpeed(pc, beaten || defender == null));
        if (defender == null) return;   // a shimmy in open ice: nothing to beat, nothing to lose
        if (beaten) Bite(carrier, defender);
        else Fumble(world, carrier);
    }

    /// <summary>The defender buys the fake: they lunge where the puck was, past the carrier, and are a moment recovering.</summary>
    static void Bite(Player carrier, Player defender)
    {
        if (defender.Action is PlayerAction.None or PlayerAction.Check)
        {
            // a check already past its contact window: the checking system lets them recover without hitting anyone
            defender.Begin(PlayerAction.Check);
            defender.ActionTime = CheckingModel.Window + 0.01f;
        }
        var fake = -DekeModel.StepSide(carrier, defender);
        defender.Velocity = defender.Velocity * 0.5f + new Vector3(fake.X, 0, fake.Y) * 2f;
    }

    /// <summary>A failed deke: the puck squirts off the blade ahead, free for whoever gets there first (the defender, usually).</summary>
    void Fumble(World world, Player carrier)
    {
        var travel = DekeModel.Travel(carrier);
        var side = new Vector2(-travel.Y, travel.X) * (rng.NextSingle() * 2f - 1f) * 1.5f;
        var push = travel * 2f + side;
        Release(world, carrier, carrier.Velocity * 0.6f + new Vector3(push.X, 0, push.Y));
    }

    /// <summary>Moves dekers sideways for the deke's length, inside the boards.</summary>
    void Sidestep(World world, float dt)
    {
        foreach (var (id, d) in deking.ToList())
        {
            var p = world.Players[id];
            if (world.Time - d.At >= DekeModel.Duration || p.Action is PlayerAction.Fallen or PlayerAction.Fight) { deking.Remove(id); continue; }
            var step = d.Side * d.Speed * dt;
            p.Position = PuckHandling.InsideBoards(p.Position + new Vector3(step.X, 0, step.Y));
        }
    }

    /// <summary>Where the puck is pulled during a deke: across the body and back again.</summary>
    Vector3 DekePull(World world, int carrier)
    {
        if (!deking.TryGetValue(carrier, out var d)) return Vector3.Zero;
        float k = SimMath.Sin(MathF.PI * Math.Clamp((world.Time - d.At) / DekeModel.Duration, 0f, 1f)) * 0.6f;
        return new Vector3(d.Side.X, 0, d.Side.Y) * k;
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
            bool oneTimer = armed.ContainsKey(p.Id);
            bool stick = d <= PuckHandling.Reach(traits) && relative <= PuckHandling.CatchSpeed(traits) * (oneTimer ? OneTimerCatch : 1f);
            // a soft puck into the skates is kicked up onto the stick
            float body = SegmentDistance(lastPuck, pk.Position, p.Position) - PuckHandling.SkateReach;
            bool skates = !oneTimer && body <= 0 && relative <= PuckHandling.CatchSpeed(traits) * 0.6f;
            if (skates && !stick) d = Math.Max(0, body + PuckHandling.Reach(traits));
            if (!(stick || skates) || d >= bestDistance) continue;
            best = p;
            bestDistance = d;
        }
        if (best == null) return;
        if (armed.ContainsKey(best.Id)) { OneTimer(world, best); return; }
        pk.Carrier = best.Id;
        pk.LastTouch = best.Id;
        pk.Position = pk.Position with { Y = 0 };
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
