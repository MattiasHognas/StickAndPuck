using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>Which side the player holds the stick on.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<Hand>))]
    public enum Hand
    {
        /// <summary>Shoots left.</summary>
        Left,

        /// <summary>Shoots right.</summary>
        Right,
    }
}
