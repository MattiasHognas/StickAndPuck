using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Render;
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
/// A goal needs the puck to have come in through the mouth: the cage is solid from behind, from the
/// sides and from above, and a puck carried through the mesh on a stick and let go inside is pushed
/// back out instead of counting.
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
    int scored;         // the end whose net the current goal was published for (-1, +1), or 0
    int inNet;          // the end whose net holds the puck because it came in through the mouth, or 0
    Vector3? lastPosition;

    public void Start(World world)
    {
        puck = LoadModelFromMesh(GenMeshCylinder(PuckLook.Radius, PuckLook.Thickness, 12));
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
        Track(pk);

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
            PuckMotion.Step(ref position, ref velocity, ref inNet, dt, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bodies), impacts);
            pk.Position = position;
            pk.Velocity = velocity;
            Publish(world, impacts);
        }

        lastPosition = pk.Position;
        GoalLine(world);
    }

    /// <summary>
    /// Follows a puck that something other than physics moved since the last step: a carrier's stick, a
    /// goalie, a faceoff. Only a move from in front of the goal line into the net counts as going in; a
    /// puck pushed through the mesh from behind or the side is not in the net and is ejected once it is
    /// loose. A puck placed with no history, or moved further than anything on the ice can move it in one
    /// step, is taken at its word.
    /// </summary>
    void Track(Puck pk)
    {
        var p = pk.Position;
        if (lastPosition is not { } last || Vector3.Distance(last, p) > PuckTuning.TeleportDistance)
        {
            inNet = PuckMotion.InsideNet(p, 1) ? 1 : PuckMotion.InsideNet(p, -1) ? -1 : 0;
            return;
        }
        if (last == p) return;
        if (inNet != 0)
        {
            if (!PuckMotion.InsideNet(p, inNet)) inNet = 0;
            return;
        }
        for (int end = -1; end <= 1; end += 2)
            if (PuckMotion.EnteredThroughMouth(last, p, end)) inNet = end;
    }

    /// <summary>Gets the end (-1, +1) whose net holds the puck after coming in through the mouth, or 0.</summary>
    public int InNet => inNet;

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

    /// <summary>A goal counts once, when the whole puck is over the line, inside a net it came into through the mouth, while the puck is live.</summary>
    void GoalLine(World world)
    {
        var pk = world.Puck;
        int end = inNet != 0 && PuckMotion.FullyOverGoalLine(pk.Position, inNet) ? inNet : 0;
        if (end == 0) { scored = 0; return; }
        if (end == scored || !pk.Live) { scored = end; return; }
        scored = end;
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
        // W35: an airborne puck drops a crisp dark shadow on the ice and grows a little, so its height reads from the broadcast camera
        if (PuckLook.Airborne(pos.Y))
        {
            float r = PuckLook.Shadow(pos.Y);
            DrawCylinder(pos with { Y = PuckLook.ShadowLift }, r, r, 0.004f, 16, PuckLook.ShadowColour);
        }
        float scale = PuckLook.Scale(pos.Y);
        DrawModelEx(puck, pos, Vector3.UnitY, 0f, new Vector3(scale), Rubber);
    }

    public void Dispose()
    {
        if (loaded) UnloadModel(puck);
    }
}
