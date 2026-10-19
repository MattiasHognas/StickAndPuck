using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>W121: whose fans sit where. One corner section, <see cref="AwaySection"/>, is the visiting
    /// club's: its seats wear the away club's colours (<see cref="CrowdRenderer"/>), it reacts to the
    /// visitors' goals and hits (<see cref="CrowdReactions"/>) and its chant is the away chant
    /// (Audio/CrowdChants). Every other section is the home club's, so playing away the arena is against you.</summary>
    internal static class CrowdFans
    {
        /// <summary>The visitors' corner: the far side's third toward +x, in the broadcast camera's view.</summary>
        public const CrowdSection AwaySection = CrowdSection.FarRight;

        /// <summary>Gets the club a section's fans support.</summary>
        /// <param name="section">The section.</param>
        /// <returns><see cref="Side.Away"/> for <see cref="AwaySection"/>, otherwise <see cref="Side.Home"/>.</returns>
        public static Side Of(CrowdSection section)
        {
            return section == AwaySection ? Side.Away : Side.Home;
        }

        /// <summary>Gives every section of a reacting crowd to its club.</summary>
        /// <param name="reactions">The crowd's reactions.</param>
        public static void Seat(CrowdReactions reactions)
        {
            foreach (CrowdSection section in Enum.GetValues<CrowdSection>())
            {
                reactions.SetFans(section, Of(section));
            }
        }
    }
}
