namespace StickAndPuck.App.Arena
{
    /// <summary>A block of the stands with its own <see cref="CrowdPose"/>, in the order
    /// art/scene/crowd_seats.json lists them (art/scripts/export_arena.py). Far is behind the
    /// benches (the game's -z), near behind the penalty boxes; left and right follow x, and the two
    /// ends are the stands past the corner arcs.</summary>
    public enum CrowdSection
    {
        /// <summary>Far side, the third toward -x.</summary>
        FarLeft,

        /// <summary>Far side, the centre third.</summary>
        FarCentre,

        /// <summary>Far side, the third toward +x.</summary>
        FarRight,

        /// <summary>Near side, the third toward -x.</summary>
        NearLeft,

        /// <summary>Near side, the centre third.</summary>
        NearCentre,

        /// <summary>Near side, the third toward +x.</summary>
        NearRight,

        /// <summary>The end stand behind the -x net.</summary>
        LeftEnd,

        /// <summary>The end stand behind the +x net.</summary>
        RightEnd,
    }
}
