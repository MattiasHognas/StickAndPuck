using System.Text.Json.Serialization;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>How a club gets the puck into the attacking zone.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ZoneEntry>))]
    public enum ZoneEntry
    {
        /// <summary>Carry or pass it in, as decisions see fit.</summary>
        Carry,

        /// <summary>Over the red line, fire it into the corner away from the defence and send two forwards after it.</summary>
        DumpAndChase,
    }
}
