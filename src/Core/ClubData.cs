using System.Text.Json.Serialization;

namespace StickAndPuck.Core;

/// <summary>A club as written in its data file under data/clubs/: names, kit colours and the roster.</summary>
public sealed record ClubData
{
    /// <summary>Gets the file-safe id, also used for kit atlas names (for example "frolunda").</summary>
    public string Id { get; init; } = "";

    /// <summary>Gets the full club name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets the three-letter name for the scorebug.</summary>
    public string ShortName { get; init; } = "";

    /// <summary>Gets the home kit colours.</summary>
    public KitColours Home { get; init; } = new();

    /// <summary>Gets the away kit colours.</summary>
    public KitColours Away { get; init; } = new();

    /// <summary>Gets the match squad: 4 forward lines, 3 defence pairs and 2 goalies in a full club.</summary>
    public IReadOnlyList<PlayerData> Roster { get; init; } = [];
}

/// <summary>A kit's colours as palette hex strings such as "#c8574a".</summary>
public sealed record KitColours
{
    /// <summary>Gets the main jersey colour.</summary>
    public string Primary { get; init; } = "#c8574a";

    /// <summary>Gets the second colour: stripes, shoulders, numbers.</summary>
    public string Secondary { get; init; } = "#efe0c2";

    /// <summary>Gets the trim colour.</summary>
    public string Accent { get; init; } = "#2a2c40";

    /// <summary>Gets the helmet colour.</summary>
    public string Helmet { get; init; } = "#2a2c40";
}

/// <summary>One player in a club's roster.</summary>
public sealed record PlayerData
{
    /// <summary>Gets the jersey number.</summary>
    public int Number { get; init; }

    /// <summary>Gets the first name.</summary>
    public string FirstName { get; init; } = "";

    /// <summary>Gets the last name, shown on the HUD.</summary>
    public string LastName { get; init; } = "";

    /// <summary>Gets the playing position.</summary>
    public PlayingPosition Position { get; init; }

    /// <summary>Gets the stick hand.</summary>
    public Hand Shoots { get; init; } = Hand.Left;

    /// <summary>Gets the player's traits.</summary>
    public Traits Traits { get; init; } = new();
}

/// <summary>
/// A player's traits, each on the same 0 to 100 scale where 50 is an average SHL player.
/// Skating, physics, goalie and AI read these instead of constants; fatigue scales them down.
/// </summary>
public sealed record Traits
{
    /// <summary>Gets how fast the player gets up to speed.</summary>
    public int Acceleration { get; init; } = 50;

    /// <summary>Gets the player's top skating speed.</summary>
    public int TopSpeed { get; init; } = 50;

    /// <summary>Gets how close shots land to where they are aimed.</summary>
    public int ShotAccuracy { get; init; } = 50;

    /// <summary>Gets how hard the player shoots.</summary>
    public int ShotPower { get; init; } = 50;

    /// <summary>Gets how hard the player hits and how well they stand up to hits.</summary>
    public int Checking { get; init; } = 50;

    /// <summary>Gets how well the player keeps and receives the puck.</summary>
    public int PuckControl { get; init; } = 50;

    /// <summary>Gets how well the player picks and places passes.</summary>
    public int SmartPasses { get; init; } = 50;

    /// <summary>Gets how well the player finds the right spot on the ice.</summary>
    public int SmartPositioning { get; init; } = 50;

    /// <summary>Gets how quickly a goalie reacts to shots.</summary>
    public int GoalieReflexes { get; init; } = 50;

    /// <summary>Maps a trait value to 0..1.</summary>
    /// <param name="value">A trait value on the 0 to 100 scale.</param>
    /// <returns>The value as a fraction, clamped to 0..1.</returns>
    public static float Unit(int value)
    {
        return Math.Clamp(value / 100f, 0f, 1f);
    }
}

/// <summary>A playing position.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlayingPosition>))]
public enum PlayingPosition
{
    /// <summary>Centre.</summary>
    C,

    /// <summary>Left wing.</summary>
    LW,

    /// <summary>Right wing.</summary>
    RW,

    /// <summary>Defence.</summary>
    D,

    /// <summary>Goalie.</summary>
    G,
}

/// <summary>Which side the player holds the stick on.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Hand>))]
public enum Hand
{
    /// <summary>Shoots left.</summary>
    Left,

    /// <summary>Shoots right.</summary>
    Right,
}
