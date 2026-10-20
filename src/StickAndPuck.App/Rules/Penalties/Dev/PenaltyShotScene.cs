using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
    /// <summary>SAP_SCENE=penaltyshot: an AI exhibition where, a moment into each spell of play, the away centre breaks
    /// away over the red line and the home skater a stride behind him trips him from behind. Under SHL and NHL rules
    /// that is a penalty shot: the whistle, PENALTY SHOT on the banner, the ice cleared, the shot from centre against the
    /// goalie and a faceoff at centre after it. In Arcade it is a tripping minor. SAP_RULES=shl|arcade|nhl picks the
    /// rules (default SHL).</summary>
    public sealed class PenaltyShotScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "penaltyshot";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            RuleSet rules = Enum.TryParse(
                Env.Get("SAP_RULES"),
                ignoreCase: true,
                out RulePreset preset
            )
                ? RuleSet.Of(preset)
                : RuleSet.Shl;
            World world = MatchScenes.Exhibition(clubs, settings, 20 * 60, rules);
            world.Dev.PenaltyShot.Set(new PenaltyShotDemo());
            return world;
        }
    }
}
