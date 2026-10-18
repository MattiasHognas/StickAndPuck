using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>The special teams' state for one match.</summary>
    internal sealed class SpecialTeamsBoard
    {
        private readonly Dictionary<int, SpotPlan> plans = [];

        public SpecialTeamsTally[] Tally { get; } = [new(), new()];

        /// <summary>Gets or sets each side's shape this step.</summary>
        public Formation[] Shape { get; set; } = [Formation.None, Formation.None];

        /// <summary>Gets each side's spots this step, per player id.</summary>
        public Dictionary<int, Vector2>[] Spots { get; } =
        [
            [],
            [],
        ];

        /// <summary>Gets each side's pairing of player id to spot index, kept between steps.</summary>
        public Dictionary<int, int>[] Pairing { get; } =
        [
            [],
            [],
        ];

        /// <summary>Gets or sets the club on the current power play, or None.</summary>
        public SideOrNone PowerPlay { get; set; }

        /// <summary>Gets or sets the shape this power play plays.</summary>
        public Formation PpShape { get; set; } = Formation.Umbrella;

        /// <summary>Gets or sets the shape the kill read for this zone entry.</summary>
        public Formation KillShape { get; set; } = Formation.Box;

        /// <summary>Gets or sets whether the power play is set up in the zone.</summary>
        public bool InZone { get; set; }

        /// <summary>Gets or sets the killer whose clear is being timed, or -1.</summary>
        public int ClearCarrier { get; set; } = -1;

        /// <summary>Gets or sets when that killer clears.</summary>
        public float ClearAt { get; set; }

        /// <summary>Gets or sets the last clear still being watched to see it leave the zone.</summary>
        public Maybe<ClearWatch> Clearing { get; set; }

        public SpotPlan For(int id)
        {
            if (!plans.TryGetValue(id, out SpotPlan plan))
            {
                plans[id] = plan = new SpotPlan();
            }

            return plan;
        }
    }
}
