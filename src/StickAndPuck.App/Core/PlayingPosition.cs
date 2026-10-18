using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
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
}
