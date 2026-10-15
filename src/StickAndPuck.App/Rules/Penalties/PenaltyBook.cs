using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>The penalty state of one match beside <see cref="MatchState.Penalties"/>.</summary>
internal sealed class PenaltyBook
{
    public List<DelayedPenalty> Delayed { get; } = [];

    /// <summary>Gets the penalties waiting for one of their club's two running penalties to end.</summary>
    public List<PenaltyState> Waiting { get; } = [];

    /// <summary>Gets the skaters whose time is up but who stay in the box until play stops, because a waiting penalty took their place.</summary>
    public HashSet<int> Held { get; } = [];

    /// <summary>Gets the skaters serving a substituted coincidental minor.</summary>
    public HashSet<int> Substituted { get; } = [];

    /// <summary>Gets what each running or waiting penalty costs when it is more than a minor, by player.</summary>
    public Dictionary<int, Severity> Lengths { get; } = [];

    /// <summary>Gets the misconducts running: the time each offender still sits, his club's penalty included.</summary>
    public List<PenaltyState> Misconducts { get; } = [];

    /// <summary>Gets the skaters on their way to the box, skating there while play is stopped.</summary>
    public List<BoxTrip> Trips { get; } = [];

    /// <summary>Gets the players thrown out of the match.</summary>
    public HashSet<int> Ejected { get; } = [];

    /// <summary>Gets each skater's last hit, checker id to the target and when, for roughing.</summary>
    public Dictionary<int, (int Target, float Time)> LastHit { get; } = [];

    /// <summary>Gets or sets when play last stopped, in <see cref="World.Time"/>, for hits after the whistle.</summary>
    public float StoppedAt { get; set; } = float.NegativeInfinity;

    /// <summary>Gets the penalties called per club this match.</summary>
    public int[] Called { get; } = new int[2];

    /// <summary>Gets or sets the game clock at the last step, to run penalty clocks by exactly what the game clock ran.</summary>
    public float LastClock { get; set; } = float.NaN;

    public int LastPeriod { get; set; }

    /// <summary>Gets or sets the skaters a side at full strength this period, read at the first faceoff of a period with nobody in the box.</summary>
    public int BaseSkaters { get; set; } = 5;

    /// <summary>Gets or sets the period whose first faceoff set <see cref="BaseSkaters"/>.</summary>
    public int BasePeriod { get; set; }

    public MatchPhase LastPhase { get; set; } = MatchPhase.Intro;

    /// <summary>Gets or sets the dice: the match seed's stream; scenes and tests seed it.</summary>
    public required Random Rng { get; set; }

    /// <summary>Gets or sets a scripted call for the penalty scene: seconds after the first drop, or NaN.</summary>
    public float ScriptAfterDrop { get; set; } = float.NaN;

    /// <summary>Gets or sets the scripted call's infraction and what it costs (the major scene).</summary>
    public (string Infraction, Severity Severity) ScriptCall { get; set; } = (Infraction.Tripping, Severity.Minor);

    public float FirstDrop { get; set; } = float.NaN;

    public void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine($"PENALTY: {what}; P{m.Period} {MatchFlow.ClockText(m.Clock)}");
    }
}

/// <summary>A penalised skater's skate to the box door: where he is and how fast he goes, kept here so
/// the AI and skating steps that still move him on the ice are put right each step.</summary>
internal sealed class BoxTrip(int playerId, System.Numerics.Vector3 position, System.Numerics.Vector3 velocity, float started)
{
    public int PlayerId { get; } = playerId;

    public System.Numerics.Vector3 Position { get; set; } = position;

    public System.Numerics.Vector3 Velocity { get; set; } = velocity;

    /// <summary>Gets when the trip started, in <see cref="World.Time"/>.</summary>
    public float Started { get; } = started;

    /// <summary>Gets or sets a value indicating whether he has lined up in front of the door.</summary>
    public bool LinedUp { get; set; }
}
