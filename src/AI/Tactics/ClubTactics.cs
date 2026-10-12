using System.Text.Json.Serialization;
using StickAndPuck.AI;

namespace StickAndPuck.Core;

/// <summary>
/// W22d: how a club plays, from the <c>tactics</c> field of its file in data/clubs/. A club with
/// no field plays balanced, carries the puck in and mixes its power play. An unknown key or value
/// fails the club load, as an unknown trait does.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClubTactics
{
    /// <summary>Gets how the club plays the other side's breakout.</summary>
    public Forecheck Forecheck { get; init; } = Forecheck.Balanced;

    /// <summary>Gets how the club gets the puck into the attacking zone.</summary>
    public ZoneEntry Entry { get; init; } = ZoneEntry.Carry;

    /// <summary>Gets the power play shape the club sets up, or mixed for either.</summary>
    public PowerPlayShape PowerPlay { get; init; } = PowerPlayShape.Mixed;
}

/// <summary>How a club meets the other side's breakout (W22d).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Forecheck>))]
public enum Forecheck
{
    /// <summary>One forward on the puck, the rest in their shape: the AI as it plays without tactics.</summary>
    Balanced,

    /// <summary>2-1-2: two forwards in deep on the puck and the outlet, a third high, the defence pinching at the blue line.</summary>
    Aggressive,

    /// <summary>1-3-1 neutral zone trap: one forward angles at the blue line, three wait across the red line, one stays back.</summary>
    Trap,
}

/// <summary>How a club gets the puck into the attacking zone (W22d).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ZoneEntry>))]
public enum ZoneEntry
{
    /// <summary>Carry or pass it in, as decisions see fit.</summary>
    Carry,

    /// <summary>Over the red line, fire it into the corner away from the defence and send two forwards after it.</summary>
    DumpAndChase,
}

/// <summary>The power play shape a club sets up (W22d picks W22c's formation).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PowerPlayShape>))]
public enum PowerPlayShape
{
    /// <summary>Either shape, picked at random each power play.</summary>
    Mixed,

    /// <summary>Always the umbrella.</summary>
    Umbrella,

    /// <summary>Always the 1-3-1.</summary>
    OneThreeOne,
}

/// <summary>Maps a club's power play choice to W22c's formation.</summary>
internal static class PowerPlayShapes
{
    public static Formation? Formation(PowerPlayShape shape) => shape switch
    {
        PowerPlayShape.Umbrella => AI.Formation.Umbrella,
        PowerPlayShape.OneThreeOne => AI.Formation.OneThreeOne,
        _ => null,
    };
}
