using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Camera;

/// <summary>
/// The last few seconds of play, one frame per fixed step: every player's place, heading and
/// body action, and the puck. Frames are reused in a ring, so recording allocates nothing once full.
/// </summary>
internal sealed class ReplayBuffer
{
    /// <summary>One player's pose in a frame.</summary>
    internal struct PlayerPose
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Heading;
        public bool OnIce;
        public PlayerAction Action;
        public float ActionTime;
    }

    /// <summary>One recorded step.</summary>
    internal sealed class Frame
    {
        public long Tick;
        public float Time;
        public PlayerPose[] Players = [];
        public int PlayerCount;
        public Vector3 PuckPosition;
        public Vector3 PuckVelocity;
        public int PuckCarrier;
    }

    readonly Frame[] ring;
    int next;

    /// <summary>Initializes a buffer that keeps the given number of steps.</summary>
    /// <param name="capacity">Steps kept, the oldest dropped first.</param>
    public ReplayBuffer(int capacity)
    {
        ring = new Frame[capacity];
        for (int i = 0; i < capacity; i++) ring[i] = new Frame();
    }

    /// <summary>Gets the number of frames held.</summary>
    public int Count { get; private set; }

    /// <summary>Gets a frame by age order: 0 is the oldest held, Count - 1 the newest.</summary>
    /// <param name="index">The frame's index from the oldest.</param>
    /// <returns>The frame.</returns>
    public Frame this[int index] => ring[(next - Count + index + ring.Length * 2) % ring.Length];

    /// <summary>Records the world as it is now, dropping the oldest frame when full.</summary>
    /// <param name="world">The match state.</param>
    public void Record(World world)
    {
        Capture(world, ring[next]);
        next = (next + 1) % ring.Length;
        Count = Math.Min(Count + 1, ring.Length);
    }

    /// <summary>Forgets everything recorded.</summary>
    public void Clear()
    {
        Count = 0;
    }

    /// <summary>Gets the index of the newest frame at or before a time, or 0 if all are later.</summary>
    /// <param name="time">World time in seconds.</param>
    /// <returns>The frame index.</returns>
    public int IndexAt(float time)
    {
        for (int i = Count - 1; i > 0; i--)
            if (this[i].Time <= time) return i;
        return 0;
    }

    /// <summary>Copies the world's players and puck into a frame.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="frame">The frame to fill.</param>
    public static void Capture(World world, Frame frame)
    {
        frame.Tick = world.Tick;
        frame.Time = world.Time;
        if (frame.Players.Length < world.Players.Count) frame.Players = new PlayerPose[world.Players.Count];
        frame.PlayerCount = world.Players.Count;
        for (int i = 0; i < world.Players.Count; i++)
        {
            var p = world.Players[i];
            frame.Players[i] = new PlayerPose
            {
                Position = p.Position, Velocity = p.Velocity, Heading = p.Heading,
                OnIce = p.OnIce, Action = p.Action, ActionTime = p.ActionTime,
            };
        }
        frame.PuckPosition = world.Puck.Position;
        frame.PuckVelocity = world.Puck.Velocity;
        frame.PuckCarrier = world.Puck.Carrier;
    }

    /// <summary>
    /// Puts a frame back into the world: players and puck where they were. The puck's Live flag
    /// and everything rules own are left alone.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="world">The match state.</param>
    public static void Apply(Frame frame, World world)
    {
        int n = Math.Min(frame.PlayerCount, world.Players.Count);
        for (int i = 0; i < n; i++)
        {
            var p = world.Players[i];
            var pose = frame.Players[i];
            p.Position = pose.Position;
            p.Velocity = pose.Velocity;
            p.Heading = pose.Heading;
            p.OnIce = pose.OnIce;
            p.Action = pose.Action;
            p.ActionTime = pose.ActionTime;
        }
        world.Puck.Position = frame.PuckPosition;
        world.Puck.Velocity = frame.PuckVelocity;
        world.Puck.Carrier = frame.PuckCarrier;
    }
}
