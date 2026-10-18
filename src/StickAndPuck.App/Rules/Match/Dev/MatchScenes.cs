using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>Shared setup for the match scenes.</summary>
    internal static class MatchScenes
    {
        /// <summary>Builds an exhibition between the default clubs with both starting lineups and begins the match flow.</summary>
        public static World Exhibition(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            float periodSeconds
        )
        {
            return Exhibition(clubs, settings, periodSeconds, Scenes.DefaultSetup(clubs).Rules);
        }

        /// <summary>Builds an exhibition as above, played by the given rules.</summary>
        public static World Exhibition(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            float periodSeconds,
            RuleSet rules
        )
        {
            if (
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
            )
            {
                periodSeconds = s;
            }

            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                PeriodSeconds = periodSeconds,
                Rules = rules,
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world);
            return world;
        }
    }
}
