using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Intermission.Dev;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Overtime.Dev
{
    /// <summary>SAP_SCENE=overtime: a match that ends tied, to watch what follows. Regulation is short
    /// (SAP_PERIOD, default 5 seconds a period) and nobody shoots, so it ends 0-0; then the 3 on 3
    /// overtime (SAP_OT seconds, default 15) and the shootout with AI shooters. SAP_HUMAN=1 puts
    /// keyboard and mouse on the home club, so you take the home shots yourself. SAP_START=overtime or
    /// shootout skips regulation (and overtime) to start right there. SAP_PLAYOFF=1 plays the SHL
    /// playoff format instead: 5 on 5 overtime periods until a goal and no shootout, each after a Zamboni
    /// break (SAP_START=overtime opens in the break before the first; SAP_ZAMBONI_AT starts its drive that
    /// many seconds in). SAP_RULES=shl|arcade|nhl
    /// plays that preset's shootout (SHL away first, NHL home chooses). The score line is on.</summary>
    public sealed class OvertimeScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "overtime";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float period = Seconds("SAP_PERIOD", 5);
            float overtime = Seconds("SAP_OT", 15);
            bool human = Env.Is("SAP_HUMAN", "1");
            bool playoff = Env.Is("SAP_PLAYOFF", "1");
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                PeriodSeconds = period,
                Overtime = OvertimeRule.Shl,
            };
            if (Enum.TryParse(Env.Get("SAP_RULES"), ignoreCase: true, out RulePreset preset))
            {
                setup = setup with { Rules = RuleSet.Of(preset) };
            }

            if (playoff)
            {
                setup = setup with
                {
                    OvertimeFormat = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = overtime },
                };
            }

            if (!human)
            {
                setup = setup with
                {
                    Inputs =
                    [
                        new InputSlot(InputDevice.None, Side.Home),
                        new InputSlot(InputDevice.None, Side.Away),
                    ],
                };
            }

            World world = new(setup, settings);
            world.Switches.OvertimeSeconds = overtime;
            Scenes.AddStarters(world);
            if (
                human
                && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            string start = Env.Get("SAP_START");
            bool shootout = string.Equals(start, "shootout", StringComparison.Ordinal);
            if (shootout || string.Equals(start, "overtime", StringComparison.Ordinal))
            {
                // as if regulation (and the overtime) just ended 0-0
                world.Match.Period = setup.Periods + (shootout ? 1 : 0);
                world.Match.Clock = 0;
                world.Match.Phase = shootout ? MatchPhase.Shootout : MatchPhase.Overtime;
                if (!shootout && MatchRules.BreaksBeforeOvertime(world, MatchPhase.Overtime))
                {
                    // the playoff overtime waits for its Zamboni break, as at the end of a tied regulation
                    world.Match.Phase = MatchPhase.Intermission;
                    world.Puck.Live = false;
                    FlowState flow = MatchFlow.State(world);
                    flow.Timer = MatchTuning.IntermissionSeconds;
                    flow.IntoOvertime = true;
                    ZamboniScene.StartAt(world);
                }
            }

            world.Switches.MatchLine = true;
            return world;
        }

        private static float Seconds(string variable, float fallback)
        {
            return
                float.TryParse(
                    Env.Get(variable),
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
