using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.AI;

/// <summary>A match a skater short and the systems to play it: SAP_SCENE=specialteams shows it, and the
/// special teams and AI match tests play it.</summary>
internal static class SpecialTeamsSetup
{
    /// <summary>A match at the opening faceoff with one skater of a club in the box for the whole period.</summary>
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
    internal static ISystem[] Systems(World world, int seed, bool special, bool rules = false)
    {
#pragma warning disable S2245 // The AI simulation RNGs are not security-sensitive and are required by the game systems.
        ISystem[] systems =
        [
            new PositioningSystem(new Random(seed)), new DefenceSystem(new Random(seed)), new SpecialTeamsSystem(new Random(seed), special), new DecisionSystem(new Random(seed)),
            new SkatingSystem(), new CheckingSystem(new Random(seed)), new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            s.Start(world);
        return rules
            ? [.. systems, new OffsideRule(), new IcingRule()]
            : systems;
    }
}
