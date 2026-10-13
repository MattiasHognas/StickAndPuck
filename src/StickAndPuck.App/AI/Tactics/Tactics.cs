using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>What one club's play came to in a match, measured the same way whatever its tactics, so
/// clubs can be compared.</summary>
public sealed class TacticsTally
{
    /// <summary>Gets or sets the times the club got the puck over the other side's blue line.</summary>
    public int Entries { get; set; }

    /// <summary>Gets or sets the entries made with a skater carrying the puck over the line.</summary>
    public int Carried { get; set; }

    /// <summary>Gets or sets the dump-ins the club's tactics fired into the corner.</summary>
    public int Dumps { get; set; }

    /// <summary>Gets or sets the dumps the club got back: one of its skaters was next on the puck.</summary>
    public int Recovered { get; set; }

    /// <summary>Gets or sets the seconds the other side carried the puck in its own zone.</summary>
    public float BreakoutSeconds { get; set; }

    /// <summary>Gets or sets the skater-seconds the club had inside the other side's zone during those breakouts.</summary>
    public float DeepSkaterSeconds { get; set; }

    /// <summary>Gets or sets the skater-seconds the club had between the far blue line and its own blue line, waiting, during those breakouts.</summary>
    public float NeutralSkaterSeconds { get; set; }

    /// <summary>Gets or sets the times the other side carried the puck out over its own blue line.</summary>
    public int Exits { get; set; }

    /// <summary>Gets or sets the club's skaters back, goal side of the carrier, summed over those exits.</summary>
    public int BackAtExits { get; set; }

    /// <summary>Gets how many of the club's skaters were back, goal side of the puck, on average, as the other side carried it out of its zone.</summary>
    public float Back => Exits == 0 ? 0f : (float)BackAtExits / Exits;

    /// <summary>Gets the share of entries carried in, 0 to 1.</summary>
    public float CarryShare => Entries == 0 ? 0f : (float)Carried / Entries;

    /// <summary>Gets how many of the club's skaters were in on the forecheck, on average, while the other side broke out.</summary>
    public float Forecheckers => BreakoutSeconds <= 0 ? 0f : DeepSkaterSeconds / BreakoutSeconds;

    /// <summary>Gets how many of the club's skaters waited in the neutral zone, on average, while the other side broke out.</summary>
    public float Trappers => BreakoutSeconds <= 0 ? 0f : NeutralSkaterSeconds / BreakoutSeconds;
}

/// <summary>Club tactics' state for a match, and the tallies other code may read.</summary>
public static class Tactics
{

    /// <summary>Gets a side's tactics, from its club file.</summary>
    public static ClubTactics Of(World world, Side side)
    {
        return world.Setup.Club(side).Tactics;
    }

    /// <summary>Gets a club's tally for this match.</summary>
    public static TacticsTally Tally(World world, Side side)
    {
        return Board(world).Tally[(int)side];
    }

    /// <summary>Gets what tactics has a skater doing this step, for tests and the debug line.</summary>
    public static TacticTask TaskOf(World world, Player p)
    {
        return Board(world).Tasks.GetValueOrDefault(p.Id);
    }

    internal static TacticsBoard Board(World world)
    {
        return world.Tactics.Get(() => new TacticsBoard());
    }
}

/// <summary>What club tactics has a skater doing this step.</summary>
public enum TacticTask
{
    /// <summary>Left to positioning, defence and decisions.</summary>
    None,

    /// <summary>Aggressive forecheck: the second forward in, on the carrier's outlet.</summary>
    Outlet,

    /// <summary>Aggressive forecheck: the third forward, high in the zone.</summary>
    High,

    /// <summary>Aggressive forecheck: a defenceman holding the blue line.</summary>
    Pinch,

    /// <summary>Trap: the first forward, angling at the blue line.</summary>
    Angle,

    /// <summary>Trap: one of the three across the red line.</summary>
    Wall,

    /// <summary>Trap: the defenceman staying back.</summary>
    Back,

    /// <summary>Dump and chase: a forward going in after the dump.</summary>
    Chase,
}

/// <summary>Club tactics' state for one match.</summary>
internal sealed class TacticsBoard
{
    public TacticsTally[] Tally { get; } = [new(), new()];

    /// <summary>Gets each skater's task this step.</summary>
    public Dictionary<int, TacticTask> Tasks { get; } = [];

    /// <summary>Gets each skater's spot as he reads it, trailing the tactic's spot.</summary>
    public Dictionary<int, (Vector2 At, long Tick)> Reads { get; } = [];

    /// <summary>Gets or sets the carrier and run with the puck whose zone entry was weighed, so a run is weighed once.</summary>
    public (int Player, float GotAt) Weighed { get; set; } = (-1, 0f);

    /// <summary>Gets or sets the dump the carrier is setting up, or None.</summary>
    public Maybe<DumpPlan> Dump { get; set; }

    /// <summary>Gets or sets the side whose dump is being chased, and until when.</summary>
    public Maybe<ChaseWatch> Chasing { get; set; }

    /// <summary>Gets or sets the side that last carried the puck, or None before anyone has.</summary>
    public SideOrNone Owner { get; set; }

    /// <summary>Gets or sets when that side got the puck.</summary>
    public float OwnedSince { get; set; }

    /// <summary>Gets where the other side's carrier was last step, along each side's attack ("a"), to spot breakouts leaving the zone.</summary>
    public float[] LastCarrierA { get; } = [float.NegativeInfinity, float.NegativeInfinity];

    /// <summary>Gets where the puck was last step, along each side's attack ("a"), to spot entries.</summary>
    public float[] LastA { get; } = [0f, 0f];
}
