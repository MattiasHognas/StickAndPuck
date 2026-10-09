using Raylib_cs;

namespace StickAndPuck.Core;

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
        Setup = setup;
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
    /// <returns>The player, or null.</returns>
    public Player? Controlled(int slot)
    {
        return Players.Find(p => p.Slot == slot);
    }
}
