using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>What one club's special teams came to in a match, for tests and the HUD line.</summary>
public sealed class SpecialTeamsTally
{
    /// <summary>Gets or sets the power plays this club had.</summary>
    public int PowerPlays { get; set; }

    /// <summary>Gets or sets how often this club's power play set up in the zone.</summary>
    public int Entries { get; set; }

    /// <summary>Gets or sets the seconds this club played its power play shape in the zone.</summary>
    public float Seconds { get; set; }

    /// <summary>Gets or sets the seconds of those in the umbrella.</summary>
    public float Umbrella { get; set; }

    /// <summary>Gets or sets the seconds of those in the 1-3-1.</summary>
    public float OneThreeOne { get; set; }

    /// <summary>Gets or sets the power play seconds weighted by the share of skaters within 2.5 m of their spot.</summary>
    public float OnSpot { get; set; }

    /// <summary>Gets or sets the seconds this club killed a penalty in its shape.</summary>
    public float KillSeconds { get; set; }

    /// <summary>Gets or sets the seconds of those in the box.</summary>
    public float Box { get; set; }

    /// <summary>Gets or sets the seconds of those in the diamond.</summary>
    public float Diamond { get; set; }

    /// <summary>Gets or sets the kill seconds weighted by the share of killers on their spot or pressing the carrier.</summary>
    public float KillOnSpot { get; set; }

    /// <summary>Gets or sets the shots this club's power play let go on its own read.</summary>
    public int Shots { get; set; }

    /// <summary>Gets or sets the clears this club fired while short-handed.</summary>
    public int Clears { get; set; }

    /// <summary>Gets or sets the clears flipped high over sticks.</summary>
    public int Flips { get; set; }

    /// <summary>Gets or sets the clears that left the zone within three seconds.</summary>
    public int ClearedOut { get; set; }

    /// <summary>Gets the share of power play time skaters held their spots, 0 to 1.</summary>
    public float Shape => Seconds <= 0 ? 0 : OnSpot / Seconds;

    /// <summary>Gets the share of kill time the killers held their spots or pressed, 0 to 1.</summary>
    public float KillShape => KillSeconds <= 0 ? 0 : KillOnSpot / KillSeconds;
}

/// <summary>W22c: the special teams' state for a match, and what other code may read of it.</summary>
public static class SpecialTeams
{
    static readonly ConditionalWeakTable<World, SpecialTeamsBoard> Boards = [];

    /// <summary>Gets a club's special teams tally for this match.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The tally.</returns>
    public static SpecialTeamsTally Tally(World world, Side side)
    {
        return Board(world).Tally[(int)side];
    }

    /// <summary>Gets the shape a club plays this step.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>Its formation, or <see cref="Formation.None"/> when special teams are not steering it.</returns>
    public static Formation FormationOf(World world, Side side)
    {
        return Board(world).Shape[(int)side];
    }

    /// <summary>Gets a skater's spot in his club's special teams shape this step.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The spot (x, z), or null.</returns>
    public static Vector2? SpotOf(World world, Player p)
    {
        return Board(world).Spots[(int)p.Side].TryGetValue(p.Id, out var spot) ? spot : null;
    }

    internal static SpecialTeamsBoard Board(World world)
    {
        return Boards.GetValue(world, _ => new SpecialTeamsBoard());
    }
}

/// <summary>The special teams' state for one match.</summary>
internal sealed class SpecialTeamsBoard
{
    readonly Dictionary<int, SpotPlan> plans = [];

    public SpecialTeamsTally[] Tally { get; } = [new(), new()];

    /// <summary>Gets or sets each side's shape this step.</summary>
    public Formation[] Shape { get; set; } = [Formation.None, Formation.None];

    /// <summary>Gets each side's spots this step, per player id.</summary>
    public Dictionary<int, Vector2>[] Spots { get; } = [[], []];

    /// <summary>Gets each side's pairing of player id to spot index, kept between steps.</summary>
    public Dictionary<int, int>[] Pairing { get; } = [[], []];

    /// <summary>Gets or sets the club on the current power play, or null.</summary>
    public Side? PowerPlay { get; set; }

    /// <summary>Gets or sets the shape this power play plays.</summary>
    public Formation PpShape { get; set; } = Formation.Umbrella;

    /// <summary>Gets or sets the shape the kill read for this zone entry.</summary>
    public Formation KillShape { get; set; } = Formation.Box;

    /// <summary>Gets or sets whether the power play is set up in the zone.</summary>
    public bool InZone { get; set; }

    /// <summary>Gets or sets the killer whose clear is being timed, or -1.</summary>
    public int ClearCarrier { get; set; } = -1;

    /// <summary>Gets or sets when that killer clears.</summary>
    public float ClearAt { get; set; }

    /// <summary>Gets or sets the last clear still being watched to see it leave the zone.</summary>
    public ClearWatch? Clearing { get; set; }

    public SpotPlan For(int id)
    {
        if (!plans.TryGetValue(id, out var plan)) plans[id] = plan = new SpotPlan();
        return plan;
    }
}

/// <summary>A clear on its way: who fired it, whether it was flipped, when it was decided and when it left the stick (below 0 until then).</summary>
/// <param name="Side">The killing club.</param>
/// <param name="Player">The killer.</param>
/// <param name="Flip">Whether it was flipped high.</param>
/// <param name="At">When the killer let it go.</param>
/// <param name="Released">When the puck left his stick, or -1.</param>
internal sealed record ClearWatch(Side Side, int Player, bool Flip, float At, float Released = -1f);

/// <summary>One skater's misread of his spot.</summary>
internal sealed class SpotPlan
{
    public Vector2 Error { get; set; }

    public float ErrorUntil { get; set; } = float.NegativeInfinity;

    /// <summary>Gets or sets the open ice this power play skater last read around his spot.</summary>
    public Vector2? Open { get; set; }

    /// <summary>Gets or sets when he reads it again.</summary>
    public float OpenNext { get; set; }

    /// <summary>Gets or sets when a power play carrier next looks for a shot.</summary>
    public float ShotLook { get; set; }
}
