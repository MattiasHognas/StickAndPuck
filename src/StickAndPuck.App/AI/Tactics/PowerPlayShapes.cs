using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>Maps a club's power play choice to the special teams formation.</summary>
    internal static class PowerPlayShapes
    {
        public static Maybe<Formation> Formation(PowerPlayShape shape)
        {
            return shape switch
            {
                PowerPlayShape.Umbrella => SpecialTeams.Formation.Umbrella,
                PowerPlayShape.OneThreeOne => SpecialTeams.Formation.OneThreeOne,
                PowerPlayShape.Mixed => Maybe.None<Formation>(),
                _ => Maybe.None<Formation>(),
            };
        }
    }
}
