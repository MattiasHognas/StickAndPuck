namespace StickAndPuck.App.Arena
{
    /// <summary>What a section of the crowd is doing: each pose is two frames in art/crowd/crowd_256.png
    /// (art/scripts/build_crowd.py), in this order.</summary>
    public enum CrowdPose
    {
        /// <summary>Seated, now and then a hand to the chin: the crowd between the whistles.</summary>
        Sit,

        /// <summary>On their feet, clapping now and then.</summary>
        Stand,

        /// <summary>Arms up and hopping.</summary>
        Cheer,

        /// <summary>Both arms overhead, swaying side to side.</summary>
        Wave,

        /// <summary>Hands cupped round the mouth, a fist shaken.</summary>
        Boo,
    }
}
