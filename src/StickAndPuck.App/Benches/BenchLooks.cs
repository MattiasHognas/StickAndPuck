namespace StickAndPuck.App.Benches;

/// <summary>Which clip, and where in it, a bench figure or staff member shows.</summary>
/// <param name="Model">The model: <see cref="BenchClips.Skater"/>, <see cref="BenchClips.Goalie"/> or <see cref="BenchClips.Staff"/>.</param>
/// <param name="Clip">The clip's name without the model's prefix.</param>
/// <param name="Seconds">How far into the clip (s).</param>
/// <param name="Loop">Whether the clip loops (else it holds its last frame).</param>
/// <param name="Hold">A frame held for the whole reaction, or -1 to play the clip.</param>
internal readonly record struct BenchLook(int Model, string Clip, float Seconds, bool Loop = true, int Hold = -1);

/// <summary>The clips the bench figures and staff play, all in one place, and which one each shows.</summary>
internal static class BenchClips
{
    /// <summary>The skater model.</summary>
    public const int Skater = 0;

    /// <summary>The goalie model.</summary>
    public const int Goalie = 1;

    /// <summary>The staff model.</summary>
    public const int Staff = 2;

    /// <summary>The frame rate the clips are made at.</summary>
    public const float Fps = 24f;

    /// <summary>The seat top over the deck (arena.glb: a 0.2 m riser and the seat), less the clips' 0.45 m from soles to seat (m).</summary>
    public const float SeatLift = 0.62f - 0.45f;

    /// <summary>How far a seated goalie sinks into his seat, his ready clip crouching rather than sitting (m).</summary>
    public const float GoalieLift = 0.1f;

    /// <summary>How far into taking the helmet off the bare head shows (s): as the hand lifts it.</summary>
    public const float BareFrom = 0.5f;

    /// <summary>The argue clip's frame with the right arm jabbing a point out at the ice (pose 1 of 4, 8 frames each).</summary>
    const int ArguePoint = 8;

    /// <summary>The argue clip's frame with the left fist pumped up (pose 3).</summary>
    const int ArgueFist = 24;

    /// <summary>The helmet_off clip's frame with the hand up at the helmet (pose 1 of 4, 6 frames each).</summary>
    const int HandToHelmet = 6;

    /// <summary>Gets every clip the benches play, by model, and whether the stick stays in the hands (else it is folded away so it does not poke through the boards).</summary>
    public static IEnumerable<(int Model, string Clip, bool Stick)> All =>
    [
        (Skater, "sit", false), (Skater, "sit_tired", false), (Skater, "stand_boards", false), (Skater, "idle", false),
        (Skater, "slump", false), (Skater, "chat", false), (Skater, "helmet_off", false), (Skater, "argue", false),
        (Skater, "celebrate", true), (Skater, "bang", true),
        (Goalie, "ready", true),
        (Staff, "idle", true), (Staff, "clap", true), (Staff, "arms_up", true), (Staff, "point", true), (Staff, "lean_talk", true), (Staff, "walk", true),
    ];

    /// <summary>Gets a model's clip prefix in its glb.</summary>
    public static string Prefix(int model) => model switch { Skater => "skater", Goalie => "goalie", _ => "staff" };

    /// <summary>Gets what a bench figure shows at a game time.</summary>
    public static BenchLook Of(BenchFigure f, float time)
    {
        float loop = time + f.Phase;
        if (f.Player.IsGoalie) return new(Goalie, "ready", loop);
        if (f.Walking) return new(Skater, "idle", loop);
        if (f.Pose != BenchPose.Watch) return new(Skater, "stand_boards", loop);
        float t = time - f.ReactFrom;
        return f.Showing(time) switch
        {
            BenchReaction.Cheer => new(Skater, "celebrate", t + f.Phase),
            BenchReaction.Bang => new(Skater, "bang", t + f.Phase),
            BenchReaction.Slump => new(Skater, "slump", t + f.Phase),
            BenchReaction.Argue => new(Skater, "argue", t),
            BenchReaction.FistPump => new(Skater, "argue", t, Hold: ArgueFist),
            BenchReaction.Point => new(Skater, "argue", t, Hold: ArguePoint),
            BenchReaction.StandFight => new(Skater, "stand_boards", loop),
            BenchReaction.Chat => new(Skater, "chat", t + f.Phase),
            BenchReaction.Ooh => new(Skater, "helmet_off", t, Hold: HandToHelmet),
            BenchReaction.HelmetOff => new(Skater, "helmet_off", t, Loop: false),
            _ => new(Skater, time < f.TiredUntil ? "sit_tired" : "sit", loop),
        };
    }

    /// <summary>Gets whether a figure shows his bare head: helmet off, or coming off now.</summary>
    public static bool BareAt(BenchFigure f, float time) => f.Bare || (f.Showing(time) == BenchReaction.HelmetOff && time - f.ReactFrom >= BareFrom);

    /// <summary>Gets what a staff member shows at a game time.</summary>
    public static BenchLook Of(BenchStaff s, float time)
    {
        float t = time - s.PoseFrom;
        return s.Pose switch
        {
            StaffPose.Clap => new(Staff, "clap", t),
            StaffPose.ArmsUp => new(Staff, "arms_up", t),
            StaffPose.Point => new(Staff, "point", t, Loop: false),
            StaffPose.LeanTalk => new(Staff, "lean_talk", t),
            StaffPose.Walk => new(Staff, "walk", t),
            _ => new(Staff, "idle", time + (s.Coach ? 0f : 1.3f)),
        };
    }
}

/// <summary>The bench staff's art (A12), beside the other player models.</summary>
internal static class BenchStaffArt
{
    /// <summary>The staff model with its idle, clap, arms_up, point, lean_talk and walk clips.</summary>
    public const string Model = "art/player/staff.glb";

    /// <summary>The coach's atlas: suit and tie.</summary>
    public const string Coach = "art/player/staff_coach_64.png";

    /// <summary>The trainer's atlas.</summary>
    public const string Trainer = "art/player/staff_trainer_64.png";
}
