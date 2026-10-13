using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>
/// W4c: body checks from the checking trait (see <see cref="CheckingModel"/> for the numbers).
/// <list type="bullet">
/// <item>Check (Q, or gamepad B) from a skater without the puck lunges at the opponent in reach
/// (<see cref="CheckingModel.Target"/>), or forward when there is none; the first opponent in reach
/// during the lunge is hit, once: right away, or when the bodies meet if they are closing fast
/// (<see cref="CheckingModel.Waits"/>). A press in the last <see cref="CheckingModel.Buffer"/> seconds of a
/// check starts the next one when it ends.</item>
/// <item>The target is shoved along the hit. A hard enough hit, against their resistance, knocks
/// them down: they drop the puck, slide to a stop and lie for a moment, ignoring their stick.
/// A carrier who stays up keeps the puck.</item>
/// <item>Publishes <see cref="CheckLanded"/> for every hit: Side and PlayerId are the
/// checker's, Position is the target's, Strength the hit's strength (about 1 for an average solid
/// check, <see cref="CheckingModel.HardHit"/> and up is hard).</item>
/// </list>
/// Runs after skating, so a fallen player's skating for this step is replaced by a slide.
/// </summary>
internal sealed class CheckingSystem : ISystem
{
    const float SlideDrag = 5f;   // m/s² a fallen body loses on the ice

    /// <summary>The dice seed without a match seed (W67).</summary>
    internal const int Fallback = 6;

    readonly Random rng;
    readonly HashSet<int> landed = [];
    readonly HashSet<int> buffered = [];
    readonly HashSet<int> waiting = [];
    readonly Dictionary<int, float> downFor = [];
    readonly Dictionary<int, (Vector3 Position, Vector3 Velocity, float Heading)> sliding = [];

    public CheckingSystem()
        : this(Seeds.Dice(Seeds.Environment, "checking", Fallback))
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for knockdowns.</param>
    public CheckingSystem(Random rng)
    {
        this.rng = rng;
    }

    public void Update(World world, float dt)
    {
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie) continue;
            if (p.Action == PlayerAction.Fallen) { Slide(p, dt); continue; }
            bool press = p.Command.Check;
            if (p.Action == PlayerAction.Check)
            {
                if (press && p.ActionTime >= CheckingModel.Recover - CheckingModel.Buffer) buffered.Add(p.Id);
                if (p.ActionTime >= CheckingModel.Recover) { p.Begin(PlayerAction.None); landed.Remove(p.Id); press |= buffered.Remove(p.Id); }
                else
                {
                    if (p.ActionTime <= CheckingModel.Window && !landed.Contains(p.Id)) TryHit(world, p);
                    continue;
                }
            }
            else
            {
                buffered.Remove(p.Id);
            }

            if (press && p.Action == PlayerAction.None && world.Puck.Carrier != p.Id) Begin(world, p);
        }

        foreach (var p in world.Players)
        {
            if (p.Action == PlayerAction.Fallen) sliding[p.Id] = (p.Position, p.Velocity, p.Heading);
        }
    }

    /// <summary>Starts a check: a burst at the opponent in reach (else forward), and a first try at contact.</summary>
    void Begin(World world, Player p)
    {
        p.Begin(PlayerAction.Check);
        landed.Remove(p.Id);
        waiting.Remove(p.Id);
        var dir = PuckHandling.Intent(p);
        if (CheckingModel.Target(world, p) is { } target)
        {
            var off = new Vector2(target.Position.X - p.Position.X, target.Position.Z - p.Position.Z);
            if (off.LengthSquared() > 1e-6f) dir = Vector2.Normalize(off);
            var dv = p.Velocity - target.Velocity;
            if (CheckingModel.Waits(off.Length(), Vector2.Dot(new Vector2(dv.X, dv.Z), dir))) waiting.Add(p.Id);
        }

        p.Velocity += new Vector3(dir.X, 0, dir.Y) * CheckingModel.Lunge;
        TryHit(world, p);
    }

    void TryHit(World world, Player checker)
    {
        if (CheckingModel.Target(world, checker) is not { } target) return;
        var off = new Vector2(target.Position.X - checker.Position.X, target.Position.Z - checker.Position.Z);
        if (waiting.Contains(checker.Id) && off.Length() > CheckingModel.Meet) return;   // coming together: hit when they meet
        landed.Add(checker.Id);

        var dir2 = off.LengthSquared() > 1e-6f ? Vector2.Normalize(off) : PuckHandling.Intent(checker);
        var dir = new Vector3(dir2.X, 0, dir2.Y);
        float closing = Vector3.Dot(checker.Velocity - target.Velocity, dir);
        var pk = world.Puck;
        bool carrying = pk.Carrier == target.Id;
        float strength = CheckingModel.Strength(checker, target, closing, carrying);

        // the target takes the checker's push; the checker gives up some of their speed into the hit
        target.Velocity += dir * CheckingModel.Shove(strength);
        checker.Velocity -= dir * Math.Max(0, Vector3.Dot(checker.Velocity, dir)) * 0.5f;
        world.Events.Publish(new CheckLanded(world.Time, checker.Side, checker.Id, target.Position, strength));

        if (rng.NextSingle() >= CheckingModel.KnockdownChance(strength)) return;
        downFor[target.Id] = CheckingModel.DownTime(strength);
        target.Begin(PlayerAction.Fallen);
        sliding[target.Id] = (target.Position, target.Velocity, target.Heading);
        if (carrying)
        {
            // knocked off the puck: it stays about where it was, drifting on with the play
            var side = new Vector3(-dir.Z, 0, dir.X) * (rng.NextSingle() - 0.5f) * 2f;
            pk.Carrier = -1;
            pk.LastTouch = target.Id;
            pk.Velocity = (target.Velocity + checker.Velocity) * 0.25f + side;
        }
    }

    /// <summary>A fallen body slides to a stop where the hit sent it, whatever its stick says, then gets up.</summary>
    void Slide(Player p, float dt)
    {
        if (sliding.TryGetValue(p.Id, out var s))
        {
            var v = new Vector2(s.Velocity.X, s.Velocity.Z);
            float speed = Math.Max(0, v.Length() - SlideDrag * dt);
            v = speed > 0 ? Vector2.Normalize(v) * speed : Vector2.Zero;
            var (pos, vel) = SkatingModel.Boards(new Vector2(s.Position.X, s.Position.Z) + v * dt, v);
            p.Position = new Vector3(pos.X, s.Position.Y, pos.Y);
            p.Velocity = new Vector3(vel.X, 0, vel.Y);
            p.Heading = s.Heading;
        }

        if (p.ActionTime >= downFor.GetValueOrDefault(p.Id, 1f))
        {
            p.Begin(PlayerAction.None);
            p.Velocity = Vector3.Zero;
            sliding.Remove(p.Id);
            downFor.Remove(p.Id);
        }
    }
}
