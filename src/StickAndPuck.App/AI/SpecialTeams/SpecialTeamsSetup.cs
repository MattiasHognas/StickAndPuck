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
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
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
#pragma warning disable S2245 // The AI simulation RNGs are not security-sensitive and are required by the game systems.
            ISystem[] systems =
            [
                new PositioningSystem(new Random(seed)),
                new DefenceSystem(new Random(seed)),
                new SpecialTeamsSystem(new Random(seed), special),
                new DecisionSystem(new Random(seed)),
                new SkatingSystem(),
                new CheckingSystem(new Random(seed)),
                new PuckHandlingSystem(new Random(seed)),
                new GoalieSystem { Dice = new Random(seed) },
                new PuckPhysics(),
                new MatchRules(),
            ];
#pragma warning restore S2245
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return rules ? [.. systems, new OffsideRule(), new IcingRule()] : systems;
        }
    }
}
