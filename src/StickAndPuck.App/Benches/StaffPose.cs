namespace StickAndPuck.App.Benches
{
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
}
