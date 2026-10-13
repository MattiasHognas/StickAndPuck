using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>What one club's passing, shooting and checking came to in a match, for the HUD line and tests.</summary>
public sealed class DecisionTally
{
    /// <summary>Gets or sets the passes made, by humans and AI.</summary>
    public int Passes { get; set; }

    /// <summary>Gets or sets the passes a teammate took.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets the passes an opponent took.</summary>
    public int Intercepted { get; set; }

    /// <summary>Gets or sets the shots taken.</summary>
    public int Shots { get; set; }

    /// <summary>Gets or sets the checks the AI threw.</summary>
    public int Checks { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate who was skating (over 2 m/s) when the puck left.</summary>
    public int ToMoving { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate with no opponent within 3 m when the puck left.</summary>
    public int ToOpen { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate.</summary>
    public int Aimed { get; set; }

    /// <summary>Gets or sets the reads and executions the AI rolled against hockey IQ (the jitter).</summary>
    public int Rolls { get; set; }

    /// <summary>Gets or sets the rolls that came out as a fumble.</summary>
    public int Fumbles { get; set; }

    /// <summary>Gets or sets the rolls that came out lucky.</summary>
    public int Lucky { get; set; }

    /// <summary>Gets or sets the rolls by skaters with hockey IQ 95 or more.</summary>
    public int StarRolls { get; set; }

    /// <summary>Gets or sets the fumbles by skaters with hockey IQ 95 or more.</summary>
    public int StarFumbles { get; set; }

    /// <summary>Gets or sets the times an AI carrier fired the puck out of his own zone under pressure.</summary>
    public int Clears { get; set; }

    /// <summary>Gets or sets the clears fired hard up the ice, the kind that may go the length for icing.</summary>
    public int HardClears { get; set; }

    /// <summary>Gets or sets the times an AI carrier held up at the blue line for a teammate offside to tag up.</summary>
    public int HoldUps { get; set; }

    /// <summary>Gets or sets the times an AI carrier came up to the blue line with a teammate offside and weighed holding up.</summary>
    public int HoldLooks { get; set; }

    /// <summary>Gets the share of passes that reached a teammate, 0 to 1.</summary>
    public float Completion => Passes == 0 ? 0f : (float)Completed / Passes;
}

/// <summary>The decisions' state for a match, and the tallies other code may read.</summary>
public static class Decisions
{

    /// <summary>Gets a club's tally for this match.</summary>
    public static DecisionTally Tally(World world, Side side)
    {
        return Board(world).Tally[(int)side];
    }

    internal static DecisionBoard Board(World world)
    {
        return world.Decisions.Get(() => new DecisionBoard());
    }
}

/// <summary>Per match: each skater's brain, the pass in flight and the tallies.</summary>
internal sealed class DecisionBoard
{
    readonly Dictionary<int, Brain> brains = [];

    public DecisionTally[] Tally { get; } = [new(), new()];

    /// <summary>Gets or sets the pass in flight, until someone takes it or it dies.</summary>
    public Maybe<InFlight> Pass { get; set; }

    /// <summary>Gets or sets the human receiver the AI skates to meet the pass in flight, until the player steers him.</summary>
    public Maybe<HumanMeet> Meet { get; set; }

    /// <summary>Gets or sets the side whose shot is in flight and when it left the stick, for the rebound chase.</summary>
    public Maybe<(Side Side, float At)> Shot { get; set; }

    /// <summary>Gets or sets the carrier at the end of the last step.</summary>
    public int LastCarrier { get; set; } = -1;

    public Brain For(int id)
    {
        if (!brains.TryGetValue(id, out var brain)) brains[id] = brain = new Brain();
        return brain;
    }

    public void ForgetShots()
    {
        foreach (var b in brains.Values) b.ShootSteps = 0;
    }
}

/// <summary>A pass on its way: who sent it, who it is meant for, and when.</summary>
/// <param name="Passer">The passer.</param>
/// <param name="Receiver">The teammate it is meant for, or -1 into space.</param>
/// <param name="Side">The passer's club.</param>
/// <param name="At">When it left the stick.</param>
internal sealed record InFlight(int Passer, int Receiver, Side Side, float At);

/// <summary>A human's pass in flight to a teammate the human now controls. The AI skates him to meet
/// it until the player gives fresh stick input: the stick held as the pass left does not count.</summary>
internal sealed class HumanMeet(int receiver, Vector2 held)
{
    public int Receiver { get; } = receiver;

    /// <summary>Gets the stick held as the pass left.</summary>
    public Vector2 Held { get; } = held;

    /// <summary>Gets or sets a value indicating whether the stick has been at rest since the pass.</summary>
    public bool Released { get; set; }

    /// <summary>Gets or sets a value indicating whether the player has steered, handing the receiver back.</summary>
    public bool HandedBack { get; set; }
}

/// <summary>One AI skater's short memory.</summary>
internal sealed class Brain
{
    /// <summary>Gets or sets when the carrier next looks at the play.</summary>
    public float NextThink { get; set; }

    /// <summary>Gets or sets when this skater got the puck.</summary>
    public float GotAt { get; set; }

    /// <summary>Gets or sets the steps left to hold shoot; 0 when not shooting.</summary>
    public int ShootSteps { get; set; }

    /// <summary>Gets or sets the stick direction held through a shot.</summary>
    public Vector2 ShootAim { get; set; }

    /// <summary>Gets or sets the earliest time this skater thinks about another check.</summary>
    public float CheckReady { get; set; }

    /// <summary>Gets or sets the teammate this skater last passed to.</summary>
    public int PassTo { get; set; } = -1;

    /// <summary>Gets or sets a pass decided on but fumbled: released a beat late, at <see cref="PassAt"/>.</summary>
    public Maybe<PassOption> LatePass { get; set; }

    /// <summary>Gets or sets when a late pass is released.</summary>
    public float PassAt { get; set; }

    /// <summary>Gets or sets the possession (its <see cref="GotAt"/>) whose blue-line hold-up was decided, so it is rolled once.</summary>
    public float HoldRun { get; set; } = float.NaN;

    /// <summary>Gets or sets until when the carrier holds up short of the blue line; past it he carries on.</summary>
    public float HoldUntil { get; set; } = float.NegativeInfinity;

    /// <summary>Gets or sets the possession whose own-zone clear was rolled, so a carrier weighs it once per pressing.</summary>
    public float ClearRun { get; set; } = float.NaN;

    /// <summary>Gets or sets until when this skater goes for a hit on the carrier.</summary>
    public float HitUntil { get; set; } = float.NegativeInfinity;

    /// <summary>Gets or sets the step on which special teams or tactics last claimed this carrier's next action (<see cref="AiCommand.Hold"/>).</summary>
    public long HeldTick { get; set; } = long.MinValue;
}
