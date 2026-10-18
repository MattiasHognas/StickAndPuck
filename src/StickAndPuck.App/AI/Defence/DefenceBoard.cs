using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Defence
{
    /// <summary>The defence's state for one match.</summary>
    internal sealed class DefenceBoard
    {
        private readonly Dictionary<int, DefencePlan> plans = [];

        public IEnumerable<DefencePlan> All => plans.Values;

        public Maybe<DefencePlan> Get(int id)
        {
            return plans.TryGetValue(id, out DefencePlan plan) ? plan : Maybe.None<DefencePlan>();
        }

        public DefencePlan For(int id)
        {
            if (!plans.TryGetValue(id, out DefencePlan plan))
            {
                plans[id] = plan = new DefencePlan();
            }

            return plan;
        }
    }
}
