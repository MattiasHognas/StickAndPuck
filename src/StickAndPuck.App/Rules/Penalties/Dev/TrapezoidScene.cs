using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
    /// <summary>SAP_SCENE=trapezoid: an AI exhibition where, a moment into each spell of play, the home goalie (standing in
    /// for a human one) skates into the corner behind his goal line and plays the puck outside the trapezoid. Under SHL
    /// and NHL rules the lighter trapezoid lines show behind each goal line and that is a delay-of-game minor, served by
    /// his nearest skater, with TRAPEZOID and the goalie on the banner. In Arcade there are no markings and no call.
    /// SAP_RULES=shl|arcade|nhl picks the rules (default SHL).</summary>
    public sealed class TrapezoidScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "trapezoid";

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
            world.Dev.Trapezoid.Set(new TrapezoidDemo());
            return world;
        }
    }
}
