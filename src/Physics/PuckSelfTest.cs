using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Physics;

/// <summary>
/// SAP_SCENE=pucktest: runs the puck physics checks without opening a window, prints one line per
/// check and exits with 0 when all pass, 1 otherwise. Covers the W3 "done when": corner shots
/// rebound plausibly, and a goal counts only when the whole puck is over the line.
/// </summary>
public sealed class PuckSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "pucktest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs);
        Func<World> make = () => new World(setup, settings);

        CornerRebound(make());
        RimAroundCorner(make());
        GoalCountsOnce(make());
        GoalLineEdge(make);
        PostRingsOut(make());
        BehindTheNet(make());
        GlassAboveBoards(make());
        BodyBlock(make, clubs);
        FrictionStops(make());
        Fuzz(make);

        Console.WriteLine(failures == 0 ? "PUCKTEST: all checks passed" : $"PUCKTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

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

    static bool InsideRink(Vector3 p)
    {
        float cr = RinkSpec.CornerRadius, cx = RinkSpec.HalfLength - cr, cz = RinkSpec.HalfWidth - cr;
        float ax = MathF.Abs(p.X), az = MathF.Abs(p.Z);
        if (ax > RinkSpec.HalfLength + 1e-3f || az > RinkSpec.HalfWidth + 1e-3f) return false;
        if (ax > cx && az > cz) return new Vector2(ax - cx, az - cz).Length() <= cr + 1e-3f;
        return true;
    }

    void CornerRebound(World w)
    {
        // a hard pass straight into the rounded corner comes back out, slower, still on the ice
        var dir = Vector3.Normalize(new Vector3(1, 0, 1));
        Shoot(w, new Vector3(18, 0, 0), dir * 20);
        var ev = Run(w, 1.2f);
        var v = w.Puck.Velocity;
        Check(ev.Any(e => e.Kind == MatchEventKind.PuckBoards), "corner shot hits the boards");
        Check(Vector3.Dot(v, dir) < 0, $"corner shot comes back out of the corner (v = {v.X:0.0}, {v.Z:0.0})");
        Check(v.Length() is > 4 and < 16, $"corner rebound keeps some speed ({v.Length():0.0} of 20 m/s)");
        Check(InsideRink(w.Puck.Position), "corner shot stays inside the rink");
    }

    void RimAroundCorner(World w)
    {
        // a shot along the side boards rims round the corner and comes out along the end boards
        Shoot(w, new Vector3(10, 0, RinkSpec.HalfWidth - 0.3f), new Vector3(25, 0, 0.5f));
        Run(w, 1.6f);
        var p = w.Puck.Position; var v = w.Puck.Velocity;
        Check(p.X > RinkSpec.HalfLength - RinkSpec.CornerRadius && p.Z < RinkSpec.HalfWidth - RinkSpec.CornerRadius + 1,
            $"rim follows the corner round (at {p.X:0.0}, {p.Z:0.0})");
        Check(v.Z < -3, $"rim leaves the corner along the end boards (vz = {v.Z:0.0})");
    }

    void GoalCountsOnce(World w)
    {
        w.Puck.LastTouch = 3;
        Shoot(w, new Vector3(18, 0, 0.2f), new Vector3(30, 1.5f, 0));
        var ev = Run(w, 2f);
        var goals = ev.Where(e => e.Kind == MatchEventKind.Goal).ToList();
        Check(goals.Count == 1, $"a shot into the net is one goal ({goals.Count})");
        Check(goals.Count == 1 && goals[0].Side == w.Match.DefendsLeft && goals[0].PlayerId == 3,
            "the goal goes to the club attacking that net, credited to the last touch");
        Check(PuckMotion.FullyOverGoalLine(w.Puck.Position, 1) && w.Puck.Velocity.Length() < 2,
            $"the mesh keeps the puck in the net (speed {w.Puck.Velocity.Length():0.0})");
    }

    void GoalLineEdge(Func<World> make)
    {
        // a puck sliding to a stop on the line: only a puck wholly past the line's far edge counts
        float r = RinkSpec.PuckRadius, half = PuckMotion.GoalLineHalfWidth;
        foreach (var (u, goal) in new[] { (0f, false), (r, false), (r + half - 0.003f, false), (r + half + 0.003f, true) })
        {
            var w = make();
            Shoot(w, new Vector3(RinkSpec.GoalLineX + u, 0, 0.3f), Vector3.Zero);
            int n = Run(w, 0.1f).Count(e => e.Kind == MatchEventKind.Goal);
            Check(n == (goal ? 1 : 0), $"puck at {u * 100:0.0} cm past the line centre: {(goal ? "goal" : "no goal")}");
        }
        var dead = make();
        Shoot(dead, new Vector3(RinkSpec.GoalLineX + 0.3f, 0, 0), Vector3.Zero);
        dead.Puck.Live = false;
        Check(Run(dead, 0.1f).All(e => e.Kind != MatchEventKind.Goal), "no goal while the puck is dead");
    }

    void PostRingsOut(World w)
    {
        // aimed at the far post's outer half: it rings off and stays out
        Shoot(w, new Vector3(18, 0, RinkSpec.NetWidth / 2 + 0.03f), new Vector3(30, 0, 0));
        var ev = Run(w, 1f);
        Check(ev.Any(e => e.Kind == MatchEventKind.PuckPost), "a shot at the post rings off it");
        Check(ev.All(e => e.Kind != MatchEventKind.Goal), "the post shot is no goal");
        Check(w.Puck.Position.X < RinkSpec.GoalLineX || MathF.Abs(w.Puck.Position.Z) > RinkSpec.NetWidth / 2,
            "the post shot ends up outside the net");

        var bar = new World(w.Setup, w.Settings);
        Shoot(bar, new Vector3(24, RinkSpec.NetHeight + 0.04f, 0), new Vector3(28, 0, 0));
        var bev = Run(bar, 0.6f);
        Check(bev.Any(e => e.Kind == MatchEventKind.PuckPost), "a shot at the crossbar hits it");
    }

    void BehindTheNet(World w)
    {
        // from behind the net into its back: bounces off, never a goal
        Shoot(w, new Vector3(RinkSpec.HalfLength - 0.5f, 0, 0.1f), new Vector3(-15, 0, 0));
        var ev = Run(w, 1.5f);
        Check(ev.All(e => e.Kind != MatchEventKind.Goal), "a puck into the back of the net is no goal");
        Check(w.Puck.Position.X > RinkSpec.GoalLineX + RinkSpec.NetDepth, "it stays behind the net");

        var side = new World(w.Setup, w.Settings);
        Shoot(side, new Vector3(RinkSpec.GoalLineX + 0.5f, 0, 4), new Vector3(0, 0, -15));
        var sev = Run(side, 1.5f);
        Check(sev.All(e => e.Kind != MatchEventKind.Goal) && side.Puck.Position.Z > 0, "a puck into the side of the net bounces off it");
    }

    void GlassAboveBoards(World w)
    {
        // a lifted puck high off the side glass drops back onto the ice
        Shoot(w, new Vector3(0, 0, 4), new Vector3(0, 6, 22));
        bool glass = false;
        var ev = Run(w, 3f, each: x => glass |= x.Puck.Position.Z > RinkSpec.HalfWidth - 0.1f && x.Puck.Position.Y > RinkSpec.BoardHeight);
        Check(glass && ev.Any(e => e.Kind == MatchEventKind.PuckBoards), "a high shot hits the glass above the boards");
        Check(w.Puck.Position.Y == 0 && InsideRink(w.Puck.Position), "and falls back onto the ice");
    }

    void BodyBlock(Func<World> make, IReadOnlyList<ClubData> clubs)
    {
        var w = make();
        var skater = clubs[0].Roster.FirstOrDefault(p => p.Position != PlayingPosition.G) ?? new PlayerData { Position = PlayingPosition.D };
        var block = w.AddPlayer(Side.Away, skater, new Vector3(5, 0, 0));
        Shoot(w, new Vector3(0, 0, 0), new Vector3(20, 0, 0));
        Run(w, 0.5f);
        Check(w.Puck.Velocity.X < 0 && w.Puck.Velocity.Length() < 8, $"a shot into a body drops off it ({w.Puck.Velocity.X:0.0} m/s)");
        Check(w.Puck.LastTouch == block.Id, "the blocker becomes the last touch");

        var over = make();
        over.AddPlayer(Side.Away, skater, new Vector3(5, 0, 0));
        Shoot(over, new Vector3(0, 1.2f, 0), new Vector3(20, 9, 0));
        Run(over, 0.5f);
        Check(over.Puck.Position.X > 6, "a puck lifted over a body flies past it");
    }

    void FrictionStops(World w)
    {
        Shoot(w, new Vector3(-20, 0, -5), new Vector3(9, 0, 0));
        Run(w, 6f);
        float v1 = w.Puck.Velocity.Length();
        Run(w, 14f);
        Check(v1 > 1 && v1 < 7, $"a 9 m/s slide still glides after 6 s ({v1:0.0} m/s)");
        Check(w.Puck.Velocity.Length() < 0.01f, "and comes to rest");
    }

    void Fuzz(Func<World> make)
    {
        // many random shots: the puck never leaves the rink, never gets into a net except through the mouth
#pragma warning disable S2245 // a fixed seed so every run fuzzes the same shots
        var rng = new Random(7);
#pragma warning restore S2245
        int escapes = 0, sneaky = 0, goals = 0;
        for (int i = 0; i < 600; i++)
        {
            var w = make();
            var from = new Vector3((float)(rng.NextDouble() * 2 - 1) * 28, 0, (float)(rng.NextDouble() * 2 - 1) * 11.5f);
            if (MathF.Abs(MathF.Abs(from.X) - RinkSpec.GoalLineX - 0.6f) < 1.2f && MathF.Abs(from.Z) < 1.5f) continue;
            if (!InsideRink(from)) continue;
            float a = (float)(rng.NextDouble() * MathF.Tau), s = 5 + (float)rng.NextDouble() * 40;
            Shoot(w, from, new Vector3(MathF.Cos(a) * s, (float)rng.NextDouble() * 6, MathF.Sin(a) * s));
            bool mouth = false;
            var last = from;
            var ev = Run(w, 4f, each: x =>
            {
                var p = x.Puck.Position;
                for (int end = -1; end <= 1; end += 2)
                {
                    if (end * last.X <= RinkSpec.GoalLineX && end * p.X > RinkSpec.GoalLineX && MathF.Abs(p.Z) < RinkSpec.NetWidth / 2 && p.Y < RinkSpec.NetHeight)
                        mouth = true;
                }

                if (!InsideRink(p) || p.Y < 0) escapes++;
                last = p;
            });
            int g = ev.Count(e => e.Kind == MatchEventKind.Goal);
            goals += g;
            if (g > 0 && !mouth) sneaky++;
        }
        Check(escapes == 0, $"600 random shots never leave the rink ({escapes} steps outside)");
        Check(sneaky == 0, $"every goal came in through the mouth ({goals} goals, {sneaky} not)");
    }
}
