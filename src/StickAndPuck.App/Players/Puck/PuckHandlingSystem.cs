using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck.Dev;

namespace StickAndPuck.App.Players.Puck
{
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
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed partial class PuckHandlingSystem(Random rng) : ISystem
    {
        /// <summary>The shoot clip's length: 46 frames at 24 per second.</summary>
        private const float ShootSeconds = 46 / 24f;
        private const float PassSeconds = 0.3f;
        private const float MaxHold = PuckHandling.FullCharge + 0.4f;

        /// <summary>Seconds after a pass that a teammate can still arm a one-timer on it.</summary>
        public const float OneTimerWindow = 1.6f;

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 7;

        private readonly Random rng = rng;
        private readonly HashSet<int> shootHeld = [];
        private readonly Dictionary<int, float> charge = [];
        private Maybe<(int Player, ShotKind Kind, float Speed, float Held)> pendingShot;
        private int releasedBy = -1;
        private float releasedAt = float.NegativeInfinity;
        private Vector3 lastPuck;
        private Maybe<(Side Side, int Passer, float At)> lastPass;
        private Maybe<(Side Side, float At)> lastShot;
        private readonly Dictionary<int, float> armed = [];
        private readonly Dictionary<int, (float At, Vector2 Side, float Speed)> deking = [];
        private readonly Dictionary<int, float> dekedAt = [];
        private readonly HashSet<int> autoTried = [];
        private readonly HashSet<int> dekeHeld = [];

        public PuckHandlingSystem()
            : this(Seeds.Dice(Seeds.Environment, "puckhandling", Fallback)) { }

        public void Start(World world)
        {
            lastPuck = world.Puck.Position;
        }

        public void Update(World world, float dt)
        {
            HandlingDemo.Update(world);
            ShotDemo.Update(world);
            OneTimerArming.Share(world, armed);
            Core.Puck pk = world.Puck;
            if (
                pk.Carrier >= 0
                && (pk.Carrier >= world.Players.Count || world.Players[pk.Carrier].IsGoalie)
            )
            {
                Buttons(world, dt, Maybe.None<Player>()); // a goalie's puck is the goalie system's
                lastPuck = pk.Position;
                return;
            }

            Maybe<Player> carrier =
                pk.Carrier >= 0 ? world.Players[pk.Carrier] : Maybe.None<Player>();
            if (!carrier.HasValue)
            {
                TakeLoose(world);
            }

            carrier = pk.Carrier >= 0 ? world.Players[pk.Carrier] : Maybe.None<Player>();
            if (carrier.HasValue || !PassOpen(world))
            {
                lastPass = Maybe.None<(Side Side, int Passer, float At)>();
                armed.Clear();
            }

            Buttons(world, dt, carrier);
            if (pk.Carrier >= 0)
            {
                Deke(world, world.Players[pk.Carrier]);
            }

            Strike(world);
            Sidestep(world, dt);
            if (pk.Carrier >= 0)
            {
                Carry(world.Players[pk.Carrier], pk, dt, DekePull(world, pk.Carrier));
            }

            lastPuck = pk.Position;
        }

        /// <summary>Reads shoot and pass for every skater: winds up, fires, passes, and ends finished actions.</summary>
        private void Buttons(World world, float dt, Maybe<Player> carrier)
        {
            foreach (Player p in world.Players)
            {
                if (p.IsGoalie)
                {
                    continue;
                }

                HandlePlayerButtons(world, p, dt, carrier);
            }
        }

        private void HandlePlayerButtons(World world, Player p, float dt, Maybe<Player> carrier)
        {
            bool shoot = p.Command.Shoot,
                pressed = shoot && !shootHeld.Contains(p.Id),
                released = !shoot && shootHeld.Contains(p.Id);
            UpdateShootHeld(p.Id, shoot);
            EndAction(p);

            if (ShouldHoldOrArm(p, carrier))
            {
                ArmOrSwing(world, p, pressed, shoot, dt);
                return;
            }

            if (p.Command.Pass && p.Action is PlayerAction.None or PlayerAction.Pass)
            {
                _ = charge.Remove(p.Id);
                Pass(world, p);
                return;
            }

            if (shoot)
            {
                charge[p.Id] = charge.GetValueOrDefault(p.Id) + dt;
            }

            FinishShot(p, released);
        }

        private void UpdateShootHeld(int playerId, bool shoot)
        {
            _ = shoot ? shootHeld.Add(playerId) : shootHeld.Remove(playerId);
        }

        private bool ShouldHoldOrArm(Player p, Maybe<Player> carrier)
        {
            return p != carrier || pendingShot.HasValue;
        }

        private void ArmOrSwing(World world, Player p, bool pressed, bool shoot, float dt)
        {
            if (pressed && CanArm(world, p))
            {
                armed[p.Id] = world.Time;
            }

            if (armed.ContainsKey(p.Id))
            {
                if (shoot)
                {
                    charge[p.Id] = charge.GetValueOrDefault(p.Id) + dt; // the wind-up while the pass comes
                }

                return;
            }

            _ = charge.Remove(p.Id);
            if (pressed && p.Action == PlayerAction.None)
            {
                p.Begin(PlayerAction.Shoot); // a swing at nothing
            }
        }

        private void FinishShot(Player p, bool released)
        {
            float held = charge.GetValueOrDefault(p.Id);
            if ((released && held > 0) || held >= MaxHold)
            {
                _ = charge.Remove(p.Id);
                ShotKind kind = PuckHandling.KindOf(held);
                float speed =
                    kind == ShotKind.Slap
                        ? PuckHandling.SlapSpeed(p.Data.Traits, held)
                        : PuckHandling.WristSpeed(p.Data.Traits);
                speed *= Lines.LinesTuning.ShotFactor(p.Energy); // tired skaters shoot softer
                pendingShot = Maybe.Some((p.Id, kind, speed, held));
                p.Begin(PlayerAction.Shoot);
            }
        }

        private static void EndAction(Player p)
        {
            if (
                (p.Action == PlayerAction.Shoot && p.ActionTime >= ShootSeconds)
                || (p.Action == PlayerAction.Pass && p.ActionTime >= PassSeconds)
            )
            {
                p.Begin(PlayerAction.None);
            }
        }

        /// <summary>Sends the puck to the teammate the stick points at, or into space, and moves the human's control with it.</summary>
        private void Pass(World world, Player passer)
        {
            Core.Puck pk = world.Puck;
            Vector3 from = pk.Position;
            bool human = passer.Slot >= 0;
            Maybe<Player> receiver = PuckHandling.PickReceiver(world, passer);
            if (!receiver.HasValue && human)
            {
                receiver = PuckHandling.PickOpen(world, passer);
            }

            int smart = Math.Min(
                100,
                passer.Data.Traits.HockeyIq + CoachTalk.PassIq(world.Setup, passer.Side)
            ); // a calm pre-game talk steadies the passes
            Vector3 velocity;
            if (receiver.TryGet(out Player lead))
            {
                (Vector3 target, float speed) = PuckHandling.Lead(from, lead, smart);
                velocity = PuckHandling.PassVelocity(from, target, speed, smart, rng);
            }
            else
            {
                Vector2 dir = PuckHandling.Intent(passer);
                velocity = PuckHandling.PassVelocity(
                    from,
                    from + new Vector3(dir.X, 0, dir.Y),
                    12f,
                    smart,
                    rng
                );
            }

            Release(world, passer, velocity);
            passer.Begin(PlayerAction.Pass);
            lastPass = Maybe.Some((passer.Side, passer.Id, world.Time));
            armed.Clear();
            world.Events.Publish(
                new PassMade(world.Time, passer.Side, passer.Id, from, velocity.Length())
            );
            if (human && receiver.TryGet(out Player mate))
            {
                world.LastHumanPass = new HumanPass(
                    passer.Id,
                    mate.Id,
                    passer.Command.Move,
                    world.Time
                );
            }

            if (
                receiver.TryGet(out Player taker)
                && passer.Slot >= 0
                && world.Settings.Switching != ControlSwitching.Manual
            )
            {
                taker.Slot = passer.Slot;
                passer.Slot = -1;
                passer.Command = default; // glide on rather than keep the human's last stick
            }
        }

        /// <summary>Gets a human's last pass to a teammate in this match, or None before the first.</summary>
        internal static Maybe<HumanPass> LastHumanPass(World world)
        {
            return world.LastHumanPass;
        }

        /// <summary>Fires a wound-up shot once the clip reaches the sweep.</summary>
        private void Strike(World world)
        {
            if (!pendingShot.TryGet(out (int Player, ShotKind Kind, float Speed, float Held) shot))
            {
                return;
            }

            Player p = world.Players[shot.Player];
            if (world.Puck.Carrier != p.Id || p.Action != PlayerAction.Shoot)
            {
                pendingShot = Maybe.None<(int Player, ShotKind Kind, float Speed, float Held)>();
                return;
            }
            if (p.ActionTime < PuckHandling.StrikeDelay)
            {
                return;
            }

            pendingShot = Maybe.None<(int Player, ShotKind Kind, float Speed, float Held)>();
            Vector3 from = world.Puck.Position;
            Vector3 velocity = PuckHandling.ShotVelocity(
                world,
                p,
                from,
                shot.Kind,
                shot.Speed,
                rng,
                shot.Held
            );
            Release(world, p, velocity);
            lastShot = (p.Side, world.Time);
            world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
        }

        /// <summary>An armed one-timer meets a pass this much harder than a skater could stop.</summary>
        private const float OneTimerCatch = 1.6f;

        /// <summary>Gets a value indicating whether a teammate's pass is still on its way, loose and live, inside the one-timer window.</summary>
        private bool PassOpen(World world)
        {
            return lastPass.TryGet(out (Side Side, int Passer, float At) pass)
                && world.Puck.Live
                && world.Puck.Carrier < 0
                && world.Time - pass.At <= OneTimerWindow;
        }

        /// <summary>Gets a value indicating whether a press of shoot arms a one-timer: switched on, a teammate's pass on its way, on their feet and free.</summary>
        private bool CanArm(World world, Player p)
        {
            return world.Settings.OneTimers
                && PassOpen(world)
                && lastPass.TryGet(out (Side Side, int Passer, float At) pass)
                && pass.Side == p.Side
                && pass.Passer != p.Id
                && p.OnIce
                && p.Action == PlayerAction.None;
        }

        /// <summary>Strikes a pass on arrival: no stop, the shot leaves the blade this step, as hard and as high as the wind-up allows.</summary>
        private void OneTimer(World world, Player p)
        {
            float held = charge.GetValueOrDefault(p.Id);
            _ = charge.Remove(p.Id);
            armed.Clear();
            lastPass = Maybe.None<(Side Side, int Passer, float At)>();
            Core.Puck pk = world.Puck;
            Vector3 from = pk.Position with { Y = 0 };
            // a one-timer borrows the pass's pace: struck like a slap shot wound up at least half way
            float speed =
                PuckHandling.SlapSpeed(p.Data.Traits, Math.Max(held, OneTimerPower))
                * Lines.LinesTuning.ShotFactor(p.Energy);
            Vector3 velocity = PuckHandling.ShotVelocity(
                world,
                p,
                from,
                ShotKind.Slap,
                speed,
                rng,
                held
            );
            pk.Position = from;
            Release(world, p, velocity);
            p.Begin(PlayerAction.Shoot);
            p.ActionTime = PuckHandling.StrikeDelay; // the clip at the sweep, where the puck leaves
            lastShot = (p.Side, world.Time);
            world.Events.Publish(new ShotTaken(world.Time, p.Side, p.Id, from, velocity.Length()));
        }

        /// <summary>The wind-up a one-timer is struck with at least, in seconds of hold.</summary>
        private const float OneTimerPower = 0.65f;

        private void Release(World world, Player p, Vector3 velocity)
        {
            Core.Puck pk = world.Puck;
            pk.Carrier = -1;
            pk.LastTouch = p.Id;
            pk.Velocity = velocity;
            releasedBy = p.Id;
            releasedAt = world.Time;
        }

        /// <summary>Gives a loose puck to the nearest skater whose blade it passed within reach of this step.</summary>
        private void TakeLoose(World world)
        {
            Core.Puck pk = world.Puck;
            if (!LoosePuckAvailable(pk))
            {
                return;
            }

            if (!TryFindLoosePuckTarget(world, pk, out Player best, out bool block))
            {
                return;
            }

            if (block)
            {
                Block(pk, best);
                return;
            }
            if (armed.ContainsKey(best.Id))
            {
                OneTimer(world, best);
                return;
            }

            pk.Carrier = best.Id;
            pk.LastTouch = best.Id;
            pk.Position = pk.Position with { Y = 0 };
        }

        private static bool LoosePuckAvailable(Core.Puck pk)
        {
            return pk.Live
                && pk.Position.Y <= PuckHandling.StickHeight
                && !PuckHandling.InNet(pk.Position);
        }

        private bool TryFindLoosePuckTarget(
            World world,
            Core.Puck pk,
            out Player best,
            out bool block
        )
        {
            best = default;
            block = false;
            Maybe<Player> nearest = Maybe.None<Player>();
            float bestDistance = float.MaxValue;
            foreach (Player p in world.Players)
            {
                if (
                    !CanTakeLoose(world, p)
                    || !LoosePuckCandidate(world, pk, p, out float d, out bool candidateBlock)
                )
                {
                    continue;
                }

                if (d >= bestDistance)
                {
                    continue;
                }

                nearest = p;
                bestDistance = d;
                block = candidateBlock;
            }

            return nearest.TryGet(out best);
        }

        private bool CanTakeLoose(World world, Player p)
        {
            return p.OnIce
                && !p.IsGoalie
                && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)
                && !(p.Id == releasedBy && world.Time - releasedAt < PuckHandling.Repick);
        }

        private bool LoosePuckCandidate(
            World world,
            Core.Puck pk,
            Player p,
            out float d,
            out bool block
        )
        {
            Traits traits = p.Data.Traits;
            float relative = (pk.Velocity - p.Velocity).Length();
            d = SegmentDistance(lastPuck, pk.Position, PuckHandling.Blade(p));
            bool oneTimer = armed.ContainsKey(p.Id);
            bool stick =
                d <= PuckHandling.Reach(traits)
                && relative <= PuckHandling.CatchSpeed(traits) * (oneTimer ? OneTimerCatch : 1f);

            // a soft puck into the skates is kicked up onto the stick
            float body =
                SegmentDistance(lastPuck, pk.Position, p.Position) - PuckHandling.SkateReach;
            bool skates =
                !oneTimer && body <= 0 && relative <= PuckHandling.CatchSpeed(traits) * 0.6f;
            if (skates && !stick)
            {
                d = Math.Max(0, body + PuckHandling.Reach(traits));
            }

            block = Blocks(world, p, relative);
            return stick || skates;
        }

        /// <summary>Seconds after a shot leaves the blade that an opponent who meets it blocks it rather than takes it.</summary>
        private const float ShotLive = 0.4f;

        /// <summary>A fresh shot meeting an opponent's stick or skates faster than this, in metres per second, is blocked, not caught.</summary>
        private const float BlockSpeed = 12f;

        /// <summary>Whether a skater meeting the puck blocks a shot rather than takes it: an opponent's fresh shot, coming harder than a stick can stop.</summary>
        private bool Blocks(World world, Player p, float relative)
        {
            return lastShot.TryGet(out (Side Side, float At) shot)
                && shot.Side != p.Side
                && world.Time - shot.At < ShotLive
                && relative > BlockSpeed;
        }

        /// <summary>A blocked shot: the puck comes off the stick or the shin pads slower, knocked off to one side, loose.</summary>
        private void Block(Core.Puck pk, Player p)
        {
            lastShot = Maybe.None<(Side Side, float At)>();
            float angle = (0.5f + (rng.NextSingle() * 1.2f)) * (rng.Next(2) == 0 ? -1 : 1);
            Vector2 flat = new(pk.Velocity.X, pk.Velocity.Z);
            float keep = flat.Length() * (0.2f + (0.25f * rng.NextSingle()));
            Vector2 dir = flat.Length() > 1e-4f ? Vector2.Normalize(flat) : Vector2.UnitX;
            Vector2 turned = new(
                (dir.X * SimMath.Cos(angle)) - (dir.Y * SimMath.Sin(angle)),
                (dir.X * SimMath.Sin(angle)) + (dir.Y * SimMath.Cos(angle))
            );
            pk.Velocity = new Vector3(turned.X * keep, 0, turned.Y * keep);
            pk.Position = pk.Position with { Y = 0 };
            pk.LastTouch = p.Id;
        }

        /// <summary>Keeps the puck on the carrier's blade: it moves with the skater and settles onto the blade (pulled across the body during a deke).</summary>
        private static void Carry(Player carrier, Core.Puck pk, float dt, Vector3 pull)
        {
            Vector3 blade = PuckHandling.Blade(carrier) + pull;
            Vector3 pos = pk.Position + (carrier.Velocity * dt);
            float k = Math.Min(1f, PuckHandling.Follow(carrier.Data.Traits) * dt);
            pos += (blade - pos) * k;
            pk.Position = PuckHandling.InsideBoards(pos with { Y = 0 });
            pk.Velocity = carrier.Velocity;
        }

        private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector2 ab = new(b.X - a.X, b.Z - a.Z);
            Vector2 ap = new(p.X - a.X, p.Z - a.Z);
            float len = ab.LengthSquared();
            float t = len > 1e-8f ? Math.Clamp(Vector2.Dot(ap, ab) / len, 0f, 1f) : 0f;
            return (ap - (ab * t)).Length();
        }
    }
}
