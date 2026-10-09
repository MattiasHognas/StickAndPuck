using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Physics;

/// <summary>
/// W3: the puck in flight. Moves a loose puck with <see cref="PuckMotion"/> (friction, boards,
/// rounded corners, glass, posts, nets, body contact) and runs the goal-line test.
/// Publishes <see cref="MatchEventKind.PuckBoards"/> (boards and glass) and
/// <see cref="MatchEventKind.PuckPost"/> (posts and crossbar) with the impact speed as Strength,
/// and <see cref="MatchEventKind.Goal"/> once when the whole puck is over the goal line of a net
/// while the puck is live: Side is the scoring club, PlayerId the last touch. Rules (W7a) keep the
/// score and restart play; they move the puck out of the net for the faceoff.
/// A body the puck bounces off becomes its last touch, so a deflection counts for the deflector.
/// Goalies are left to W5, which runs before this system.
/// </summary>
internal sealed class PuckPhysics : ISystem, IDisposable
{
    static readonly Color Rubber = new(0x18, 0x13, 0x1f, 255);
    readonly List<PuckImpact> impacts = [];
    readonly List<PuckBody> bodies = [];
    Model puck;
    bool loaded;
    int lastCarrier = -1;
    int releasedBy = -1;
    float releasedAt = float.NegativeInfinity;
    int inNet;          // the end whose net holds the puck past the line (-1, +1), or 0

    public void Start(World world)
    {
        puck = LoadModelFromMesh(GenMeshCylinder(0.12f, 0.05f, 12));
        loaded = true;
    }

    public void Update(World world, float dt)
    {
        PuckDemo.Update(world);
        var pk = world.Puck;
        if (pk.Carrier < 0 && lastCarrier >= 0)
        {
            releasedBy = lastCarrier;
            releasedAt = world.Time;
        }
        lastCarrier = pk.Carrier;

        if (pk.Carrier < 0)
        {
            bodies.Clear();
            bool grace = world.Time - releasedAt < PuckTuning.ReleaseGrace;
            foreach (var p in world.Players)
            {
                if (!p.OnIce || p.IsGoalie) continue;
                if (grace && p.Id == releasedBy) continue;
                bodies.Add(new PuckBody(p.Id, p.Position, p.Velocity));
            }

            impacts.Clear();
            var position = pk.Position;
            var velocity = pk.Velocity;
            PuckMotion.Step(ref position, ref velocity, dt, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bodies), impacts);
            pk.Position = position;
            pk.Velocity = velocity;
            Publish(world, impacts);
        }

        GoalLine(world);
    }

    /// <summary>Publishes the hardest board and post hit of the step, and makes a body hit the last touch.</summary>
    static void Publish(World world, List<PuckImpact> impacts)
    {
        PuckImpact? boards = null, post = null;
        foreach (var i in impacts)
        {
            switch (i.Kind)
            {
                case PuckHit.Boards or PuckHit.Glass when i.Speed > (boards?.Speed ?? PuckTuning.EventSpeed):
                    boards = i;
                    break;
                case PuckHit.Post when i.Speed > (post?.Speed ?? PuckTuning.EventSpeed):
                    post = i;
                    break;
                case PuckHit.Body:
                    world.Puck.LastTouch = i.PlayerId;
                    break;
            }
        }
        if (boards is { } b) world.Events.Publish(new MatchEvent(MatchEventKind.PuckBoards, world.Time, Position: b.Position, Strength: b.Speed));
        if (post is { } p) world.Events.Publish(new MatchEvent(MatchEventKind.PuckPost, world.Time, Position: p.Position, Strength: p.Speed));
    }

    /// <summary>A goal counts once, when the whole puck is over the line, inside the net, while the puck is live.</summary>
    void GoalLine(World world)
    {
        var pk = world.Puck;
        int end = PuckMotion.FullyOverGoalLine(pk.Position, 1) ? 1 : PuckMotion.FullyOverGoalLine(pk.Position, -1) ? -1 : 0;
        if (end == 0) { inNet = 0; return; }
        if (end == inNet || !pk.Live) { inNet = end; return; }
        inNet = end;
        // the net at -x belongs to the club defending the left end; the other club scores there
        var defender = end < 0 ? world.Match.DefendsLeft : Other(world.Match.DefendsLeft);
        world.Events.Publish(new MatchEvent(MatchEventKind.Goal, world.Time, Other(defender), pk.LastTouch, pk.Position, pk.Velocity.Length()));
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }

    public void DrawScene(World world, RenderContext render)
    {
        var pos = world.Puck.Position;
        // a flat shadow under a flying puck, so its height reads from the broadcast camera
        if (pos.Y > 0.05f)
            DrawCylinder(pos with { Y = 0.003f }, 0.1f, 0.1f, 0.004f, 12, new Color(0x3a, 0x2e, 0x48, 255));
        DrawModel(puck, pos, 1f, Rubber);
    }

    public void Dispose()
    {
        if (loaded) UnloadModel(puck);
    }
}
