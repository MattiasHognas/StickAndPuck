using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=goalreview: an AI exhibition where, a moment into each spell of play, the home club puts the puck
    /// in from the edge of the crease, in turn cleanly and with a teammate in the crease leaning on the goalie. Under
    /// SHL and NHL rules each goal is held under review (GOAL UNDER REVIEW), then confirmed (GOAL!) or disallowed
    /// (NO GOAL, GOALIE INTERFERENCE) with the faceoff outside the zone; in Arcade the goals count at once.
    /// SAP_RULES=shl|arcade|nhl picks the rules (default SHL). SAP_REVIEW=challenge plays the NHL coach's challenge
    /// instead: the keyboard on the home centre, the visitors score with a man standing in the crease, the review lets
    /// it stand, CHALLENGE THE GOAL? shows and the scene answers it with pass, a challenge for goalie interference that holds.</summary>
    public sealed class GoalReviewScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "goalreview";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            bool challenge = Env.Is("SAP_REVIEW", "challenge");
            RuleSet rules =
                Enum.TryParse(Env.Get("SAP_RULES"), ignoreCase: true, out RulePreset preset)
                    ? RuleSet.Of(preset)
                : challenge ? RuleSet.Nhl
                : RuleSet.Shl;
            World world = MatchScenes.Exhibition(clubs, settings, 20 * 60, rules);
            if (
                challenge
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            world.Dev.GoalReview.Set(new GoalReviewDemo { Challenge = challenge });
            return world;
        }
    }
}
