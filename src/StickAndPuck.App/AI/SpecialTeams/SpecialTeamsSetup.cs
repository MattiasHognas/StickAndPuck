using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>A match a skater short and the systems to play it: SAP_SCENE=specialteams shows it, and the
    /// special teams and AI match tests play it.</summary>
    internal static class SpecialTeamsSetup
    {
        /// <summary>A match at the opening faceoff with one skater of a club in the box for the whole period.</summary>
        internal static World ShortHanded(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Side kill
        )
        {
            return ShortHanded(clubs, settings, kill, RuleSet.Shl);
        }

        /// <summary>A match at the opening faceoff with one skater of a club in the box for the whole period, played by the given rules.</summary>
        internal static World ShortHanded(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Side kill,
            RuleSet rules
        )
        {
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                Rules = rules,
                PeriodSeconds = 20 * 60,
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new(setup, settings);
            Scenes.AddStarters(world);
            MatchFlow.Begin(world, log: false);
            Player boxed = world
                .OnIce(kill)
                .Where(p => !p.IsGoalie)
                .OrderBy(p => p.Data.Position == PlayingPosition.C ? 0 : 1)
                .First();
            world.Match.Penalties.Add(
                new PenaltyState(boxed.Id, kill, Infraction.Hooking, setup.PeriodSeconds)
            );
            boxed.OnIce = false;
            boxed.Position = PenaltyCalls.BoxDoor(kill) with { Z = RinkSpec.HalfWidth + 1.2f };
            return world;
        }

        /// <summary>The AI, skating, puck and rules systems of a special teams match, started on the world.</summary>
        internal static ISystem[] Systems(World world, int seed, bool special, bool rules = false)
        {
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(seed)),
                new DefenceSystem(Seeds.Dice(seed)),
                new SpecialTeamsSystem(Seeds.Dice(seed), special),
                new DecisionSystem(Seeds.Dice(seed)),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(seed)),
                new PuckHandlingSystem(Seeds.Dice(seed)),
                new GoalieSystem { Dice = Seeds.Dice(seed) },
                new PuckPhysics(),
                new MatchRules(),
            ];
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return rules ? [.. systems, new OffsideRule(), new IcingRule()] : systems;
        }
    }
}
