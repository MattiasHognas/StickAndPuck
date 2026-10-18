namespace StickAndPuck.App.Lighting
{
    /// <summary>When in the day a match starts. Each start time has its own lighting.</summary>
    public enum TimeOfDay
    {
        /// <summary>An early afternoon start: high, cool daylight through the roof windows, bright ice.</summary>
        Early,

        /// <summary>A late afternoon start: the dusk look of the Blender broadcast render.</summary>
        Afternoon,

        /// <summary>An evening start: low warm arena lights, deep plum shadows, gold light bands.</summary>
        Late,
    }
}
