using System.Text.Json.Serialization;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>The power play shape a club sets up (it picks the special teams formation).</summary>
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
}
