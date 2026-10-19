using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=overglass: an AI exhibition where, a second into each spell of play, a home defenceman flips
    /// the puck high over the far glass from his own zone: the puck leaves the rink, play stops and under the SHL and
    /// NHL he takes a delay-of-game minor, the draw in his end. SAP_GLASS_FROM=attack flips it from the zone home
    /// attacks instead (a stoppage, no minor, the draw outside that zone). SAP_RULES=shl|arcade|nhl picks the rules
    /// (default SHL); in Arcade the netting keeps the puck in play.</summary>
    public sealed class OverGlassScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "overglass";

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
            bool own = !string.Equals(
                Env.Get("SAP_GLASS_FROM"),
                "attack",
                StringComparison.OrdinalIgnoreCase
            );
            world.Dev.Match.Set(new MatchDemo { ClearsFromOwnZone = own });
            return world;
        }
    }
}
