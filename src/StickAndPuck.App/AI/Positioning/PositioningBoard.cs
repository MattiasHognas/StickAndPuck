using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Positioning's state for one match: roles, jobs and each skater's plan.</summary>
    internal sealed class PositioningBoard
    {
        private readonly Dictionary<int, SkaterPlan> plans = [];

        /// <summary>Gets each side's phase, indexed by <see cref="Side"/>.</summary>
        public TeamPhase[] Phase { get; } = [TeamPhase.Defend, TeamPhase.Defend];

        /// <summary>Gets each side's attacking-zone cycle, indexed by <see cref="Side"/>.</summary>
        public CycleState[] Cycle { get; } = [new(), new()];

        /// <summary>Gets the on-ice skaters the roles were handed out to, so they are only redone after a change.</summary>
        public string Lineup { get; set; } = "";

        public Maybe<SkaterPlan> Get(int id)
        {
            return plans.TryGetValue(id, out SkaterPlan plan) ? plan : Maybe.None<SkaterPlan>();
        }

        public SkaterPlan For(int id)
        {
            if (!plans.TryGetValue(id, out SkaterPlan plan))
            {
                plans[id] = plan = new SkaterPlan();
            }

            return plan;
        }
    }
}
