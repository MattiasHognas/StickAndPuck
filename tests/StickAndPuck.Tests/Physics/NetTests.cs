using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Physics;

/// <summary>
/// Fires pucks at both nets from behind, from both sides, over the top and from the front, loose
/// and on a carrier's stick. Only the shots through the mouth may score; everything else must
/// bounce off the cage and end up outside it. Formerly SAP_SCENE=nettest.
/// </summary>
public sealed class NetTests : CheckedTest
{
    /// <summary>An empty world: each check places its own puck and players.</summary>
    static World Make() => new WorldBuilder().NoStarters().NotBegun().Build();

    static string End(int end) => end < 0 ? "-x net" : "+x net";

    /// <summary>Runs the physics system alone for some seconds and returns every event it published.</summary>
    static List<MatchEvent> Run(World world, float seconds, PuckPhysics? physics = null, Action<World>? each = null)
    {
        physics ??= new PuckPhysics();
        var events = new List<MatchEvent>();
        int steps = (int)MathF.Round(seconds / Game.Step);
        for (int i = 0; i < steps; i++)
        {
            world.Events.Clear();
            physics.Update(world, Game.Step);
            events.AddRange(world.Events.Current);
            each?.Invoke(world);
            world.Time += Game.Step; world.Tick++;
        }
        return events;
    }

    static void Shoot(World world, Vector3 from, Vector3 velocity)
    {
        world.Puck.Position = from;
        world.Puck.Velocity = velocity;
        world.Puck.Carrier = -1;
        world.Puck.Live = true;
    }

    /// <summary>A point given as depth behind the goal line and sideways offset, at one end.</summary>
    static Vector3 At(int end, float depth, float z, float y = 0) => new(end * (RinkSpec.GoalLineX + depth), y, z);

    static bool Goal(IEnumerable<MatchEvent> ev) => ev.Any(e => e.Kind == MatchEventKind.Goal);

    static bool Outside(Vector3 p) => !PuckMotion.InsideNet(p, 1) && !PuckMotion.InsideNet(p, -1);

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void FromBehind(int end)
    {
        float d = RinkSpec.NetDepth;
        foreach (var (z, vz, what) in new[] { (0f, 0f, "straight"), (0.5f, -4f, "angled"), (-0.7f, 6f, "across") })
        {
            var w = Make();
            Shoot(w, At(end, d + 1.6f, z), new Vector3(-end * 16, 0, vz));
            var ev = Run(w, 1.5f);
            var p = w.Puck.Position;
            Check(!Goal(ev) && PuckMotion.Depth(p, end) >= d && Outside(p),
                $"{End(end)}: {what} shot into the back of the net rebounds, no goal (depth {PuckMotion.Depth(p, end):0.00} m)");
        }

        // a slow dribble into the back stops against it
        var soft = Make();
        Shoot(soft, At(end, d + 0.4f, 0.2f), new Vector3(-end * 2, 0, 0));
        Check(!Goal(Run(soft, 1f)) && PuckMotion.Depth(soft.Puck.Position, end) >= d, $"{End(end)}: a dribble into the back of the net stays out");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void FromTheSides(int end)
    {
        float w2 = RinkSpec.NetWidth / 2;
        for (int s = -1; s <= 1; s += 2)
        {
            foreach (var (depth, vx, what) in new[] { (0.5f, 0f, "square"), (0.2f, end * 4f, "angled toward the back") })
            {
                var w = Make();
                Shoot(w, At(end, depth, s * 3.5f), new Vector3(vx, 0, -s * 15));
                var ev = Run(w, 1.5f);
                var p = w.Puck.Position;
                Check(!Goal(ev) && s * p.Z >= w2 && Outside(p),
                    $"{End(end)}: {what} shot into the {(s < 0 ? "-z" : "+z")} side of the net rebounds, no goal (z {p.Z:0.00})");
            }

            // a sneak from beside the net, cutting in just behind the post
            var cut = Make();
            Shoot(cut, At(end, 0.08f, s * 2f), new Vector3(0, 0, -s * 12));
            var cev = Run(cut, 1.5f);
            Check(!Goal(cev) && Outside(cut.Puck.Position), $"{End(end)}: a puck cutting in behind the {(s < 0 ? "-z" : "+z")} post stays out");
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void OverTheTop(int end)
    {
        // dropped straight onto the roof of the net
        var drop = Make();
        Shoot(drop, At(end, RinkSpec.NetDepth / 2, 0.1f, RinkSpec.NetHeight + 1f), Vector3.Zero);
        var dev = Run(drop, 2.5f);
        Check(!Goal(dev) && Outside(drop.Puck.Position), $"{End(end)}: a puck dropped onto the top of the net never gets in");

        // a lob from the front that comes down on the roof
        var lob = Make();
        Shoot(lob, At(end, -4f, 0.3f), new Vector3(end * 6f, 6.5f, 0));
        bool roof = false;
        var lev = Run(lob, 3f, each: x => roof |= PuckMotion.Depth(x.Puck.Position, end) > 0 && x.Puck.Position.Y >= RinkSpec.NetHeight - 0.01f && !PuckMotion.InsideNet(x.Puck.Position, end));
        Check(!Goal(lev) && Outside(lob.Puck.Position), $"{End(end)}: a lob landing on the roof of the net is no goal (on roof: {roof})");

        // a high shot from behind, falling in from above the back
        var high = Make();
        Shoot(high, At(end, RinkSpec.NetDepth + 2.5f, 0), new Vector3(-end * 7f, 5.5f, 0));
        var hev = Run(high, 3f);
        Check(!Goal(hev) && Outside(high.Puck.Position), $"{End(end)}: a high puck from behind the net coming down on it is no goal");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void FromTheFront(int end)
    {
        var w = Make();
        w.Puck.LastTouch = 5;
        Shoot(w, At(end, -8f, 0.2f), new Vector3(end * 28f, 1.2f, 0));
        var ev = Run(w, 2f);
        var goals = ev.Where(e => e.Kind == MatchEventKind.Goal).ToList();
        Check(goals.Count == 1 && goals[0].PlayerId == 5 && PuckMotion.InsideNet(w.Puck.Position, end),
            $"{End(end)}: a shot through the mouth is one goal and stays in the net ({goals.Count} goal(s))");

        // under the bar, from an angle
        var angle = Make();
        Shoot(angle, At(end, -6f, 4f), new Vector3(end * 24f, 2.5f, -14f));
        Check(Goal(Run(angle, 2f)), $"{End(end)}: an angled shot through the mouth is a goal");

        // over the bar: no goal, and it lands behind the net rather than in it
        var over = Make();
        Shoot(over, At(end, -6f, 0), new Vector3(end * 22f, 7.5f, 0));
        var oev = Run(over, 3f);
        Check(!Goal(oev) && Outside(over.Puck.Position), $"{End(end)}: a shot over the crossbar is no goal and lands outside the net");

        // off the post: no goal
        var post = Make();
        Shoot(post, At(end, -8f, RinkSpec.NetWidth / 2 + 0.02f), new Vector3(end * 30f, 0, 0));
        var pev = Run(post, 1.5f);
        Check(!Goal(pev) && pev.Any(e => e.Kind == MatchEventKind.PuckPost) && Outside(post.Puck.Position), $"{End(end)}: a shot off the post is no goal");
    }

    /// <summary>A carrier's stick moves the puck itself, so physics only sees where it ends up.</summary>
    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void Carried(int end)
    {
        float d = RinkSpec.NetDepth;
        // a skater behind the net pokes the puck through the back mesh and lets go of it inside
        var w = Make();
        var physics = new PuckPhysics();
        Shoot(w, At(end, d + 1f, 0.1f), Vector3.Zero);
        Run(w, 0.1f, physics);
        w.Puck.Carrier = 7;
        float depth = d + 1f;
        Run(w, 1f, physics, each: x =>
        {
            depth = Math.Max(d - 0.5f, depth - 0.05f);
            x.Puck.Position = At(end, depth, 0.1f);
            x.Puck.Velocity = new Vector3(-end * 3f, 0, 0);
        });
        Check(PuckMotion.InsideNet(w.Puck.Position, end), $"{End(end)}: a carried puck can be pushed through the mesh (set-up)");
        w.Puck.Carrier = -1;
        var ev = Run(w, 1.5f, physics);
        Check(!Goal(ev) && PuckMotion.Depth(w.Puck.Position, end) >= d && Outside(w.Puck.Position),
            $"{End(end)}: let go inside from behind it pops back out through the back, no goal (depth {PuckMotion.Depth(w.Puck.Position, end):0.00} m)");

        // the same from the side
        var side = Make();
        var sp = new PuckPhysics();
        Shoot(side, At(end, 0.4f, 2f), Vector3.Zero);
        Run(side, 0.1f, sp);
        side.Puck.Carrier = 7;
        float z = 2f;
        Run(side, 1f, sp, each: x => { z = Math.Max(0.3f, z - 0.05f); x.Puck.Position = At(end, 0.4f, z); });
        side.Puck.Carrier = -1;
        var sev = Run(side, 1.5f, sp);
        Check(!Goal(sev) && side.Puck.Position.Z >= RinkSpec.NetWidth / 2 && Outside(side.Puck.Position),
            $"{End(end)}: let go inside from the side it pops out through the side, no goal");

        // a skater in the crease walks the puck in through the mouth: that is a goal
        var walk = Make();
        var wp = new PuckPhysics();
        Shoot(walk, At(end, -1.5f, 0.2f), Vector3.Zero);
        Run(walk, 0.1f, wp);
        walk.Puck.Carrier = 7;
        walk.Puck.LastTouch = 7;
        float u = -1.5f;
        var wev = Run(walk, 1.5f, wp, each: x => { u = Math.Min(0.4f, u + 0.03f); x.Puck.Position = At(end, u, 0.2f); });
        var g = wev.Where(e => e.Kind == MatchEventKind.Goal).ToList();
        Check(g.Count == 1 && g[0].PlayerId == 7, $"{End(end)}: a puck walked in through the mouth on the stick is a goal ({g.Count})");
    }

    /// <summary>W62: skaters driving at the cage, pushed every step the way skating would move them.</summary>
    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void Skaters(int end)
    {
        var skater = new PlayerData { Position = PlayingPosition.C };

        // skate hard at the net from behind, from both sides and in through the mouth
        (string Name, Vector3 From, Vector3 Velocity)[] runs =
        [
            ("from behind", At(end, RinkSpec.NetDepth + 3f, 0.1f), new Vector3(-end * 9f, 0, 0)),
            ("from the -z side", At(end, 0.5f, -3f), new Vector3(0, 0, 9f)),
            ("from the +z side", At(end, 0.5f, 3f), new Vector3(0, 0, -9f)),
            ("in through the mouth", At(end, -3f, 0.2f), new Vector3(end * 9f, 0, 0)),
            ("diagonally across the back", At(end, RinkSpec.NetDepth + 2f, -2f), new Vector3(-end * 6f, 0, 6f)),
        ];
        foreach (var (name, from, velocity) in runs)
        {
            var w = Make();
            var p = w.AddPlayer(Side.Home, skater, from);
            bool overlap = false;
            Run(w, 2f, each: x =>
            {
                overlap |= NetBody.Overlaps(p.Position, end);
                p.Velocity = velocity;    // keeps pushing into the cage like a held stick
                p.Position += p.Velocity * Game.Step;
            });
            Check(!overlap, $"{End(end)}: a skater driving {name} never gets into or over the cage");
        }

        // a skater sliding along the back keeps its speed along the frame
        var slide = Make();
        var sl = slide.AddPlayer(Side.Home, skater, At(end, RinkSpec.NetDepth + 0.5f, -0.5f));
        Run(slide, 1f, each: x =>
        {
            sl.Velocity = new Vector3(-end * 2f, 0, 3f);
            sl.Position += sl.Velocity * Game.Step;
        });
        Check(sl.Position.Z > RinkSpec.NetWidth / 2 + PuckTuning.SkaterNetRadius, $"{End(end)}: a skater pressed against the back slides along it and round the post (z {sl.Position.Z:0.0})");

        // a carried puck goes with its carrier when the cage stops him
        var carry = Make();
        var c = carry.AddPlayer(Side.Home, skater, At(end, RinkSpec.NetDepth + 0.6f, 0f));
        carry.Puck.Position = At(end, RinkSpec.NetDepth + 1.2f, 0f);
        carry.Puck.Carrier = c.Id;
        var offset = carry.Puck.Position - c.Position;
        Run(carry, 0.5f, each: x =>
        {
            c.Velocity = new Vector3(-end * 6f, 0, 0);
            c.Position += c.Velocity * Game.Step;
            x.Puck.Position += c.Velocity * Game.Step;
        });
        Check(Vector3.Distance(carry.Puck.Position - c.Position, offset) < 0.01f, $"{End(end)}: the carried puck stays on the stick of a skater stopped by the cage");

    }

    /// <summary>W62: a goalie keeps its crease and gets round the cage when it leaves it.</summary>
    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void Goalies(int end)
    {
        var keeper = new PlayerData { Position = PlayingPosition.G };

        // a goalie stands on its line between the posts and shuffles post to post untouched
        bool moved = false;
        var again = Make();
        var ak = again.AddPlayer(Side.Home, keeper, At(end, -0.04f, -0.9f));
        for (int i = 0; i < 72; i++)
        {
            ak.Velocity = new Vector3(0, 0, 1.5f);
            var want = ak.Position + ak.Velocity * Game.Step;
            ak.Position = want;
            Run(again, Game.Step);
            moved |= ak.Position != want;
        }
        Check(!moved && MathF.Abs(PuckMotion.Depth(ak.Position, end) + 0.04f) < 1e-4f, $"{End(end)}: a goalie right on its goal line moves post to post without touching the cage");

        // a goalie going straight out to a puck behind the net gets round the frame, and back again
        var out_ = Make();
        var ok = out_.AddPlayer(Side.Home, keeper, At(end, -0.4f, 0f));
        var target = At(end, RinkSpec.NetDepth + 0.8f, 0f);
        bool inCage = false;
        Run(out_, 4f, each: x =>
        {
            inCage |= NetBody.Overlaps(ok.Position, end, goalie: true);
            var to = target - ok.Position;
            ok.Velocity = to.Length() > 0.05f ? Vector3.Normalize(to) * MathF.Min(4f, to.Length() / Game.Step) : Vector3.Zero;
            ok.Position += ok.Velocity * Game.Step;
        });
        Check(!inCage && Vector3.Distance(ok.Position, target) < 0.2f, $"{End(end)}: a goalie skating straight at a puck behind the net goes round the cage ({Vector3.Distance(ok.Position, target):0.00} m short)");
        var home = At(end, -0.4f, 0f);
        Run(out_, 4f, each: x =>
        {
            inCage |= NetBody.Overlaps(ok.Position, end, goalie: true);
            var to = home - ok.Position;
            ok.Velocity = to.Length() > 0.05f ? Vector3.Normalize(to) * MathF.Min(4f, to.Length() / Game.Step) : Vector3.Zero;
            ok.Position += ok.Velocity * Game.Step;
        });
        Check(!inCage && Vector3.Distance(ok.Position, home) < 0.2f, $"{End(end)}: and back to the crease the same way ({Vector3.Distance(ok.Position, home):0.00} m short)");
    }

    [Test]
    public void Fuzz()
    {
        // random shots from behind and beside both nets, aimed at the cage: none may score or get inside
        // unless it rebounded off the boards and came back in through the mouth
#pragma warning disable S2245 // a fixed seed so every run fuzzes the same shots
        var rng = new Random(11);
#pragma warning restore S2245
        int sneaky = 0, inside = 0, total = 0, goals = 0;
        for (int i = 0; i < 400; i++)
        {
            var w = Make();
            int end = rng.Next(2) == 0 ? -1 : 1;
            float depth = (float)(rng.NextDouble() * 3.0 - 0.5), z = (float)(rng.NextDouble() * 2 - 1) * 6f;
            if (depth < RinkSpec.NetDepth + 0.3f && MathF.Abs(z) < RinkSpec.NetWidth / 2 + 0.3f) continue;
            if (depth < 0 && MathF.Abs(z) < RinkSpec.NetWidth / 2 + 1f) continue;   // that would be a shot from the front
            var from = At(end, depth, z);
            var target = At(end, (float)(rng.NextDouble() * RinkSpec.NetDepth), (float)(rng.NextDouble() * 2 - 1) * RinkSpec.NetWidth / 2, (float)rng.NextDouble() * RinkSpec.NetHeight);
            var dir = Vector3.Normalize(target - from);
            float s = 4 + (float)rng.NextDouble() * 30;
            var velocity = new Vector3(dir.X * s, Math.Max(0, dir.Y * s) + (float)rng.NextDouble() * 3, dir.Z * s);
            Shoot(w, from, velocity);
            total++;
            bool mouth = false;
            var last = from;
            var ev = Run(w, 3f, each: x =>
            {
                var p = x.Puck.Position;
                if (PuckMotion.EnteredThroughMouth(last, p, end)) mouth = true;
                if (!mouth && !Outside(p)) inside++;
                last = p;
            });
            if (Goal(ev)) goals++;
            if (Goal(ev) && !mouth)
            {
                sneaky++;
                Console.WriteLine($"      sneaky: from ({from.X:0.00}, {from.Z:0.00}) at ({velocity.X:0.0}, {velocity.Y:0.0}, {velocity.Z:0.0}) m/s");
            }
        }
        Check(sneaky == 0, $"{total} random shots from behind and beside the nets: {goals} rebounded in through the mouth, {sneaky} got in another way");
        Check(inside == 0, $"and the puck was never inside a net without passing the mouth ({inside} steps inside)");
    }
}
