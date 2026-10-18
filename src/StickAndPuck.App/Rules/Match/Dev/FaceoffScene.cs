using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>SAP_SCENE=faceoff: faceoffs at centre with the human on the home left wing, moved onto the centre for each
    /// draw; the scene presses shoot 0.15 s after each drop (a timed win), then whistles the next faceoff.
    /// SAP_FACEOFF_VIOLATION=n also presses n times before each drop: a faceoff violation each, so with 1 a
    /// winger takes the draw and with 2 under SAP_RULES=nhl the second is a delay-of-game minor.
    /// SAP_RULES=shl|arcade|nhl picks the rules (default SHL).</summary>
    public sealed class FaceoffScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "faceoff";

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
                world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.LW) is
                { } wing
            )
            {
                wing.Slot = 0;
            }

            world.Dev.Match.Set(
                new MatchDemo
                {
                    PressAfter = 0.15f,
                    EarlyPresses = int.TryParse(
                        Env.Get("SAP_FACEOFF_VIOLATION"),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out int early
                    )
                        ? Math.Max(0, early)
                        : 0,
                }
            );
            return world;
        }
    }
}
