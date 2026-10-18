using System.Text.Json.Serialization;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>How a club meets the other side's breakout.</summary>
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
}
