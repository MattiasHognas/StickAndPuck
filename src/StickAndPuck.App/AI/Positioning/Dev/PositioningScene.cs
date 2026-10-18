using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.AI.Positioning.Dev
{
    /// <summary>SAP_SCENE=positioning: a 5 on 5 match between the two clubs under the match flow, every
    /// skater AI, with the camera following the puck. The stand-in play (<see cref="StandInPlay"/>)
    /// shoots and passes. SAP_PERIOD=seconds shortens the periods. SAP_AI_TARGETS=1 draws where each
    /// skater is heading; SAP_AI_HUMAN=1 puts the keyboard on the home centre.</summary>
    public sealed class PositioningScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "positioning";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float period =
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                    ? s
                    : 20 * 60;
            World world = new(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
            Scenes.AddStarters(world);
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre
            )
            {
                centre.Slot = 0;
            }

            StandInPlay standIn = StandInPlay.Start(world);
            MatchFlow.Begin(world, log: false);
            standIn.FollowCamera = true;
            standIn.ShowTargets = Env.Is("SAP_AI_TARGETS", "1");
            return world;
        }
    }
}
