using StickAndPuck.App.Core;

namespace StickAndPuck.App.Hud.Dev
{
    /// <summary>SAP_SCENE=hud: both starting lineups with the scorebug at 2-1 in the second period and one
    /// banner up. SAP_HUD_MESSAGE picks it: goal (default), period, final, tie, offside, icing, intro,
    /// hattrick, firstgoal, shutout, gwg (final with the game-winning goal), pp (a power play, no banner)
    /// or none. For screenshots of the HUD without waiting for a match to get there.</summary>
    public sealed class HudScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "hud";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            MatchState m = world.Match;
            m.Score[0] = 2;
            m.Score[1] = 1;
            m.Period = 2;
            m.Clock = 734.4f;
            world.Switches.HudAlways = true;
            world.Switches.HudMessage = Env.Get("SAP_HUD_MESSAGE") is { Length: > 0 } s
                ? s
                : "goal";
            if (string.Equals(world.Switches.HudMessage, "intro", StringComparison.Ordinal))
            {
                m.Phase = MatchPhase.Intro;
            }

            if (string.Equals(world.Switches.HudMessage, "shutout", StringComparison.Ordinal))
            {
                m.Score[1] = 0;
            }

            if (string.Equals(world.Switches.HudMessage, "firstgoal", StringComparison.Ordinal))
            {
                m.Score[0] = 0;
                world.Switches.SeasonGoals = (_, _) => 0; // as if the club had not scored yet this season
            }
            if (
                string.Equals(world.Switches.HudMessage, "pp", StringComparison.Ordinal)
                && world.OnIce(Side.Away).FirstOrDefault(p => !p.IsGoalie) is { } boxed
            )
            {
                m.Penalties.Add(new PenaltyState(boxed.Id, Side.Away, "tripping", 74));
            }

            return world;
        }
    }
}
