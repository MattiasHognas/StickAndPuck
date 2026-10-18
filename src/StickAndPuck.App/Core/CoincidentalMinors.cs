namespace StickAndPuck.App.Core
{
    /// <summary>What equal minors on both clubs at once do.</summary>
    public enum CoincidentalMinors
    {
        /// <summary>Immediate substitution, play stays 5 on 5 (SHL).</summary>
        Substitute,

        /// <summary>One minor each plays 4 on 4 (NHL).</summary>
        FourOnFour,
    }
}
