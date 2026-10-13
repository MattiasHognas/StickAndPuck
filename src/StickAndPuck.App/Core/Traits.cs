using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core;

/// <summary>
/// A player's traits, each on the same 0 to 100 scale where 50 is an average SHL player.
/// Skating, physics, goalie and AI read these instead of constants; fatigue scales them down.
/// Trait names in the club files must match these properties; an unknown name fails the load,
/// so a stale or misspelled field cannot silently fall back to 50.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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

    /// <summary>
    /// Gets the player's hockey IQ: how well they read the play. One trait feeds every AI read:
    /// vision, lane reading, how often the carrier re-thinks, pass leading and weight, finding
    /// the right spot, and later getting open, the cycle, special teams and tactics.
    /// The value only sets the odds; the skill-scaled randomness the AI rolls each time decides
    /// what happens, so the same situation is not always played the same way.
    /// </summary>
    public int HockeyIq { get; init; } = 50;

    /// <summary>Gets how quickly a goalie reacts to shots.</summary>
    public int GoalieReflexes { get; init; } = 50;

    /// <summary>Gets how well a goalie's glove hand stops shots on that side; the weaker hand is the side shooters pick.</summary>
    public int GoalieGlove { get; init; } = 50;

    /// <summary>Gets how well a goalie's blocker (stick hand) stops shots on that side.</summary>
    public int GoalieBlocker { get; init; } = 50;

    /// <summary>Gets how often a goalie holds a shot, and how soft and wide the rebounds it leaves are.</summary>
    public int GoalieReboundControl { get; init; } = 50;

    /// <summary>Gets how well a goalie plays the puck: stopping dump-ins behind the net and passing it out to a teammate.</summary>
    public int GoaliePuckHandling { get; init; } = 50;

    /// <summary>Gets <see cref="HockeyIq"/> as 0..1, the odds the AI's reads roll against.</summary>
    [JsonIgnore]
    public float Iq => Unit(HockeyIq);

    /// <summary>Maps a trait value to 0..1.</summary>
    /// <param name="value">A trait value on the 0 to 100 scale.</param>
    /// <returns>The value as a fraction, clamped to 0..1.</returns>
    public static float Unit(int value)
    {
        return Math.Clamp(value / 100f, 0f, 1f);
    }

    /// <summary>
    /// Maps a trait value onto a range: what a 0 player gets and what a 100 player gets, linearly
    /// between. The result is the expected value for that skill; the AI's jitter rolls around it.
    /// </summary>
    /// <param name="value">A trait value on the 0 to 100 scale.</param>
    /// <param name="atZero">The result for a 0 player.</param>
    /// <param name="atHundred">The result for a 100 player.</param>
    /// <returns>The value for this player.</returns>
    public static float Scale(int value, float atZero, float atHundred)
    {
        return atZero + (atHundred - atZero) * Unit(value);
    }
}
