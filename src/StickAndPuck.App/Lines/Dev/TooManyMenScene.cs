using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>SAP_SCENE=toomanymen: an exhibition with the keyboard on the home centre where, a moment into each spell
    /// of play, the scene presses change line and then again to rush the change: the fresh skaters step out at
    /// once while the tired ones still skate for the bench, and the first one out gets the puck. Under SHL and NHL
    /// rules that is too many men on the ice, a bench minor; in Arcade nothing is called. SAP_RULES=shl|arcade|nhl
    /// picks the rules (default SHL).</summary>
    public sealed class TooManyMenScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "toomanymen";

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
            if (
                world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is
                { } centre
            )
            {
                centre.Slot = 0;
            }

            world.Switches.LinesPanel = true;
            world.Dev.TooManyMen.Set(new TooManyMenDemo());
            return world;
        }
    }
}
