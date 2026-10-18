using System.Globalization;
using StickAndPuck.App.AI.Positioning.Dev;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>SAP_SCENE=lines: a 5 on 5 match with every skater AI (the stand-in play) where skaters tire
    /// six times faster than normal (SAP_FATIGUE sets the pace), so lines change on the fly every
    /// few seconds. The bottom panel shows each club's unit on the ice, its energy and the goalie.
    /// SAP_PERIOD=seconds shortens the periods; SAP_AI_HUMAN=1 puts the keyboard on the home centre
    /// (L or gamepad LB changes line early).</summary>
    public sealed class LinesScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "lines";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(
                Scenes.DefaultSetup(clubs) with
                {
                    PeriodSeconds = Period(20 * 60),
                },
                settings
            );
            Scenes.AddStarters(world);
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            StandInPlay standIn = StandInPlay.Start(world);
            MatchFlow.Begin(world);
            standIn.FollowCamera = true;
            LinesScenes.Demo(world);
            return world;
        }

        /// <summary>Gets the period length from SAP_PERIOD, or a default.</summary>
        internal static float Period(float fallback)
        {
            return
                float.TryParse(
                    Env.Get("SAP_PERIOD"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float s
                )
                && s > 0
                ? s
                : fallback;
        }
    }
}
