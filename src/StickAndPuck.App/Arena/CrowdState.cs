namespace StickAndPuck.App.Arena
{
    /// <summary>Each section's crowd pose, which the arena draws every frame. Presentation only: no
    /// simulation reads it, so setting it never changes a replay. Everyone starts seated; a reacting
    /// crowd (goals, hits, momentum) sets sections from its own update. One per world
    /// (<see cref="Core.World.Crowd"/>).</summary>
    public sealed class CrowdState
    {
        private static readonly CrowdSection[] Sections = Enum.GetValues<CrowdSection>();
        private readonly CrowdPose[] poses = new CrowdPose[Sections.Length];
        private readonly float[] since = new float[Sections.Length];

        /// <summary>Gets the home tifo (W120): whether it is up, and over which section.</summary>
        public TifoState Tifo { get; } = new();

        /// <summary>Gets a section's pose.</summary>
        /// <param name="section">The section.</param>
        /// <returns>What that section is doing.</returns>
        public CrowdPose Pose(CrowdSection section)
        {
            return poses[(int)section];
        }

        /// <summary>Gets the world time a section's pose last changed, so its animation runs from the change.</summary>
        /// <param name="section">The section.</param>
        /// <returns>World seconds; 0 until the first change.</returns>
        public float Since(CrowdSection section)
        {
            return since[(int)section];
        }

        /// <summary>Sets one section's pose; setting the pose it already has keeps its start time.</summary>
        /// <param name="section">The section.</param>
        /// <param name="pose">The new pose.</param>
        /// <param name="time">The world time now (<see cref="Core.World.Time"/>).</param>
        public void Set(CrowdSection section, CrowdPose pose, float time)
        {
            if (poses[(int)section] == pose)
            {
                return;
            }

            poses[(int)section] = pose;
            since[(int)section] = time;
        }

        /// <summary>Sets every section's pose.</summary>
        /// <param name="pose">The new pose.</param>
        /// <param name="time">The world time now.</param>
        public void SetAll(CrowdPose pose, float time)
        {
            foreach (CrowdSection s in Sections)
            {
                Set(s, pose, time);
            }
        }
    }
}
