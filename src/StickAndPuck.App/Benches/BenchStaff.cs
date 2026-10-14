using StickAndPuck.App.Core;

namespace StickAndPuck.App.Benches;

/// <summary>What the coach or the trainer is doing (staff.glb's clips).</summary>
public enum StaffPose
{
    /// <summary>Arms folded, following the play.</summary>
    Idle,

    /// <summary>Clapping a goal.</summary>
    Clap,

    /// <summary>Both arms up at the referee.</summary>
    ArmsUp,

    /// <summary>Pointing out at the ice, leaning in (played once and held).</summary>
    Point,

    /// <summary>Leaning over a seated player, a hand on his shoulder, talking.</summary>
    LeanTalk,

    /// <summary>Walking along the step behind the bench.</summary>
    Walk,
}

/// <summary>A club's coach or trainer on the step behind its bench: stands at his spot, claps or
/// throws his arms up for a while, or walks to a seated player, leans in to talk, and walks back.</summary>
public sealed class BenchStaff
{
    /// <summary>Where the staff stand (z): on the 0.3 m step behind the seats (arena.glb's bench_step, 13.75 to 14.25 m out).</summary>
    public const float StepZ = -14f;

    /// <summary>How high the step is (m).</summary>
    public const float StepY = 0.3f;

    /// <summary>How fast the staff walk along the step (m/s).</summary>
    public const float WalkSpeed = 1.6f;

    /// <summary>How far from the player he talks to a staff member stands (m along the bench).</summary>
    const float Beside = 0.3f;

    float target;
    float toward;
    float talkSeconds;
    bool going;
    bool there;

    /// <summary>Initializes a new instance of the <see cref="BenchStaff"/> class.</summary>
    /// <param name="side">The club.</param>
    /// <param name="coach">Whether this is the coach (else the trainer).</param>
    public BenchStaff(Side side, bool coach)
    {
        Side = side;
        Coach = coach;
        Home = BenchLayout.DoorX(side) + (coach ? 0f : -BenchLayout.ForwardsWay(side) * 2.6f);
        X = Home;
        target = Home;
    }

    /// <summary>Gets the club.</summary>
    public Side Side { get; }

    /// <summary>Gets a value indicating whether this is the coach (else the trainer).</summary>
    public bool Coach { get; }

    /// <summary>Gets his own spot (x): the coach behind the middle of the bench at the door, the trainer toward the defence end.</summary>
    public float Home { get; }

    /// <summary>Gets where he is along the step (x).</summary>
    public float X { get; private set; }

    /// <summary>Gets his heading, as <see cref="BenchFigure.Heading"/>: 0 faces the ice.</summary>
    public float Heading { get; private set; }

    /// <summary>Gets what he is doing.</summary>
    public StaffPose Pose { get; private set; }

    /// <summary>Gets the game time his pose started, for a clip played once.</summary>
    public float PoseFrom { get; private set; }

    /// <summary>Gets the game time his clap or arms up ends.</summary>
    public float Until { get; private set; }

    /// <summary>Gets a value indicating whether he is at his own spot and free (not on his way to or with a player).</summary>
    public bool Free => !going && !there && MathF.Abs(X - Home) < 1e-3f;

    /// <summary>Claps or throws his arms up for a while, if he is free.</summary>
    public void Do(StaffPose pose, float seconds, float time)
    {
        if (!Free) return;
        Set(pose, time);
        Until = time + seconds;
    }

    /// <summary>Walks to a seated player at <paramref name="x"/> and talks to him for a while, if he is free.</summary>
    /// <returns>Whether he goes.</returns>
    public bool Visit(float x, float seconds, float time)
    {
        if (!Free) return false;
        toward = x > Home ? 1f : -1f;
        target = x - (toward * Beside);
        talkSeconds = seconds;
        going = true;
        Set(StaffPose.Walk, time);
        return true;
    }

    /// <summary>Gets the game time he will reach the player he is walking to (now, if he is not walking to one).</summary>
    public float Arrives(float time) => going ? time + (MathF.Abs(target - X) / WalkSpeed) : time;

    /// <summary>Moves him one step on.</summary>
    /// <param name="time">The game time.</param>
    /// <param name="dt">The step (s).</param>
    /// <param name="puckX">The puck's x, which an idle staff member's head follows.</param>
    public void Update(float time, float dt, float puckX)
    {
        float goal = going || there ? target : Home;
        float gap = goal - X, step = WalkSpeed * dt;
        if (!there && MathF.Abs(gap) > step)
        {
            X += MathF.Sign(gap) * step;
            Heading = MathF.Sign(gap) * MathF.PI / 2f;
            if (Pose != StaffPose.Walk) Set(StaffPose.Walk, time);
            return;
        }
        X = goal;
        if (going)
        {
            // there: lean in to the player beside him, toward the ice
            going = false;
            there = true;
            Until = time + talkSeconds;
            Set(StaffPose.LeanTalk, time);
        }
        if (there)
        {
            Heading = toward * 0.5f;
            if (Coach && Pose == StaffPose.LeanTalk && time >= PoseFrom + (talkSeconds / 2f)) Set(StaffPose.Point, time);   // and points out at the ice
            if (time >= Until) there = false;
            return;
        }
        if (Pose != StaffPose.Idle && (Pose == StaffPose.Walk || time >= Until)) Set(StaffPose.Idle, time);
        float want = Math.Clamp((puckX - X) * 0.08f, -0.6f, 0.6f);
        Heading += (want - Heading) * MathF.Min(1f, 2f * dt);
    }

    void Set(StaffPose pose, float time)
    {
        Pose = pose;
        PoseFrom = time;
    }
}
