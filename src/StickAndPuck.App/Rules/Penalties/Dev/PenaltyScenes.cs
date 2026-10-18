using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
    /// <summary>Shared helpers for the penalty scenes.</summary>
    internal static class PenaltyScenes
    {
        /// <summary>Plays the match off screen for a while with every gameplay system, so a headless screenshot shows a later moment.</summary>
        public static void FastForward(World world, float seconds)
        {
            ISystem[] systems =
            [
                new AI.Positioning.PositioningSystem(),
                new AI.Decisions.DecisionSystem(),
                new Lines.LinesSystem(),
                new Players.Movement.SkatingSystem(),
                new Players.Checks.CheckingSystem(),
                new Players.Puck.PuckHandlingSystem(),
                new Goalie.GoalieSystem(),
                new Physics.PuckPhysics(),
                new MatchRules(),
                new OffsideRule(),
                new IcingRule(),
                new PenaltyRules(),
                new Referee.RefereeSystem(),
            ];
            // only the systems whose Start needs no window (the rest load GPU resources there)
            foreach (
                ISystem s in systems.Where(s =>
                    s
                        is AI.Positioning.PositioningSystem
                            or AI.Decisions.DecisionSystem
                            or Lines.LinesSystem
                            or Players.Movement.SkatingSystem
                            or Players.Checks.CheckingSystem
                            or Players.Puck.PuckHandlingSystem
                )
            )
            {
                s.Start(world);
            }

            for (int i = 0; i < seconds / Game.Step; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
            }
            world.Time = 0;
        }
    }
}
