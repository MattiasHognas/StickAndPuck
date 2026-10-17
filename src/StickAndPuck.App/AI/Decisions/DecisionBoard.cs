using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

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

    /// <summary>Gets or sets the drill SAP_SCENE=aimoves runs, if any.</summary>
    public Maybe<Dev.MovesDrill> Drill { get; set; }

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
