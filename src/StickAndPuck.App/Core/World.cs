using Raylib_cs;

namespace StickAndPuck.App.Core;

/// <summary>
/// The one place match state lives: the setup, the players, the puck, the match, the camera
/// and this step's events. Systems read all of it and change only their own part.
/// </summary>
public sealed class World
{
    /// <summary>Initializes a new world for a match.</summary>
    /// <param name="setup">The clubs, inputs and rules.</param>
    /// <param name="settings">Player options.</param>
    public World(MatchSetup setup, Settings settings)
    {
        Setup = AI.DifficultyScale.Apply(setup, settings.Difficulty);   // W29: easy and hard scale the AI club's traits
        Settings = settings;
        Match.Clock = setup.PeriodSeconds;
    }

    /// <summary>Gets the match setup.</summary>
    public MatchSetup Setup { get; }

    /// <summary>Gets or sets the player options.</summary>
    public Settings Settings { get; set; }

    /// <summary>Gets every player in the match, both clubs, indexed by <see cref="Player.Id"/>.</summary>
    public List<Player> Players { get; } = [];

    /// <summary>Gets the puck.</summary>
    public Puck Puck { get; } = new();

    /// <summary>Gets the score, clock and flow.</summary>
    public MatchState Match { get; } = new();

    /// <summary>Gets this step's events.</summary>
    public MatchEvents Events { get; } = new();

    /// <summary>Gets or sets the 3D camera every scene and mask pass draws with. The camera system owns it.</summary>
    public Camera3D Camera { get; set; }

    /// <summary>Gets the real seconds the world has run, in fixed steps.</summary>
    public float Time { get; internal set; }

    /// <summary>Gets the number of fixed steps run so far.</summary>
    public long Tick { get; internal set; }

    /// <summary>
    /// Gets or sets the match seed (W67): every system's dice are a stream of it, so the same seed
    /// and the same inputs play the same match. 0 is no seed: the dice start from fixed test seeds.
    /// Starts as SAP_SEED (0 when unset); set it before the systems start.
    /// </summary>
    public int Seed { get; set; } = Seeds.Environment;

    // W56: each system's own state, made the first time the system asks for it (these were side tables keyed on the world)

    /// <summary>Gets the AI layer that last set each player's command, by player id (<see cref="AI.AiCommand"/>).</summary>
    internal Slot<Dictionary<int, AI.AiIntent>> AiIntents { get; } = new();

    /// <summary>Gets the intents SAP_AI_TRACE last printed, by player id.</summary>
    internal Slot<Dictionary<int, AI.AiIntent>> AiIntentsPrinted { get; } = new();

    /// <summary>Gets the positioning system's board.</summary>
    internal Slot<AI.PositioningBoard> Positioning { get; } = new();

    /// <summary>Gets the decision system's board.</summary>
    internal Slot<AI.DecisionBoard> Decisions { get; } = new();

    /// <summary>Gets the defence system's board.</summary>
    internal Slot<AI.DefenceBoard> Defence { get; } = new();

    /// <summary>Gets the tactics system's board.</summary>
    internal Slot<AI.TacticsBoard> Tactics { get; } = new();

    /// <summary>Gets the special teams system's board.</summary>
    internal Slot<AI.SpecialTeamsBoard> SpecialTeams { get; } = new();

    /// <summary>Gets the fight in progress and the offer before it.</summary>
    internal Slot<Fights.FightState> Fight { get; } = new();

    /// <summary>Gets the lines, the bench and the changes in progress.</summary>
    internal Slot<Lines.LinesBoard> Lines { get; } = new();

    /// <summary>Gets each club's momentum.</summary>
    internal Slot<Momentum.MomentumState> Momentum { get; } = new();

    /// <summary>Gets or sets the last pass a human made, for the AI receiver to skate onto.</summary>
    internal Maybe<Players.HumanPass> LastHumanPass { get; set; }

    /// <summary>Gets the on-ice referee.</summary>
    internal Slot<Referee.Referee> Referee { get; } = new();

    /// <summary>Gets the pregame warm-up.</summary>
    internal Slot<Pregame.WarmupState> Warmup { get; } = new();

    /// <summary>Gets the icing rule's state.</summary>
    internal Slot<Rules.IcingState> Icing { get; } = new();

    /// <summary>Gets the match flow's state.</summary>
    internal Slot<Rules.FlowState> Flow { get; } = new();

    /// <summary>Gets the offside rule's state.</summary>
    internal Slot<Rules.OffsideState> Offside { get; } = new();

    /// <summary>Gets the overtime and shootout state.</summary>
    internal Slot<Rules.OvertimeState> Overtime { get; } = new();

    /// <summary>Gets the penalty book.</summary>
    internal Slot<Rules.PenaltyBook> Penalties { get; } = new();

    /// <summary>Adds a player from a club's roster.</summary>
    /// <param name="side">Which club.</param>
    /// <param name="data">The roster entry.</param>
    /// <param name="position">Where on the ice.</param>
    /// <returns>The new player.</returns>
    public Player AddPlayer(Side side, PlayerData data, System.Numerics.Vector3 position)
    {
        var p = new Player { Id = Players.Count, Side = side, Data = data, Position = position };
        Players.Add(p);
        return p;
    }

    /// <summary>Gets the players of one club that are on the ice.</summary>
    /// <param name="side">Which club.</param>
    /// <returns>Those players.</returns>
    public IEnumerable<Player> OnIce(Side side)
    {
        return Players.Where(p => p.Side == side && p.OnIce);
    }

    /// <summary>Gets the player a human slot controls, if any.</summary>
    /// <param name="slot">The slot index, 0 or 1.</param>
    /// <returns>The player, or None.</returns>
    public Maybe<Player> Controlled(int slot)
    {
        return Players.Find(p => p.Slot == slot) is { } player ? player : Maybe<Player>.None;
    }
}
