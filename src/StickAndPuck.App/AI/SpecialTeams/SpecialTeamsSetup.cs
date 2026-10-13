using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.AI;

/// <summary>
/// A match a skater short and the systems to play it: SAP_SCENE=specialteams shows it, and the
/// special teams and AI match tests play it.
/// </summary>
internal static class SpecialTeamsSetup
{
    /// <summary>A match at the opening faceoff with one skater of a club in the box for the whole period.</summary>
    /// <param name="clubs">The clubs.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="kill">The club a skater short.</param>
    /// <returns>The match.</returns>
    internal static World ShortHanded(IReadOnlyList<ClubData> clubs, Settings settings, Side kill)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = 20 * 60, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world, log: false);
        var boxed = world.OnIce(kill).Where(p => !p.IsGoalie).OrderBy(p => p.Data.Position == PlayingPosition.C ? 0 : 1).First();
        world.Match.Penalties.Add(new PenaltyState(boxed.Id, kill, Infraction.Hooking, setup.PeriodSeconds));
        boxed.OnIce = false;
        boxed.Position = Penalties.BoxDoor(kill) with { Z = RinkSpec.HalfWidth + 1.2f };
        return world;
    }

    /// <summary>The AI, skating, puck and rules systems of a special teams match, started on the world.</summary>
    /// <param name="world">The match.</param>
    /// <param name="seed">The seed of every random stream.</param>
    /// <param name="special">Whether special teams play their shapes.</param>
    /// <param name="rules">Whether offside and icing are called too.</param>
    /// <returns>The systems, in update order.</returns>
    internal static ISystem[] Systems(World world, int seed, bool special, bool rules = false)
    {
#pragma warning disable S2245 // fixed seeds so the checks repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed), special), new DecisionSystem(new Random(seed)),
            new SkatingSystem(), new CheckingSystem(new Random(seed)), new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return rules ? [.. systems, new OffsideRule(), new IcingRule()] : systems;
    }
}
